namespace RZND.Bot.Providers.Amazon;

/// <summary>
/// Configurações do associado Amazon para geração estática de links de afiliado.
/// </summary>
public sealed class AmazonApiSettings
{
    /// <summary>
    /// ID de Associado da Amazon (ex: rzndmedia-20).
    /// </summary>
    public string PartnerTag { get; set; } = string.Empty;

    /// <summary>
    /// URL base da loja Amazon Brasil.
    /// </summary>
    public string BaseUrl { get; set; } = "https://www.amazon.com.br/";
}
