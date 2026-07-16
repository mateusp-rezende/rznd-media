using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RZND.Bot.Tracing;

namespace RZND.Bot.Providers;

/// <summary>
/// Coletor oficial (ShopeeApiProvider).
/// Consome a API oficial de afiliados da Shopee via GraphQL, com validação criptográfica (HMAC-SHA256)
/// e controle de limites.
/// </summary>
public sealed class ShopeeApiProvider : IProductDetailsFetcher, ISearchableProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<ShopeeApiProvider> _logger;
    private readonly ShopeeApiSettings _settings;

    /// <summary>
    /// Construtor que recebe injeção das configurações e serviços de rede/logging.
    /// </summary>
    public ShopeeApiProvider(HttpClient http, ILogger<ShopeeApiProvider> logger, IOptions<ShopeeApiSettings> settings)
    {
        _http = http;
        _logger = logger;
        _settings = settings.Value;
    }

    /// <inheritdoc />
    public string ProviderName => "ShopeeApi";

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<OfferDetails>> FetchDetailsAsync(IEnumerable<string> productIds, CancellationToken cancellationToken)
    {
        using var _ = Tracer.Start(nameof(ShopeeApiProvider) + ".FetchDetails");
        
        var idList = new List<string>(productIds);
        _logger.LogInformation("Iniciando coleta de detalhes via API oficial para {Count} produtos...", idList.Count);

        if (idList.Count == 0)
        {
            return Array.Empty<OfferDetails>();
        }

        // Se o desenvolvedor não forneceu chaves de API reais, roda em Modo Simulado/Fallback
        if (string.IsNullOrWhiteSpace(_settings.AppId) || string.IsNullOrWhiteSpace(_settings.AppSecret))
        {
            _logger.LogWarning("Chaves de API da Shopee não configuradas. Retornando dados simulados legítimos para o ciclo.");
            return FetchSimulatedDetails(idList);
        }

        var results = new List<OfferDetails>();

        try
        {
            // GraphQL Query para buscar dados do catálogo de produtos
            var query = @"
            query getProductDetails($itemIds: [String!]!) {
                productDetails(itemIds: $itemIds) {
                    itemId
                    title
                    price
                    imageUrl
                    sourceUrl
                    discountPct
                    originalPrice
                    commissionRate
                    commission
                }
            }";

            var payloadObj = new
            {
                query = query,
                variables = new
                {
                    itemIds = idList
                }
            };

            // Serialização sem indentação para consistência na formação da assinatura digital
            var jsonPayload = JsonSerializer.Serialize(payloadObj, new JsonSerializerOptions
            {
                WriteIndented = false
            });

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var signature = CalculateSignature(_settings.AppId, _settings.AppSecret, timestamp, jsonPayload);

            var request = new HttpRequestMessage(HttpMethod.Post, _settings.BaseUrl);
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            request.Headers.Add("Authorization", $"SHA256 AppId={_settings.AppId}, Timestamp={timestamp}, Signature={signature}");

            _logger.LogInformation("Despachando consulta GraphQL para o catálogo oficial da Shopee...");
            var response = await _http.SendAsync(request, cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Detecção de Rate Limit (HTTP 429) no provedor da Shopee.");
                throw new HttpRequestException("Rate Limit excedido (HTTP 429).");
            }

            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("errors", out var errorsProp))
            {
                _logger.LogError("Erro do servidor GraphQL da Shopee: {Errors}", errorsProp.ToString());
                return results;
            }

            if (root.TryGetProperty("data", out var dataProp) && dataProp.TryGetProperty("productDetails", out var detailsArray))
            {
                foreach (var item in detailsArray.EnumerateArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var itemId = item.GetProperty("itemId").GetString() ?? string.Empty;
                    var title = item.GetProperty("title").GetString() ?? string.Empty;
                    var price = item.GetProperty("price").GetDecimal();
                    var imageUrl = item.GetProperty("imageUrl").GetString() ?? string.Empty;
                    var sourceUrl = item.GetProperty("sourceUrl").GetString() ?? string.Empty;
                    var discountPct = item.GetProperty("discountPct").GetDecimal();
                    var originalPrice = item.GetProperty("originalPrice").GetDecimal();
                    var commissionRate = item.GetProperty("commissionRate").GetDecimal();
                    var commission = item.GetProperty("commission").GetDecimal();

                    // Gerar o link curto de afiliado
                    var affiliateLink = await GenerateAffiliateLinkAsync(sourceUrl, cancellationToken);

                    results.Add(new OfferDetails(
                        Id: itemId,
                        Title: title,
                        Price: price,
                        ImageUrl: imageUrl,
                        SourceUrl: sourceUrl,
                        AffiliateLink: affiliateLink,
                        Provider: "Shopee",
                        CapturedAt: DateTimeOffset.UtcNow,
                        CommissionRate: commissionRate,
                        Commission: commission,
                        DiscountPct: discountPct,
                        OriginalPrice: originalPrice
                    ));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao obter dados detalhados via API da Shopee.");
        }

        return results;
    }

    /// <summary>
    /// Gera o link curto de afiliado via mutação da API da Shopee.
    /// </summary>
    private async Task<string> GenerateAffiliateLinkAsync(string originalUrl, CancellationToken cancellationToken)
    {
        try
        {
            var mutation = @"
            mutation ($urls: [String!]!, $subIds: [String!]) {
                generateBatchShortLink(originLinks: $urls, subIds: $subIds) {
                    shortLink
                    originLink
                }
            }";

            var payloadObj = new
            {
                query = mutation,
                variables = new
                {
                    urls = new[] { originalUrl },
                    subIds = new[] { "telegram", "local_bot" }
                }
            };

            var jsonPayload = JsonSerializer.Serialize(payloadObj);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var signature = CalculateSignature(_settings.AppId, _settings.AppSecret, timestamp, jsonPayload);

            var request = new HttpRequestMessage(HttpMethod.Post, _settings.BaseUrl);
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            request.Headers.Add("Authorization", $"SHA256 AppId={_settings.AppId}, Timestamp={timestamp}, Signature={signature}");

            var response = await _http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.TryGetProperty("data", out var dataProp) && 
                    dataProp.TryGetProperty("generateBatchShortLink", out var linksArray) && 
                    linksArray.ValueKind == JsonValueKind.Array &&
                    linksArray.GetArrayLength() > 0)
                {
                    return linksArray[0].GetProperty("shortLink").GetString() ?? originalUrl;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao gerar link curto de afiliado via API. Retornando URL original.");
        }

        return originalUrl;
    }

    /// <summary>
    /// Realiza a computação HMAC-SHA256 da assinatura de cabeçalho da Shopee.
    /// </summary>
    private string CalculateSignature(string appId, string appSecret, long timestamp, string payload)
    {
        var rawData = $"{appId}{timestamp}{payload}{appSecret}";
        var keyBytes = Encoding.UTF8.GetBytes(appSecret);
        var dataBytes = Encoding.UTF8.GetBytes(rawData);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Geração de dados de catálogo simulados para fins de teste sem credenciais de produção.
    /// </summary>
    private IReadOnlyCollection<OfferDetails> FetchSimulatedDetails(IEnumerable<string> productIds)
    {
        var list = new List<OfferDetails>();
        var random = new Random();

        foreach (var id in productIds)
        {
            decimal originalPrice = random.Next(150, 600);
            decimal discountPct = random.Next(15, 60);
            decimal price = originalPrice * (1 - (discountPct / 100m));
            decimal commissionRate = 0.08m; // comissão de 8%
            decimal commission = price * commissionRate;

            list.Add(new OfferDetails(
                Id: id,
                Title: $"[Oficial] Produto Shopee ID {id} - Com Desconto e Comissão",
                Price: Math.Round(price, 2),
                ImageUrl: "https://down-br.img.sg.susercontent.com/file/cfba54c87c02b28c5a21efc0a1a0db27",
                SourceUrl: $"https://shopee.com.br/product/12345/{id}",
                AffiliateLink: $"https://s.shopee.com.br/simulated_{id}",
                Provider: "Shopee",
                CapturedAt: DateTimeOffset.UtcNow,
                CommissionRate: commissionRate,
                Commission: Math.Round(commission, 2),
                DiscountPct: discountPct,
                OriginalPrice: Math.Round(originalPrice, 2)
            ));
        }

        return list;
    }

    /// <inheritdoc />
    public async Task<OfferDetails?> SearchBestMatchAsync(string query, CancellationToken cancellationToken)
    {
        using var _ = Tracer.Start(nameof(ShopeeApiProvider) + ".SearchBestMatch");
        _logger.LogInformation("Shopee API: Pesquisando melhor correspondência para '{Query}'...", query);

        if (string.IsNullOrWhiteSpace(_settings.AppId) || string.IsNullOrWhiteSpace(_settings.AppSecret))
        {
            _logger.LogInformation("Modo Simulação ativo na Shopee. Gerando correspondência simulada.");
            return CreateSimulatedSearchMatch(query);
        }

        try
        {
            var graphQuery = @"
            query searchProduct($keyword: String!) {
                searchProduct(keyword: $keyword, limit: 1) {
                    itemId
                    title
                    price
                    imageUrl
                    sourceUrl
                    discountPct
                    originalPrice
                    commissionRate
                    commission
                }
            }";

            var payloadObj = new
            {
                query = graphQuery,
                variables = new { keyword = query }
            };

            var jsonPayload = JsonSerializer.Serialize(payloadObj);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var signature = CalculateSignature(_settings.AppId, _settings.AppSecret, timestamp, jsonPayload);

            var request = new HttpRequestMessage(HttpMethod.Post, _settings.BaseUrl);
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            request.Headers.Add("Authorization", $"SHA256 AppId={_settings.AppId}, Timestamp={timestamp}, Signature={signature}");

            var response = await _http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.TryGetProperty("data", out var dataProp) && 
                    dataProp.TryGetProperty("searchProduct", out var productArray) && 
                    productArray.ValueKind == JsonValueKind.Array && 
                    productArray.GetArrayLength() > 0)
                {
                    var item = productArray[0];
                    var itemId = item.GetProperty("itemId").GetString() ?? string.Empty;
                    var title = item.GetProperty("title").GetString() ?? string.Empty;
                    var price = item.GetProperty("price").GetDecimal();
                    var imageUrl = item.GetProperty("imageUrl").GetString() ?? string.Empty;
                    var sourceUrl = item.GetProperty("sourceUrl").GetString() ?? string.Empty;
                    var discountPct = item.GetProperty("discountPct").GetDecimal();
                    var originalPrice = item.GetProperty("originalPrice").GetDecimal();
                    var commissionRate = item.GetProperty("commissionRate").GetDecimal();
                    var commission = item.GetProperty("commission").GetDecimal();

                    var affiliateLink = await GenerateAffiliateLinkAsync(sourceUrl, cancellationToken);

                    return new OfferDetails(
                        Id: itemId,
                        Title: title,
                        Price: price,
                        ImageUrl: imageUrl,
                        SourceUrl: sourceUrl,
                        AffiliateLink: affiliateLink,
                        Provider: "Shopee",
                        CapturedAt: DateTimeOffset.UtcNow,
                        CommissionRate: commissionRate,
                        Commission: commission,
                        DiscountPct: discountPct,
                        OriginalPrice: originalPrice
                    );
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao pesquisar produto via API Shopee.");
        }

        return null;
    }

    private OfferDetails CreateSimulatedSearchMatch(string query)
    {
        var random = new Random(query.GetHashCode());
        decimal originalPrice = random.Next(150, 500);
        decimal discountPct = random.Next(10, 45);
        decimal price = originalPrice * (1 - (discountPct / 100m));
        
        // Simular que a Shopee é ligeiramente mais barata
        price = price * 0.92m;

        decimal commissionRate = 0.08m;
        decimal commission = price * commissionRate;

        string matchedTitle = query;
        if (query.Contains("capacete", StringComparison.OrdinalIgnoreCase))
        {
            matchedTitle = "Capacete Articulado LS2 FF320 Stream Matt Black";
        }
        else if (query.Contains("smartwatch", StringComparison.OrdinalIgnoreCase) || query.Contains("relogio", StringComparison.OrdinalIgnoreCase))
        {
            matchedTitle = "Relógio Inteligente Smartwatch Sport Amoled Tela 1.43";
        }
        else if (query.Contains("fone", StringComparison.OrdinalIgnoreCase))
        {
            matchedTitle = "Fone de Ouvido Bluetooth Soundflow Bass";
        }

        return new OfferDetails(
            Id: $"shopee_search_{Math.Abs(query.GetHashCode())}",
            Title: matchedTitle,
            Price: Math.Round(price, 2),
            ImageUrl: "https://down-br.img.sg.susercontent.com/file/cfba54c87c02b28c5a21efc0a1a0db27",
            SourceUrl: $"https://shopee.com.br/product-search-match",
            AffiliateLink: $"https://s.shopee.com.br/simulated_search_{Math.Abs(query.GetHashCode())}",
            Provider: "Shopee",
            CapturedAt: DateTimeOffset.UtcNow,
            CommissionRate: commissionRate,
            Commission: Math.Round(commission, 2),
            DiscountPct: discountPct,
            OriginalPrice: Math.Round(originalPrice, 2)
        );
    }
}
