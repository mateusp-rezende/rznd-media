namespace RZND.Bot.Providers;

/// <summary>
/// Representa um produto básico descoberto pelo "Batedor" (WebScraperProvider).
/// </summary>
/// <param name="ProductId">Identificador único do produto na plataforma.</param>
/// <param name="Source">A URL de origem ou o nome do canal/feed de onde o produto foi descoberto.</param>
public record ScoutedProduct(string ProductId, string Source);
