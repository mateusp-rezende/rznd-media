using System;

namespace RZND.Bot.Providers;

/// <summary>
/// Representa as informações detalhadas e validadas de uma oferta obtida via API oficial.
/// </summary>
/// <param name="Id">Identificador único da oferta.</param>
/// <param name="Title">Título/nome oficial do produto.</param>
/// <param name="Price">Preço final atualizado.</param>
/// <param name="ImageUrl">Link para a imagem em alta resolução.</param>
/// <param name="SourceUrl">Link direto para a página oficial do produto.</param>
/// <param name="AffiliateLink">Link curto com o ID de afiliado embutido.</param>
/// <param name="Provider">Nome da plataforma de origem (ex: Shopee).</param>
/// <param name="CapturedAt">Data e hora em que a oferta foi coletada.</param>
/// <param name="CommissionRate">Taxa de comissão garantida (ex: 0.05 para 5%).</param>
/// <param name="Commission">Valor monetário estimado da comissão.</param>
/// <param name="DiscountPct">Percentual de desconto promocional ativo.</param>
/// <param name="OriginalPrice">Preço original sem descontos.</param>
public record OfferDetails(
    string Id,
    string Title,
    decimal Price,
    string ImageUrl,
    string SourceUrl,
    string AffiliateLink,
    string Provider,
    DateTimeOffset CapturedAt,
    decimal CommissionRate,
    decimal Commission,
    decimal DiscountPct,
    decimal OriginalPrice
);
