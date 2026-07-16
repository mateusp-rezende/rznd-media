using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace RZND.Bot.Services;

/// <summary>
/// Normalizador de Fallback local para extração e limpeza de títulos usando heurísticas locais.
/// </summary>
public sealed class FallbackAiNormalizer : IAiNormalizer
{
    private static readonly string[] NoiseTerms = new[]
    {
        "novo", "nova", "original", "lançamento", "lancamento", "queima de estoque", 
        "com nf", "nota fiscal", "garantia", "imperdível", "barato", "lindo", 
        "excelente", "melhor preço", "melhor preco", "pronta entrega", "envio imediato", 
        "lacrado", "oficial", "100%", "promocao", "promoção", "oferta", "frete gratis", "frete grátis"
    };

    /// <inheritdoc />
    public Task<string> NormalizeAsync(string complexTitle, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(complexTitle))
        {
            return Task.FromResult(string.Empty);
        }

        // 1. Remover emojis e caracteres especiais decorativos
        string cleaned = Regex.Replace(complexTitle, @"[^\w\s\-\.\/]", " ");

        // 2. Remover termos comuns de marketing/ruído
        foreach (var term in NoiseTerms)
        {
            cleaned = Regex.Replace(cleaned, $@"\b{term}\b", "", RegexOptions.IgnoreCase);
        }

        // 3. Normalizar espaços em branco extras
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

        return Task.FromResult(cleaned);
    }
}
