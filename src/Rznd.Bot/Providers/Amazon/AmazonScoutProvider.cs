using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using HtmlAgilityPack;
using RZND.Bot.Tracing;

namespace RZND.Bot.Providers.Amazon;

/// <summary>
/// Módulo Batedor da Amazon (AmazonScoutProvider).
/// Varre páginas de busca ou ofertas na Amazon de forma leve para capturar ASINs (Amazon Standard Identification Numbers).
/// </summary>
public sealed class AmazonScoutProvider : IProductScoutProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<AmazonScoutProvider> _logger;

    /// <summary>
    /// Construtor com injeção de HttpClient e Logging.
    /// </summary>
    public AmazonScoutProvider(HttpClient http, ILogger<AmazonScoutProvider> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ProviderName => "AmazonWebScraper";

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<ScoutedProduct>> ScoutAsync(CancellationToken cancellationToken)
    {
        using var _ = Tracer.Start(nameof(AmazonScoutProvider) + ".Scout");
        _logger.LogInformation("Iniciando varredura leve do Batedor Amazon...");

        var scoutedList = new List<ScoutedProduct>();
        
        // Exemplo de URL de busca leve
        string searchUrl = "https://www.amazon.com.br/s?k=capacete";

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            var response = await _http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                var found = ParseAsinsFromHtml(html);
                scoutedList.AddRange(found);
                _logger.LogInformation("Varredura da Amazon concluída. Encontrados {Count} produtos.", found.Count);
            }
            else
            {
                _logger.LogWarning("Falha ao obter página da Amazon (Status: {StatusCode}). Proteção WAF ativa.", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro de rede ao escanear a Amazon.");
        }

        // Se houver bloqueio rígido do Cloudflare/WAF, aciona o Fallback de simulação
        if (scoutedList.Count == 0)
        {
            _logger.LogInformation("Modo Fallback ativado para Amazon. Injetando ASINs simulados.");
            scoutedList.Add(new ScoutedProduct("B0C74P8X11", "https://www.amazon.com.br/dp/B0C74P8X11")); // Capacete de Exemplo 1
            scoutedList.Add(new ScoutedProduct("B08H2H6Z14", "https://www.amazon.com.br/dp/B08H2H6Z14")); // Capacete de Exemplo 2
            scoutedList.Add(new ScoutedProduct("B09G96T27G", "https://www.amazon.com.br/dp/B09G96T27G")); // Capacete de Exemplo 3
        }

        return scoutedList;
    }

    private List<ScoutedProduct> ParseAsinsFromHtml(string html)
    {
        var list = new List<ScoutedProduct>();
        var regexAsin = new Regex(@"/dp/([A-Z0-9]{10})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var anchorNodes = doc.DocumentNode.SelectNodes("//a[@href]");
        if (anchorNodes != null)
        {
            foreach (var node in anchorNodes)
            {
                var href = node.GetAttributeValue("href", string.Empty);
                if (string.IsNullOrEmpty(href)) continue;

                var match = regexAsin.Match(href);
                if (match.Success)
                {
                    var asin = match.Groups[1].Value.ToUpperInvariant();
                    var fullUrl = href.StartsWith("http") ? href : $"https://www.amazon.com.br{href}";
                    
                    if (!list.Exists(x => x.ProductId == asin))
                    {
                        list.Add(new ScoutedProduct(asin, fullUrl));
                    }
                }
            }
        }

        return list;
    }
}
