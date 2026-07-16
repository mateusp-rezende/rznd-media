namespace RZND.Bot.Providers;

/// <summary>
/// Configurações para a API oficial de Afiliados da Shopee.
/// </summary>
public sealed class ShopeeApiSettings
{
    /// <summary>
    /// Identificador do aplicativo (AppId) para a plataforma de afiliados.
    /// </summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>
    /// Chave secreta do aplicativo (AppSecret) para assinatura de requisições.
    /// </summary>
    public string AppSecret { get; set; } = string.Empty;

    /// <summary>
    /// URL base da API oficial da Shopee (GraphQL).
    /// </summary>
    public string BaseUrl { get; set; } = "https://open-api.affiliate.shopee.com.br/";

    /// <summary>
    /// Código de rastreamento do parceiro (ex: tag de afiliado).
    /// </summary>
    public string PartnerTag { get; set; } = string.Empty;
}
