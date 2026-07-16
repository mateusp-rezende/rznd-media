using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RZND.Bot.Providers;
using RZND.Bot.Tracing;
using RZND.Bot.Watchlist;

namespace RZND.Bot.Services;

/// <summary>
/// Representa um grupo de ofertas parecidas de diferentes plataformas para um mesmo produto.
/// </summary>
/// <param name="PrimaryTitle">Título mais representativo do produto.</param>
/// <param name="Offers">Lista de ofertas encontradas nas diferentes lojas.</param>
/// <param name="CheapestOffer">A oferta com o menor preço do grupo.</param>
public record ComparedOfferCluster(
    string PrimaryTitle,
    IReadOnlyCollection<OfferDetails> Offers,
    OfferDetails CheapestOffer
);

/// <summary>
/// Serviço de Comparação de Preços entre plataformas (PriceComparisonService).
/// Consome múltiplos batedores (Scouters) e coletores (Fetchers), agrupa ofertas similares 
/// utilizando o algoritmo Scatter-Gather e o ProductMatcherService com validação Fuzzy e regra de colisão.
/// </summary>
public sealed class PriceComparisonService
{
    private readonly IEnumerable<IProductScoutProvider> _scouters;
    private readonly IEnumerable<IProductDetailsFetcher> _fetchers;
    private readonly IProductMatcherService _matcher;
    private readonly WatchlistLoader _watchlist;
    private readonly ILogger<PriceComparisonService> _logger;

    /// <summary>
    /// Construtor que recebe as dependências de DI incluindo o novo matcher e a watchlist.
    /// </summary>
    public PriceComparisonService(
        IEnumerable<IProductScoutProvider> scouters,
        IEnumerable<IProductDetailsFetcher> fetchers,
        IProductMatcherService matcher,
        WatchlistLoader watchlist,
        ILogger<PriceComparisonService> logger)
    {
        _scouters = scouters;
        _fetchers = fetchers;
        _matcher = matcher;
        _watchlist = watchlist;
        _logger = logger;
    }

