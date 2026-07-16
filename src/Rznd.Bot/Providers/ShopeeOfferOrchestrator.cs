using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RZND.Bot.Scrapers;
using RZND.Bot.Tracing;

namespace RZND.Bot.Providers;

/// <summary>
/// Orquestrador de ofertas da Shopee (ShopeeOfferOrchestrator).
/// Coordena o fluxo "Scout and Fetch" (Batedor e Coletor):
/// 1. Aciona o Batedor (IProductScoutProvider) para pescar os IDs em alta.
/// 2. Transmite os IDs para o Coletor oficial (IProductDetailsFetcher) para obter dados oficiais e links de afiliado.
/// 3. Converte os resultados ricos no formato de domínio unificado (Offer) usado pelo Worker.
/// </summary>
public sealed class ShopeeOfferOrchestrator : IOfferScraper
{
    private readonly IProductScoutProvider _scouter;
    private readonly IProductDetailsFetcher _fetcher;
    private readonly ILogger<ShopeeOfferOrchestrator> _logger;

    /// <summary>
    /// Construtor com injeção de dependências dos componentes de Scout, Fetch e log.
    /// </summary>
    public ShopeeOfferOrchestrator(
        IProductScoutProvider scouter,
        IProductDetailsFetcher fetcher,
        ILogger<ShopeeOfferOrchestrator> logger)
    {
        _scouter = scouter;
        _fetcher = fetcher;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ProviderName => "ShopeeOrchestrator";

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<Offer>> ScrapeAsync(CancellationToken cancellationToken)
    {
        using var _ = Tracer.Start(nameof(ShopeeOfferOrchestrator) + ".Scrape");
        _logger.LogInformation("Iniciando ciclo orquestrado 'Scout and Fetch' para ofertas da Shopee.");

        try
        {
            // 1. Executa o Batedor (WebScraperProvider)
            _logger.LogInformation("Acionando o Batedor (Scouter)...");
            var scoutedProducts = await _scouter.ScoutAsync(cancellationToken);
            
            if (scoutedProducts == null || scoutedProducts.Count == 0)
            {
                _logger.LogWarning("Nenhum produto foi localizado pelo Batedor neste ciclo.");
                return Array.Empty<Offer>();
            }

            _logger.LogInformation("Batedor identificou {Count} produtos em potencial.", scoutedProducts.Count);

            // Obter a lista distinta de IDs encontrados
            var productIds = scoutedProducts
                .Select(p => p.ProductId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            if (productIds.Count == 0)
            {
                _logger.LogWarning("Lista de IDs válidos está vazia após filtragem de duplicidades.");
                return Array.Empty<Offer>();
            }

            // 2. Executa o Coletor Oficial (ShopeeApiProvider)
            _logger.LogInformation("Acionando o Coletor (Fetcher) para os IDs: {Ids}", string.Join(", ", productIds));
            var detailedOffers = await _fetcher.FetchDetailsAsync(productIds, cancellationToken);

            if (detailedOffers == null || detailedOffers.Count == 0)
            {
                _logger.LogWarning("O Coletor Oficial não obteve detalhes para os IDs pesquisados.");
                return Array.Empty<Offer>();
            }

            _logger.LogInformation("Coletor retornou {Count} ofertas válidas e estruturadas.", detailedOffers.Count);

            // 3. Mapeia e consolida para o modelo comum Offer
            var mappedOffers = detailedOffers.Select(d => new Offer(
                Id: d.Id,
                Title: d.Title,
                Price: d.Price,
                ImageUrl: d.ImageUrl,
                SourceUrl: d.SourceUrl,
                AffiliateLink: d.AffiliateLink,
                Provider: d.Provider,
                CapturedAt: d.CapturedAt
            )).ToList();

            _logger.LogInformation("Fluxo orquestrado concluído. {Count} ofertas enviadas para o pipeline do Worker.", mappedOffers.Count);
            return mappedOffers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro crítico de orquestração no fluxo Shopee Scout & Fetch.");
            return Array.Empty<Offer>();
        }
    }
}
