using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FuzzySharp;
using Microsoft.Extensions.Logging;
using RZND.Bot.Providers;

namespace RZND.Bot.Services;

/// <summary>
/// Serviço responsável por normalizar títulos e cruzar/comparar ofertas utilizando heurísticas
/// locais, gabarito de modelos (mismatch safeguard) e a biblioteca FuzzySharp.
/// </summary>
public sealed class ProductMatcherService : IProductMatcherService
{
    private readonly IAiNormalizer _aiNormalizer;
    private readonly ILogger<ProductMatcherService> _logger;

    /// <summary>
    /// Construtor que recebe as dependências necessárias via injeção.
    /// </summary>
    public ProductMatcherService(IAiNormalizer aiNormalizer, ILogger<ProductMatcherService> logger)
    {
        _aiNormalizer = aiNormalizer;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ComparisonResult> CompareOffersAsync(OfferDetails a, OfferDetails b, CancellationToken ct)
    {
        _logger.LogInformation("ProductMatcher: Comparando oferta '{TitleA}' ({ProviderA}) com '{TitleB}' ({ProviderB})", 
            a.Title, a.Provider, b.Title, b.Provider);

        // 1. Normalização pesada
        string titleA = await NormalizeTitleAsync(a.Title, ct);
        string titleB = await NormalizeTitleAsync(b.Title, ct);

        // 2. Proteção contra "Colisão de Modelo" (A20 vs S20)
        if (HasDifferentModelNumbers(titleA, titleB))
        {
            _logger.LogWarning("ProductMatcher: Colisão de modelo detectada entre '{TitleA}' e '{TitleB}'. Cruzamento rejeitado.", titleA, titleB);
            return new ComparisonResult(
                IsSameProduct: false,
                ConfidenceScore: 0,
                Details: "Modelos/Identificadores diferentes detectados (Regra de Colisão)"
            );
        }

        // 3. Comparação Fuzzy
        int score = Fuzz.TokenSetRatio(titleA, titleB);
        bool isMatch = score > 85;

        decimal? priceDiff = null;
        if (isMatch)
        {
            priceDiff = Math.Abs(a.Price - b.Price);
            _logger.LogInformation("ProductMatcher: Match confirmado com score {Score}%! Diferença de preço: R$ {Diff:F2}", score, priceDiff);
        }
        else
        {
            _logger.LogInformation("ProductMatcher: Correspondência rejeitada com score {Score}% (limiar: 85%).", score);
        }

        return new ComparisonResult(
            IsSameProduct: isMatch,
            ConfidenceScore: score,
            Details: isMatch ? "Match confirmado por similaridade fuzzy" : "Baixa similaridade de tokens",
            PriceDifference: priceDiff
        );
    }

    /// <inheritdoc />
    public async Task<string> NormalizeTitleAsync(string rawTitle, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawTitle)) return string.Empty;

        // Limpeza bruta via Regex (marketing noise & emojis)
        string cleaned = CleanMarketingNoise(rawTitle);

        // Chama o AI Normalizer (ou Fallback) para polir a essência (Marca + Modelo + Specs)
        string normalized = await _aiNormalizer.NormalizeAsync(cleaned, ct);

        return normalized;
    }

    private bool HasDifferentModelNumbers(string a, string b)
    {
        var idsA = ExtractModelIdentifiers(a);
        var idsB = ExtractModelIdentifiers(b);

        // Se ambos têm identificadores de modelo ou códigos, a divergência acarreta colisão direta
        foreach (var id in idsA)
        {
            if (idsB.Count > 0 && !idsB.Contains(id))
            {
                return true;
            }
        }

        foreach (var id in idsB)
        {
            if (idsA.Count > 0 && !idsA.Contains(id))
            {
                return true;
            }
        }

        return false;
    }

    private HashSet<string> ExtractModelIdentifiers(string text)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text)) return ids;

        var clean = text.ToLowerInvariant();

        // 1. Códigos alfanuméricos mesclados (ex: s20, a20, ff320)
        var regexAlphaNum = new Regex(@"\b(?=[a-zA-Z]*\d)(?=\d*[a-zA-Z])[a-zA-Z0-9]+\b", RegexOptions.Compiled);
        foreach (Match m in regexAlphaNum.Matches(clean))
        {
            ids.Add(m.Value);
        }

        // 2. Números isolados de versão (1 a 2 dígitos)
        var regexShortNumbers = new Regex(@"\b\d{1,2}\b", RegexOptions.Compiled);
        foreach (Match m in regexShortNumbers.Matches(clean))
        {
            ids.Add(m.Value);
        }

        return ids;
    }

    private string CleanMarketingNoise(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        string cleaned = input;

        // 1. Remover palavras inúteis de marketing (suportando acentos via regex IgnoreCase)
        var noiseTerms = new[]
        {
            "Promoção", "Promocao", "Oferta", "Frete Grátis", "Frete Gratis",
            "Lançamento", "Lancamento", "Queima de Estoque", "Brinde",
            "com NF", "Nota Fiscal", "Original", "Lacrado", "Garantia"
        };
        foreach (var term in noiseTerms)
        {
            cleaned = Regex.Replace(cleaned, Regex.Escape(term), "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        // 2. Remover apenas emojis e símbolos decorativos (não remove letras latinas com acento)
        //    Unicode Category: So = Symbol,Other | Cs = Surrogate | Co = Private Use
        cleaned = Regex.Replace(cleaned, @"[\p{So}\p{Cs}\p{Co}\p{Cn}]+", " ");

        // 3. Normalizar espaços múltiplos
        cleaned = Regex.Replace(cleaned, @"\s+", " ");

        return cleaned.Trim();
    }

    /// <summary>
    /// Remove acentos de uma string normalizando para Unicode Form D e filtrando marcas não-espaçadas.
    /// Útil apenas internamente para comparação - o título exibido mantém os acentos.
    /// </summary>
    private static string StripAccentsForComparison(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