    /// <summary>
    /// Varre todas as plataformas, puxa dados de produtos e faz o agrupamento comparativo usando a estratégia Scatter-Gather.
    /// </summary>
    public async Task<IReadOnlyCollection<ComparedOfferCluster>> ComparePricesAsync(CancellationToken cancellationToken)
    {
        using var _ = Tracer.Start(nameof(PriceComparisonService) + ".ComparePrices");
        _logger.LogInformation("Iniciando Comparador Scatter-Gather de Preços em Tempo Real...");

        var searchables = _fetchers.OfType<ISearchableProvider>().ToList();
        var clusters = new List<ComparedOfferCluster>();

        var mode = _watchlist.Current?.Mode ?? "both";
        var activeScouters = _scouters.Where(s =>
        {
            if (mode == "trend") return s.ProviderName != "WatchlistScout";
            if (mode == "watchlist") return s.ProviderName == "WatchlistScout";
            return true; // "both"
        }).ToList();

        _logger.LogInformation("Modo ativo do comparador: {Mode}. Executando {Count} batedores semente.", mode, activeScouters.Count);

        // 1. Fase de Captação Semente (Scout Inicial)
        var seedOffers = new List<OfferDetails>();
        foreach (var scouter in activeScouters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            try
            {
                if (scouter.ProviderName == "WatchlistScout")
                {
                    _logger.LogInformation("Batedor especial 'WatchlistScout' coletando ofertas semente...");
                    var scouted = await scouter.ScoutAsync(cancellationToken);
                    if (scouted == null || scouted.Count == 0) continue;

                    foreach (var s in scouted)
                    {
                        IProductDetailsFetcher innerFetcher = null;
                        if (s.ProductId.StartsWith("shopee", StringComparison.OrdinalIgnoreCase))
                        {
                            innerFetcher = _fetchers.FirstOrDefault(f => f.ProviderName == "ShopeeApi");
                        }
                        else if (s.ProductId.Contains("_"))
                        {
                            innerFetcher = _fetchers.FirstOrDefault(f => f.ProviderName == "MagaluApi");
                        }
                        else
                        {
                            innerFetcher = _fetchers.FirstOrDefault(f => f.ProviderName == "AmazonApi");
                        }

                        if (innerFetcher != null)
                        {
                            var details = await innerFetcher.FetchDetailsAsync(new[] { s.ProductId }, cancellationToken);
                            if (details != null && details.Count > 0)
                            {
                                seedOffers.AddRange(details);
                            }
                        }
                    }
                    continue;
                }

                string targetProvider = scouter.ProviderName.Replace("WebScraper", "Api");
                var fetcher = _fetchers.FirstOrDefault(f => f.ProviderName == targetProvider);
                
                if (fetcher == null) continue;

                _logger.LogInformation("Batedor '{Scouter}' coletando ofertas semente...", scouter.ProviderName);
                var scoutedNormal = await scouter.ScoutAsync(cancellationToken);
                if (scoutedNormal == null || scoutedNormal.Count == 0) continue;

                var ids = scoutedNormal.Select(s => s.ProductId).Distinct().ToList();
                var detailsNormal = await fetcher.FetchDetailsAsync(ids, cancellationToken);
                
                if (detailsNormal != null)
                {
                    seedOffers.AddRange(detailsNormal);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter sementes do batedor {Scouter}", scouter.ProviderName);
            }
        }

        if (seedOffers.Count == 0)
        {
            _logger.LogWarning("Nenhum produto semente coletado de nenhuma plataforma.");
            return Array.Empty<ComparedOfferCluster>();
        }

        // Remover sementes duplicadas ou equivalentes na lista de partida de forma assíncrona
        var uniqueSeeds = new List<OfferDetails>();
        foreach (var seed in seedOffers)
        {
            bool hasMatch = false;
            foreach (var s in uniqueSeeds)
            {
                if (s.Provider == seed.Provider)
                {
                    var result = await _matcher.CompareOffersAsync(s, seed, cancellationToken);
                    if (result.IsSameProduct)
                    {
                        hasMatch = true;
                        break;
                    }
                }
            }
            if (!hasMatch)
            {
                uniqueSeeds.Add(seed);
            }
        }

        _logger.LogInformation("Fase Scatter-Gather: Processando {Count} produtos semente únicos...", uniqueSeeds.Count);

        // 2. Fase de Dispersão e Busca (Scatter-Gather) em Tempo Real
        foreach (var seed in uniqueSeeds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogInformation("Produto Semente: '{Title}' ({Provider} - R$ {Price})", seed.Title, seed.Provider, seed.Price);

            var groupOffers = new List<OfferDetails> { seed };

            // Criar a query de busca a partir do título limpo
            string searchQuery = await _matcher.NormalizeTitleAsync(seed.Title, cancellationToken);

            // Disparar busca paralela nos outros provedores compatíveis com pesquisa
            var tasks = searchables
                .Where(f => f.ProviderName != $"{seed.Provider}Api") // Não pesquisa na mesma loja da semente
                .Select(async provider =>
                {
                    try
                    {
                        _logger.LogInformation("   [Scatter] Pesquisando '{Query}' no provedor {Provider}...", searchQuery, provider.ProviderName);
                        var match = await provider.SearchBestMatchAsync(searchQuery, cancellationToken);

                        if (match != null)
                        {
                            // Validação rigorosa usando FuzzySharp e gabarito de colisão de modelos
                            var result = await _matcher.CompareOffersAsync(seed, match, cancellationToken);
                            if (result.IsSameProduct)
                            {
                                _logger.LogInformation("   [Match] Encontrado compatível na {Provider}: '{Title}' por R$ {Price}", provider.ProviderName, match.Title, match.Price);
                                lock (groupOffers)
                                {
                                    groupOffers.Add(match);
                                }
                            }
                            else
                            {
                                _logger.LogWarning("   [Divergente] Candidato ignorado: '{SeedTitle}' vs '{MatchTitle}' (Score: {Score}%, Detalhes: {Details})", 
                                    seed.Title, match.Title, result.ConfidenceScore, result.Details);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Erro ao buscar correspondência no provedor {Provider}", provider.ProviderName);
                    }
                });

            await Task.WhenAll(tasks);

            // Ordena o grupo por preço e define o mais barato
            var cheapest = groupOffers.OrderBy(o => o.Price).First();
            clusters.Add(new ComparedOfferCluster(seed.Title, groupOffers, cheapest));
        }

        // 3. Imprime o relatório final
        PrintComparisonReport(clusters);

        return clusters;
    }

    private void PrintComparisonReport(IReadOnlyCollection<ComparedOfferCluster> clusters)
    {
        _logger.LogInformation("=================================================================================");
        _logger.LogInformation("        RELATÓRIO DE COMPARAÇÃO SCATTER-GATHER (FUZZYSHARP + GABARITO)         ");
        _logger.LogInformation("=================================================================================");

        foreach (var cluster in clusters)
        {
            _logger.LogInformation("PRODUTO SEMENTE: {Title}", cluster.PrimaryTitle);
            
            foreach (var offer in cluster.Offers)
            {
                var isCheapest = offer.Id == cluster.CheapestOffer.Id && offer.Provider == cluster.CheapestOffer.Provider;
                var marker = isCheapest ? "★ [MAIS BARATO]" : "  ";
                
                _logger.LogInformation("  {Marker} {Provider}: R$ {Price:F2} (Link: {Link})", 
                    marker, 
                    offer.Provider.PadRight(8), 
                    offer.Price, 
                    offer.AffiliateLink);
            }
            _logger.LogInformation("---------------------------------------------------------------------------------");
        }
        _logger.LogInformation("=================================================================================");
    }
}
