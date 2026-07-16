using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RZND.Bot.Tracing;
using RZND.Bot.Watchlist;

namespace RZND.Bot.Providers;

/// <summary>
/// Scout guiado pelo usuário (WatchlistScoutProvider).
/// Lê a watchlist.json e usa os provedores pesquisáveis (ISearchableProvider)
/// para encontrar a melhor oferta de cada keyword em todas as plataformas.
/// Alimenta o mesmo pipeline Scatter-Gather do PriceComparisonService.
/// </summary>
public sealed class WatchlistScoutProvider : IProductScoutProvider
{
    private readonly WatchlistLoader _watchlist;
    private readonly IEnumerable<ISearchableProvider> _searchables;
    private readonly ILogger<WatchlistScoutProvider> _logger;

    public WatchlistScoutProvider(
        WatchlistLoader watchlist,
        IEnumerable<ISearchableProvider> searchables,
        ILogger<WatchlistScoutProvider> logger)
    {
        _watchlist = watchlist;
        _searchables = searchables;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ProviderName => "WatchlistScout";

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<ScoutedProduct>> ScoutAsync(CancellationToken cancellationToken)
    {
        using var _ = Tracer.Start(nameof(WatchlistScoutProvider) + ".Scout");

        var config = _watchlist.Current;
        var activeEntries = config.ActiveEntries.ToList();

        if (activeEntries.Count == 0)
        {
            _logger.LogInformation("[Watchlist] Nenhuma entrada ativa na watchlist. Verifique o watchlist.json.");
            return Array.Empty<ScoutedProduct>();
        }

        _logger.LogInformation("[Watchlist] Processando {Count} entradas ativas...", activeEntries.Count);

        var results = new List<ScoutedProduct>();

        foreach (var entry in activeEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogInformation("[Watchlist] Buscando '{Keyword}' em {ProviderCount} plataformas...",
                entry.Keyword, _searchables.Count());

            // Busca em paralelo em todas as plataformas configuradas
            var tasks = _searchables.Select(async provider =>
            {
                try
                {
                    var offer = await provider.SearchBestMatchAsync(entry.Keyword, cancellationToken);

                    if (offer == null)
                    {
                        _logger.LogDebug("[Watchlist] '{Keyword}' não encontrado em {Provider}.", entry.Keyword, provider.ProviderName);
                        return null;
                    }

                    // Filtro de preço máximo (se configurado)
                    if (entry.MaxPrice.HasValue && offer.Price > entry.MaxPrice.Value)
                    {
                        _logger.LogInformation("[Watchlist] '{Title}' ignorado em {Provider}: R$ {Price:F2} > limite R$ {Max:F2}.",
                            offer.Title, provider.ProviderName, offer.Price, entry.MaxPrice.Value);
                        return null;
                    }

                    _logger.LogInformation("[Watchlist] ✓ Encontrado: '{Title}' em {Provider} por R$ {Price:F2}.",
                        offer.Title, provider.ProviderName, offer.Price);

                    return new ScoutedProduct(offer.Id, offer.AffiliateLink ?? offer.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Watchlist] Erro ao buscar '{Keyword}' em {Provider}.", entry.Keyword, provider.ProviderName);
                    return null;
                }
            });

            var found = await Task.WhenAll(tasks);

            foreach (var product in found)
            {
                if (product != null && !results.Any(r => r.ProductId == product.ProductId))
                {
                    results.Add(product);
                }
            }
        }

        _logger.LogInformation("[Watchlist] Scout concluído. {Count} produtos únicos encontrados para comparação.", results.Count);
        return results;
    }
}
