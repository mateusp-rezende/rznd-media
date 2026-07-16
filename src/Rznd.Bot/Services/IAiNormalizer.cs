using System.Threading;
using System.Threading.Tasks;

namespace RZND.Bot.Services;

/// <summary>
/// Interface para normalização de títulos utilizando Inteligência Artificial (Ollama, Claude, Gemini, etc.).
/// </summary>
public interface IAiNormalizer
{
    /// <summary>
    /// Normaliza um título de produto complexo extraindo a essência de Marca, Modelo e Especificações críticas.
    /// </summary>
    /// <param name="complexTitle">O título cru ou parcialmente limpo do produto.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>O título simplificado e ideal para comparação fuzzy.</returns>
    Task<string> NormalizeAsync(string complexTitle, CancellationToken cancellationToken = default);
}
