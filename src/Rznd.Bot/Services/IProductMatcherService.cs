using System.Threading;
using System.Threading.Tasks;
using RZND.Bot.Providers;

namespace RZND.Bot.Services;

/// <summary>
/// DTO que representa o resultado do cruzamento/comparação entre duas ofertas.
/// </summary>
/// <param name="IsSameProduct">Identifica se o motor considerou as ofertas como sendo do mesmo produto.</param>
/// <param name="ConfidenceScore">Pontuação de confiança calculada (0 a 100).</param>
/// <param name="Details">Informações detalhadas sobre a decisão de correspondência.</param>
/// <param name="PriceDifference">A diferença absoluta de preços se for o mesmo produto, ou null caso contrário.</param>
public record ComparisonResult(
    bool IsSameProduct,
    int ConfidenceScore,
    string Details,
    decimal? PriceDifference = null
);

/// <summary>
/// Contrato para o serviço de comparação e correspondência de ofertas.
/// </summary>
public interface IProductMatcherService
{
    /// <summary>
    /// Compara duas ofertas para identificar se correspondem ao mesmo produto físico.
    /// </summary>
    Task<ComparisonResult> CompareOffersAsync(OfferDetails a, OfferDetails b, CancellationToken ct);

    /// <summary>
    /// Limpa ruídos promocionais e padroniza o título do produto.
    /// </summary>
    Task<string> NormalizeTitleAsync(string rawTitle, CancellationToken ct);
}
