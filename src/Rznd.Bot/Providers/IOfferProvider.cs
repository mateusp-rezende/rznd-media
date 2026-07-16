using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RZND.Bot.Providers;

/// <summary>
/// Interface base de modularidade para fontes de dados do projeto.
/// </summary>
public interface IOfferProvider
{
    /// <summary>
    /// Nome identificador do provedor.
    /// </summary>
    string ProviderName { get; }
}

/// <summary>
/// Contrato do módulo "Batedor" (Scouter).
/// Responsável por varrer fontes de dados baratas e extrair chaves de produtos.
/// </summary>
public interface IProductScoutProvider : IOfferProvider
{
    /// <summary>
    /// Realiza a busca inicial de produtos populares, retornando apenas informações superficiais (ID e URL original).
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento da operação.</param>
    /// <returns>Coleção de produtos "pescados".</returns>
    Task<IReadOnlyCollection<ScoutedProduct>> ScoutAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Contrato do módulo "Coletor" (Fetcher).
/// Responsável por consultar endpoints oficiais usando chaves legítimas e obter payloads estruturados.
/// </summary>
public interface IProductDetailsFetcher : IOfferProvider
{
    /// <summary>
    /// Acessa a API oficial e recolhe metadados ricos (imagem, preço, percentual de comissão) baseados no ID.
    /// </summary>
    /// <param name="productIds">Coleção de IDs a serem consultados.</param>
    /// <param name="cancellationToken">Token de cancelamento da operação.</param>
    /// <returns>Coleção de ofertas detalhadas e formatadas.</returns>
    Task<IReadOnlyCollection<OfferDetails>> FetchDetailsAsync(IEnumerable<string> productIds, CancellationToken cancellationToken);
}

/// <summary>
/// Contrato para provedores capazes de pesquisar em tempo real por palavras-chave (Scatter-Gather).
/// </summary>
public interface ISearchableProvider : IOfferProvider
{
    /// <summary>
    /// Pesquisa e retorna a melhor oferta correspondente a uma busca textual.
    /// </summary>
    /// <param name="query">Termo ou modelo de produto limpo para busca.</param>
    /// <param name="cancellationToken">Token de cancelamento da operação.</param>
    /// <returns>A melhor oferta correspondente ou nulo se não encontrada.</returns>
    Task<OfferDetails?> SearchBestMatchAsync(string query, CancellationToken cancellationToken);
}
