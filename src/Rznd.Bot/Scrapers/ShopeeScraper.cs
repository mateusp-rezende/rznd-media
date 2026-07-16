using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using RZND.Bot.Tracing;

namespace RZND.Bot.Scrapers;

/// <summary>
/// Scraper que coleta ofertas da Shopee usando Playwright com Evasão Stealth Completa e Chrome Real.
/// </summary>
public sealed class ShopeeScraper : IOfferScraper
{
    private readonly HttpClient _http;
    private readonly ILogger<ShopeeScraper> _logger;

    public ShopeeScraper(HttpClient http, ILogger<ShopeeScraper> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ProviderName { get; } = "Shopee";

    /// <summary>
    /// Executa a coleta de ofertas.
    /// </summary>
    public async Task<IReadOnlyCollection<Offer>> ScrapeAsync(CancellationToken cancellationToken)
    {
        using var _ = Tracer.Start(nameof(ShopeeScraper) + ".Scrape");

        _logger.LogInformation(
            "Iniciando coleta de ofertas com Playwright em modo HEADFUL com Evasão Stealth e Chrome Real. TraceId={TraceId}",
            System.Diagnostics.Activity.Current?.Id);

        var offers = new List<Offer>();

        try
        {
            using var playwright = await Playwright.CreateAsync();
            
            // Tentar localizar o executável do Google Chrome real do usuário
            string chromePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
            if (!System.IO.File.Exists(chromePath))
            {
                chromePath = @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe";
            }
            if (!System.IO.File.Exists(chromePath))
            {
                chromePath = null;
            }

            var launchOptions = new BrowserTypeLaunchOptions
            {
                Headless = false,
                Args = new[]
                {
                    "--disable-blink-features=AutomationControlled",
                    "--disable-infobars",
                    "--no-sandbox",
                    "--disable-web-security"
                }
            };

            if (chromePath != null)
            {
                launchOptions.ExecutablePath = chromePath;
                _logger.LogInformation("Executável do Google Chrome real localizado em: {Path}", chromePath);
            }
            else
            {
                _logger.LogWarning("Google Chrome real não localizado. Usando Chromium padrão do Playwright.");
            }

            await using var browser = await playwright.Chromium.LaunchAsync(launchOptions);

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36",
                Locale = "pt-BR",
                ViewportSize = new ViewportSize { Width = 1280, Height = 800 }
            });

            // Injetar script de evasão de detecção (Stealth Completo)
            await context.AddInitScriptAsync(@"
                Object.defineProperty(navigator, 'webdriver', {
                    get: () => undefined
                });

                window.chrome = {
                    runtime: {},
                    loadTimes: function() {},
                    csi: function() {},
                    app: {}
                };

                Object.defineProperty(navigator, 'plugins', {
                    get: () => [1, 2, 3, 4, 5]
                });

                Object.defineProperty(navigator, 'languages', {
                    get: () => ['pt-BR', 'pt', 'en-US', 'en']
                });

                const mockWebGL = (proto) => {
                    if (!proto) return;
                    const getParameter = proto.getParameter;
                    proto.getParameter = function(parameter) {
                        if (parameter === 37445) return 'Intel Inc.';
                        if (parameter === 37446) return 'Intel(R) UHD Graphics 620';
                        return getParameter.apply(this, arguments);
                    };
                };
                mockWebGL(WebGLRenderingContext.prototype);
                if (window.WebGL2RenderingContext) {
                    mockWebGL(WebGL2RenderingContext.prototype);
                }

                const originalQuery = navigator.permissions.query;
                navigator.permissions.query = (parameters) => 
                    parameters.name === 'notifications' ? 
                        Promise.resolve({ state: Notification.permission }) : 
                        originalQuery(parameters);
            ");

            var page = await context.NewPageAsync();

            // Interceptar a API de busca
            var apiResponseTcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);

            page.Response += async (_, response) =>
            {
                if (response.Url.Contains("/api/v4/search/search_items"))
                {
                    _logger.LogInformation("API de busca detectada: {Url} (Status: {Status})", response.Url, response.Status);
                    try
                    {
                        var body = await response.BodyAsync();
                        string jsonPath = @"C:\Users\MateusRezende\.gemini\antigravity-ide\brain\c2dbcdba-de2d-41f3-8d7f-4f9fbf643918\shopee_search_response.json";
                        await System.IO.File.WriteAllBytesAsync(jsonPath, body);
                        
                        using var doc = JsonDocument.Parse(body);
                        var root = doc.RootElement.Clone();
                        
                        if (root.TryGetProperty("error", out var errorProp) && errorProp.GetInt32() == 90309999)
                        {
                            _logger.LogWarning("Recebido erro 90309999 de detecção de robô.");
                        }
                        else
                        {
                            apiResponseTcs.TrySetResult(root);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Falha ao analisar o JSON da resposta da busca do Shopee.");
                    }
                }
            };

            // 1. Navegar para a Home Page do Shopee
            _logger.LogInformation("Navegando até a home page do Shopee...");
            await page.GotoAsync("https://shopee.com.br/", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 60000
            });

            // 2. Tratar modal de idioma se aparecer
            try
            {
                var langBtn = page.Locator("text=Português (BR)");
                _logger.LogInformation("Aguardando modal de idioma aparecer...");
                await langBtn.First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
                _logger.LogInformation("Clicando no botão de idioma Português (BR)...");
                await langBtn.First.ClickAsync();
                _logger.LogInformation("Aguardando 5 segundos para a página recarregar...");
                await page.WaitForTimeoutAsync(5000);
            }
            catch (Exception ex)
            {
                _logger.LogInformation("Modal de idioma não apareceu ou erro: {Msg}", ex.Message);
            }

            // 3. Tratar banner de cookies se aparecer
            try
            {
                var cookieBtn = page.Locator("text=Aceitar todos os cookies");
                if (await cookieBtn.CountAsync() > 0 && await cookieBtn.First.IsVisibleAsync())
                {
                    _logger.LogInformation("Clicando no banner de aceitar cookies...");
                    await cookieBtn.First.ClickAsync();
                    await page.WaitForTimeoutAsync(1000);
                }
            }
            catch (Exception)
            {
            }

            // 4. Localizar a barra de pesquisa
            _logger.LogInformation("Localizando a barra de pesquisa...");
            ILocator searchInput = null;
            var selectors = new[] { "input.shopee-searchbar-input__input", "input[type='search']", "input[placeholder*='Buscar']" };
            foreach (var sel in selectors)
            {
                try
                {
                    var loc = page.Locator(sel);
                    await loc.First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
                    searchInput = loc.First;
                    _logger.LogInformation("Barra de pesquisa encontrada: {Selector}", sel);
                    break;
                }
                catch
                {
                }
            }

            if (searchInput == null)
            {
                _logger.LogWarning("Não foi possível encontrar a barra de pesquisa. Indo direto para busca.");
                await page.GotoAsync("https://shopee.com.br/search?keyword=capacete", new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.NetworkIdle
                });
            }
            else
            {
                await searchInput.ClickAsync();
                await page.WaitForTimeoutAsync(500);
                _logger.LogInformation("Digitando termo de busca 'capacete'...");
                await searchInput.PressSequentiallyAsync("capacete", new() { Delay = 150 });
                await page.WaitForTimeoutAsync(500);
                _logger.LogInformation("Enviando pesquisa...");
                await searchInput.PressAsync("Enter");
            }

