namespace RZND.Bot.Providers.Magalu;

/// <summary>
/// Configurações da loja Magazine Luiza (Magazine Você) para geração estática de links.
/// </summary>
public sealed class MagaluApiSettings
{
    /// <summary>
    /// ID ou identificador do usuário da loja no Magazine Você (ex: magazinevoce.com.br/magazineUSUARIO/).
    /// </summary>
    public string StoreId { get; set; } = string.Empty;

    /// <summary>
    /// URL base da loja virtual do Magazine Luiza.
    /// </summary>
    public string BaseUrl { get; set; } = "https://www.magazineluiza.com.br/";
}
