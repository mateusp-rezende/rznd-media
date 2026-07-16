using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RZND.Bot.Watchlist;

/// <summary>
/// Representa uma entrada na watchlist do usuário — um produto ou keyword a ser monitorado.
/// </summary>
public record WatchlistEntry(
    [property: JsonPropertyName("keyword")] string Keyword,
    [property: JsonPropertyName("maxPrice")] decimal? MaxPrice,
    [property: JsonPropertyName("enabled")] bool Enabled
);

/// <summary>
/// Configuração completa da watchlist lida do arquivo watchlist.json.
/// </summary>
public record WatchlistConfig(
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("entries")] IReadOnlyList<WatchlistEntry> Entries
)
{
    /// <summary>
    /// Retorna apenas as entradas ativas (enabled = true).
    /// </summary>
    public IEnumerable<WatchlistEntry> ActiveEntries
    {
        get
        {
            foreach (var entry in Entries)
            {
                if (entry.Enabled)
                    yield return entry;
            }
        }
    }

    /// <summary>
    /// Se true, o modo Trend Scout (automático) está habilitado.
    /// </summary>
    public bool TrendEnabled => Mode is "trend" or "both";

    /// <summary>
    /// Se true, o modo Watchlist (curado pelo usuário) está habilitado.
    /// </summary>
    public bool WatchlistEnabled => Mode is "watchlist" or "both";
}
