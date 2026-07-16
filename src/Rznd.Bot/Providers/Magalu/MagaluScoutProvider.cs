using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using HtmlAgilityPack;
using RZND.Bot.Tracing;

namespace RZND.Bot.Providers.Magalu;

/// <summary>
/// Módulo Batedor da Magazine Luiza (MagaluScoutProvider).
/// Escaneia páginas de busca ou categorias da Magalu para capturar os IDs de produtos e SKUs.
/// </summary>
public sealed class MagaluScoutProvider : IProductScoutProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<MagaluScoutProvider> _logger;

    /// <summary>
    /// Construtor com injeção de dependências de rede e logs.
    /// </summary>
    public MagaluScoutProvider(HttpClient http, ILogger<MagaluScoutProvider> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ProviderName => "MagaluWebScraper";

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<ScoutedProduct>> ScoutAsync(CancellationToken cancellationToken)
    {
        using var _ = Tracer.Start(nameof(MagaluScoutProvider) + ".Scout");
        _logger.LogInformation("Iniciando varredura leve do Batedor Magalu...");

        var scoutedList = new List<ScoutedProduct>();
        string searchUrl = "https://www.magazineluiza.com.br/busca/capacete/";

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            var response = await _http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                var found = ParseMagaluIdsFromHtml(html);
                scoutedList.AddRange(found);
                _logger.LogInformation("Varredura da Magalu concluída. Encontrados {Count} produtos.", found.Count);
            }
            else
            {
                _logger.LogWarning("Falha ao obter página da Magalu (Status: {StatusCode}). Proteção WAF ativa.", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro de rede ao escanear a Magazine Luiza.");
        }

        // Se houver bloqueio ou não achar nada, ativa o Fallback de simulação
        if (scoutedList.Count == 0)
        {
            _logger.LogInformation("Modo Fallback ativado para Magalu. Injetando IDs/SKUs simulados.");
            scoutedList.Add(new ScoutedProduct("2371928_fs01", "https://www.magazineluiza.com.br/p/2371928/fs01")); // Capacete de Exemplo 1
            scoutedList.Add(new ScoutedProduct("2839481_wt02", "https://www.magazineluiza.com.br/p/2839481/wt02")); // Smartwatch de Exemplo 2
            scoutedList.Add(new ScoutedProduct("1938475_au03", "https://www.magazineluiza.com.br/p/1938475/au03")); // Fone de Ouvido de Exemplo 3
        }

        return scoutedList;
    }

    private List<ScoutedProduct> ParseMagaluIdsFromHtml(string html)
    {
        var list = new List<ScoutedProduct>();
        
        // Padrão de link da Magalu: /p/{productId}/{sku}/
        var regexMagaluLink = new Regex(@"/p/([a-zA-Z0-9]+)/([a-zA-Z0-9]+)/", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var anchorNodes = doc.DocumentNode.SelectNodes("//a[@href]");
        if (anchorNodes != null)
        {
            foreach (var node in anchorNodes)
            {
                var href = node.GetAttributeValue("href", string.Empty);
                if (string.IsNullOrEmpty(href)) continue;

                var match = regexMagaluLink.Match(href);
                if (match.Success)
                {
                    var productId = match.Groups[1].Value;
                    var sku = match.Groups[2].Value;
                    var compoundId = $"{productId}_{sku}";
                    var fullUrl = href.StartsWith("http") ? href : $"https://www.magazineluiza.com.br{href}";
                    
                    if (!list.Exists(x => x.ProductId == compoundId))
                    {
                        list.Add(new ScoutedProduct(compoundId, fullUrl));
                    }
                }
            }
        }

        return list;
    }
}
