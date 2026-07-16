using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace RZND.Bot.Services;

/// <summary>
/// Auxiliar de Limpeza e Normalização de Títulos de Produtos.
/// Contém o motor de validação gramatical e o gabarito de modelos (safeguard para A20 vs S20).
/// </summary>
public static class TitleNormalizer
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "de", "do", "da", "em", "para", "com", "o", "a", "os", "as", "um", "uma", "e", "ou", "no", "na", "nos", "nas", "ao", "aos",
        "oficial", "original", "promoção", "promocional", "frete", "grátis", "queima", "estoque", "lacrado", "novo", "nova", "super",
        "oferta", "ofertas", "desconto", "descontos", "compre", "agora", "envio", "imediato", "rápido"
    };

    /// <summary>
    /// Limpa e extrai os tokens essenciais de um título.
    /// </summary>
    public static HashSet<string> Tokenize(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return new HashSet<string>();

        // Remover decorações e emojis
        string clean = RemoveAccents(title).ToLowerInvariant();
        clean = Regex.Replace(clean, @"[^\w\s\-\.]", " "); // mantém letras, números, traços e pontos

        var rawWords = clean.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var w in rawWords)
        {
            if (w.Length > 1 && !StopWords.Contains(w))
            {
                tokens.Add(w);
            }
        }

        return tokens;
    }

    /// <summary>
    /// Extrai os identificadores de modelo críticos (ex: s20, a20, ff320, 14, 15) do título.
    /// </summary>
    public static HashSet<string> ExtractModelIdentifiers(string title)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(title)) return ids;

        var clean = RemoveAccents(title).ToLowerInvariant();

        // 1. Capturar códigos alfanuméricos com letras e números mesclados (ex: s20, a20, ff320, ff358, g9)
        var regexAlphaNum = new Regex(@"\b(?=[a-zA-Z]*\d)(?=\d*[a-zA-Z])[a-zA-Z0-9]+\b", RegexOptions.Compiled);
        foreach (Match m in regexAlphaNum.Matches(clean))
        {
            ids.Add(m.Value);
        }

        // 2. Capturar números de versão curtos (1 a 2 dígitos) isolados (ex: iphone 14 vs iphone 15, ou gopro 9 vs gopro 10)
        var regexShortNumbers = new Regex(@"\b\d{1,2}\b", RegexOptions.Compiled);
        foreach (Match m in regexShortNumbers.Matches(clean))
        {
            ids.Add(m.Value);
        }

        return ids;
    }

    /// <summary>
    /// Verifica se dois títulos são textualmente compatíveis considerando o limiar de palavras 
    /// e a regra rígida de colisão de modelos (gabarito).
    /// </summary>
    public static bool AreCompatible(string titleA, string titleB, double threshold = 0.70)
    {
        // 1. Gabarito de Modelos: extrair os identificadores críticos de ambos
        var idsA = ExtractModelIdentifiers(titleA);
        var idsB = ExtractModelIdentifiers(titleB);

        // Se ambos possuem identificadores alfanuméricos/versões, eles DEVEM bater.
        // Se houver qualquer divergência (ex: A tem "s20" e B tem "a20", ou A tem "14" e B tem "15"), são produtos distintos!
        foreach (var id in idsA)
        {
            // Se o ID existe no outro título de forma diferente (ex: "s20" presente em A mas B possui "a20" e não possui "s20")
            if (idsB.Count > 0 && !idsB.Contains(id))
            {
                // Se existe uma colisão direta na família (ex: A contem 's20' mas não contem 'a20')
                // Vamos invalidar o cruzamento imediatamente.
                return false;
            }
        }

        foreach (var id in idsB)
        {
            if (idsA.Count > 0 && !idsA.Contains(id))
            {
                return false;
            }
        }

        // 2. Coeficiente de Sobreposição dos tokens normais
        var tokensA = Tokenize(titleA);
        var tokensB = Tokenize(titleB);

        if (tokensA.Count == 0 || tokensB.Count == 0) return false;

        var intersection = new HashSet<string>(tokensA, StringComparer.OrdinalIgnoreCase);
        intersection.IntersectWith(tokensB);

        int minSize = Math.Min(tokensA.Count, tokensB.Count);
        double similarity = (double)intersection.Count / minSize;

        return similarity >= threshold;
    }

    private static string RemoveAccents(string text)
    {
        string normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (char c in normalized)
        {
            var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
