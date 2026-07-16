using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RZND.Bot.Api;
using RZND.Bot.Scrapers;
using RZND.Bot.Services;

namespace RZND.Bot;

/// <summary>
/// Serviço de background que executa ciclos de monitoramento e comparação de preços.
/// </summary>
public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IEnumerable<IOfferScraper> _scrapers;
    private readonly PriceComparisonService _comparisonService;
    private readonly BotStateService _state;
    private readonly CreativeRendererService _renderer;
    private readonly TelegramPublisherService _publisher;

    // Cache local em memória para evitar repostar a mesma oferta no mesmo ciclo
    private readonly HashSet<string> _publishedTitles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Construtor que recebe dependências via DI.
    /// </summary>
    public Worker(
        ILogger<Worker> logger,
        IEnumerable<IOfferScraper> scrapers,
        PriceComparisonService comparisonService,
        BotStateService state,
        CreativeRendererService renderer,
        TelegramPublisherService publisher)
    {
        _logger = logger;
        _scrapers = scrapers;
        _comparisonService = comparisonService;
        _state = state;
        _renderer = renderer;
        _publisher = publisher;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _state.IsScanning = true;
            _logger.LogInformation("--- INICIANDO NOVO CICLO DE MONITORAMENTO ---");

            // 1. Comparação de preços cruzados (Scatter-Gather)
            try
            {
                var clusters = await _comparisonService.ComparePricesAsync(stoppingToken);
                _logger.LogInformation("Comparador finalizou com {Count} grupos identificados.", clusters.Count);

                // Atualiza o estado compartilhado (exposto pela API /api/results)
                _state.UpdateResults(clusters);

                // === FLUXO COMPLETO: Renderizar Anúncio e Publicar Automaticamente ===
                foreach (var cluster in clusters)
                {
                    if (cluster.CheapestOffer == null) continue;

                    // Evita republicar ofertas que já foram postadas nesta execução
                    if (_publishedTitles.Contains(cluster.PrimaryTitle))
                    {
                        _logger.LogInformation("   [Publicador] Produto '{Title}' já divulgado anteriormente. Pulando.", cluster.PrimaryTitle);
                        continue;
                    }

                    _logger.LogInformation("   [Publicador] Novo produto qualificado para anúncio: '{Title}' por R$ {Price:F2}", 
                        cluster.PrimaryTitle, cluster.CheapestOffer.Price);

                    try
                    {
                        // 1. Renderiza o criativo de overlay em formato 1:1
                        var imagePath = await _renderer.RenderCreativeAsync(cluster, "overlay-shopee", stoppingToken);

                        // 2. Calcula métrica de economia para enriquecer a legenda
                        var savingsText = "";
                        if (cluster.Offers.Count > 1)
                        {
                            var maxPrice = cluster.Offers.Max(o => o.Price);
                            if (maxPrice > cluster.CheapestOffer.Price && maxPrice > 0)
                            {
                                var savingsPct = (int)Math.Round(((maxPrice - cluster.CheapestOffer.Price) / maxPrice) * 100);
                                if (savingsPct > 0)
                                {
                                    savingsText = $"({savingsPct}% mais barato que a média!)";
                                }
                            }
                        }

                        // 3. Monta a legenda estruturada em HTML
                        var caption = 
                            $"<b>🔥 OFERTA IMPERDÍVEL</b>\n\n" +
                            $"📦 <b>{cluster.CheapestOffer.Title}</b>\n" +
                            $"💵 Por apenas: <b>R$ {cluster.CheapestOffer.Price.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))}</b>\n" +
                            $"🛍️ Loja: <b>{cluster.CheapestOffer.Provider}</b>\n" +
                            $"{savingsText}\n\n" +
                            $"👉 Garanta o seu no link: <a href=\"{cluster.CheapestOffer.AffiliateLink}\">Clique aqui para Comprar</a>";

                        // 4. Registrar a mídia gerada organizadamente no estado compartilhado
                        var outputId = Path.GetFileNameWithoutExtension(imagePath);
                        _state.RegisterOutput(new GeneratedOutput(
                            outputId,
                            cluster.CheapestOffer.Title,
                            cluster.CheapestOffer.Price.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")),
                            cluster.CheapestOffer.Provider,
                            $"/output/{Path.GetFileName(imagePath)}",
                            caption,
                            DateTimeOffset.UtcNow
                        ));

                        // 5. Dispara a publicação para o canal do Telegram
                        await _publisher.PublishPhotoAsync(imagePath, caption, stoppingToken);

                        // Salva no cache para não repetir
                        _publishedTitles.Add(cluster.PrimaryTitle);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Falha ao gerar/publicar anúncio para o produto '{Title}'.", cluster.PrimaryTitle);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no serviço de comparação de preços.");
            }

            // 2. Scrapers individuais legados
            foreach (var scraper in _scrapers)
            {
                try
                {
                    var ofertas = await scraper.ScrapeAsync(stoppingToken);
                    _logger.LogInformation("{Provider} retornou {Count} ofertas", scraper.ProviderName, ofertas.Count);

                    foreach (var oferta in ofertas)
                    {
                        _logger.LogInformation("   [Scraper Legado] {Title} | Preço: {Price} | Link: {Link}",
                            oferta.Title,
                            oferta.Price.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")),
                            oferta.AffiliateLink);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao rodar scraper legado {Provider}.", scraper.ProviderName);
                }
            }

            // 3. Aguarda 15 minutos OU acorda imediatamente se API solicitar scan
            _state.IsScanning = false;
            _logger.LogInformation("Ciclo concluído. Aguardando próximo scan (15min ou sob demanda)...");
            await _state.WaitForNextScanAsync(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }
}