            // Esperar resposta da API
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(25));

            var registration = cts.Token.Register(() => apiResponseTcs.TrySetCanceled());
            await using (registration)
            {
                try
                {
                    var content = await apiResponseTcs.Task;
                    
                    if (content.ValueKind == JsonValueKind.Object && content.TryGetProperty("items", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in itemsProp.EnumerateArray())
                        {
                            if (item.TryGetProperty("item_basic", out var basic))
                            {
                                if (basic.TryGetProperty("itemid", out var itemIdProp) &&
                                    basic.TryGetProperty("shopid", out var shopIdProp) &&
                                    basic.TryGetProperty("name", out var nameProp) &&
                                    basic.TryGetProperty("price", out var priceProp))
                                {
                                    long itemId = itemIdProp.GetInt64();
                                    long shopId = shopIdProp.GetInt64();
                                    string name = nameProp.GetString() ?? string.Empty;
                                    decimal price = priceProp.GetDecimal() / 100000m;
                                    
                                    string imageHash = basic.TryGetProperty("image", out var imageProp) ? (imageProp.GetString() ?? string.Empty) : string.Empty;
                                    string imageUrl = !string.IsNullOrEmpty(imageHash) 
                                        ? $"https://down-br.img.sg.susercontent.com/file/{imageHash}" 
                                        : string.Empty;
                                    
                                    string sourceUrl = $"https://shopee.com.br/product/{shopId}/{itemId}";
                                    
                                    offers.Add(new Offer(
                                        Id: itemId.ToString(),
                                        Title: name,
                                        Price: price,
                                        ImageUrl: imageUrl,
                                        SourceUrl: sourceUrl,
                                        AffiliateLink: string.Empty,
                                        Provider: "Shopee",
                                        CapturedAt: DateTimeOffset.UtcNow
                                    ));
                                }
                            }
                        }
                        _logger.LogInformation("Coletadas com sucesso {Count} ofertas do Shopee.", offers.Count);
                    }
                }
                catch (TaskCanceledException)
                {
                    _logger.LogWarning("A interceptação da API da Shopee expirou ou foi cancelada.");
                }
            }

            await page.ScreenshotAsync(new() { Path = @"C:\Users\MateusRezende\.gemini\antigravity-ide\brain\c2dbcdba-de2d-41f3-8d7f-4f9fbf643918\stealth_diagnostico_final.png" });

            await context.CloseAsync();
            await browser.CloseAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro fatal no scraper stealth com Chrome Real.");
        }

        return offers;
    }
}
