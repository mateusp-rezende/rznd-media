using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RZND.Bot.Scrapers
{
    /// <summary>
    /// Representa um módulo que extrai ofertas de uma loja.
    /// Cada implementação deve buscar as ofertas disponíveis e
    /// retorná‑las como objetos de domínio (Offer).
    /// </summary>
    public interface IOfferScraper
    {
        /// <summary>
        /// Nome amigável da loja (ex.: "Shopee", "Amazon").
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Executa a captura das ofertas.
        /// </summary>
        /// <param name="cancellationToken">Permite cancelamento gracioso.</param>
        /// <returns>Lista de ofertas encontradas.</returns>
        Task<IReadOnlyCollection<Offer>> ScrapeAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// Modelo de dados que será armazenado no SQLite e publicado.
    /// </summary>
    public record Offer(
        string Id,               // Identificador único (ex.: ASIN, SKU ou hash)
        string Title,
        decimal Price,
        string ImageUrl,
        string SourceUrl,        // URL original do produto
        string AffiliateLink,    // Link curto de afiliado (gerado depois)
        string Provider,         // Nome da loja que forneceu a oferta
        DateTimeOffset CapturedAt);
}
