using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using RZND.Bot;
using RZND.Bot.Api;
using RZND.Bot.Scrapers;
using RZND.Bot.Providers;
using RZND.Bot.Providers.Amazon;
using RZND.Bot.Providers.Magalu;
using RZND.Bot.Services;
using RZND.Bot.Watchlist;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ==== SERILOG ====
builder.Host.UseSerilog((ctx, services, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .MinimumLevel.Information()
        .WriteTo.Console());

// ==== JSON: camelCase + nulos omitidos ====
builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    opts.SerializerOptions.PropertyNameCaseInsensitive = true;
    opts.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// ==== SERVIÇOS CORE ====
builder.Services.AddHttpClient();

// Configurações de APIs (lidas do appsettings.json)
builder.Services.Configure<ShopeeApiSettings>(builder.Configuration.GetSection("Shopee"));
builder.Services.Configure<AmazonApiSettings>(builder.Configuration.GetSection("Amazon"));
builder.Services.Configure<MagaluApiSettings>(builder.Configuration.GetSection("Magalu"));
builder.Services.Configure<TelegramSettings>(builder.Configuration.GetSection("Telegram"));

// Estado compartilhado entre Worker e API
builder.Services.AddSingleton<BotStateService>();
builder.Services.AddSingleton<ILoggerProvider, MemoryLoggerProvider>();

// Watchlist (com hot-reload via FileSystemWatcher)
builder.Services.AddSingleton<WatchlistLoader>();

// ==== SCOUTS (sempre registrados e filtrados em tempo de execução) ====
builder.Services.AddSingleton<IProductScoutProvider, WebScraperProvider>();
builder.Services.AddSingleton<IProductScoutProvider, AmazonScoutProvider>();
builder.Services.AddSingleton<IProductScoutProvider, MagaluScoutProvider>();
builder.Services.AddSingleton<IProductScoutProvider, WatchlistScoutProvider>();

// Fetchers (sempre registrados — usados tanto pelo Scout quanto pelo Scatter-Gather)
builder.Services.AddSingleton<ShopeeApiProvider>();
builder.Services.AddSingleton<IProductDetailsFetcher>(sp => sp.GetRequiredService<ShopeeApiProvider>());
builder.Services.AddSingleton<ISearchableProvider>(sp => sp.GetRequiredService<ShopeeApiProvider>());

builder.Services.AddSingleton<AmazonDetailsFetcher>();
builder.Services.AddSingleton<IProductDetailsFetcher>(sp => sp.GetRequiredService<AmazonDetailsFetcher>());
builder.Services.AddSingleton<ISearchableProvider>(sp => sp.GetRequiredService<AmazonDetailsFetcher>());

builder.Services.AddSingleton<MagaluDetailsFetcher>();
builder.Services.AddSingleton<IProductDetailsFetcher>(sp => sp.GetRequiredService<MagaluDetailsFetcher>());
builder.Services.AddSingleton<ISearchableProvider>(sp => sp.GetRequiredService<MagaluDetailsFetcher>());

// Scraper legado Shopee (Playwright)
builder.Services.AddSingleton<IOfferScraper, ShopeeOfferOrchestrator>();

// Motor de comparação
builder.Services.AddSingleton<PriceComparisonService>();
builder.Services.AddSingleton<IAiNormalizer, FallbackAiNormalizer>();
builder.Services.AddSingleton<IProductMatcherService, ProductMatcherService>();

// Renderizadores e Publicadores do MVP
builder.Services.AddSingleton<CreativeRendererService>();
builder.Services.AddSingleton<TelegramPublisherService>();

// Worker de background
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

// ==== STATIC FILES (wwwroot/browser/ → Dashboard) ====
var browserPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "browser");
if (Directory.Exists(browserPath))
{
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(browserPath)
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(browserPath)
    });
}
else
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

// Servir a pasta física 'output/' no caminho de URL '/output' para download e exibição
var outputFolder = Path.Combine(Directory.GetCurrentDirectory(), "output");
Directory.CreateDirectory(outputFolder);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(outputFolder),
    RequestPath = "/output"
});

// ==== MINIMAL API ====

// GET /api/outputs — lista todas as mídias geradas
app.MapGet("/api/outputs", (BotStateService state) =>
{
    return Results.Ok(state.Outputs);
});

// GET /api/templates/{name}/html — lê o arquivo index.html do template
app.MapGet("/api/templates/{name}/html", (string name) =>
{
    var path = Path.Combine(Directory.GetCurrentDirectory(), "templates", name, "index.html");
    if (!File.Exists(path)) return Results.NotFound("Template não localizado.");
    return Results.Text(File.ReadAllText(path), "text/html");
});

// GET /api/templates/{name}/css — lê o arquivo styles.css do template
app.MapGet("/api/templates/{name}/css", (string name) =>
{
    var path = Path.Combine(Directory.GetCurrentDirectory(), "templates", name, "styles.css");
    if (!File.Exists(path)) return Results.NotFound("Estilos do template não localizados.");
    return Results.Text(File.ReadAllText(path), "text/css");
});

// PUT /api/templates/{name}/html — salva o arquivo index.html do template
app.MapPut("/api/templates/{name}/html", async (string name, HttpRequest request) =>
{
    var path = Path.Combine(Directory.GetCurrentDirectory(), "templates", name, "index.html");
    if (!File.Exists(path)) return Results.NotFound("Template não localizado.");
    using var reader = new StreamReader(request.Body);
    var content = await reader.ReadToEndAsync();
    await File.WriteAllTextAsync(path, content);
    return Results.Ok();
});

// PUT /api/templates/{name}/css — salva o arquivo styles.css do template
app.MapPut("/api/templates/{name}/css", async (string name, HttpRequest request) =>
{
    var path = Path.Combine(Directory.GetCurrentDirectory(), "templates", name, "styles.css");
    if (!File.Exists(path)) return Results.NotFound("Estilos do template não localizados.");
    using var reader = new StreamReader(request.Body);
    var content = await reader.ReadToEndAsync();
    await File.WriteAllTextAsync(path, content);
    return Results.Ok();
});

// GET /api/status — estado geral do bot
app.MapGet("/api/status", (BotStateService state, WatchlistLoader watchlist, Microsoft.Extensions.Options.IOptions<TelegramSettings> tSettings) =>
{
    var uptime = DateTimeOffset.UtcNow - state.StartedAt;
    var config = watchlist.Current;

    var t = tSettings.Value;
    var isTelegramConfigured = !string.IsNullOrWhiteSpace(t.BotToken) && !string.IsNullOrWhiteSpace(t.ChannelId);
    var tLabel = isTelegramConfigured ? "Pronto" : "Desativado";
    var tStatus = isTelegramConfigured ? "ok" : "warning";

    return Results.Ok(new
    {
        isRunning  = true,
        isScanning = state.IsScanning,
        uptime     = FormatUptime(uptime),
        lastScanAt = state.LastScanAt?.ToLocalTime().ToString("HH:mm:ss"),
        lastScanDate = state.LastScanAt?.ToLocalTime().ToString("dd/MM/yyyy"),
        mode         = config.Mode,
        activeEntries = config.Entries.Count(e => e.Enabled),
        totalEntries  = config.Entries.Count,
        providers = new[]
        {
            new { name = "Bot Worker", status = "ok",      label = "Ativo"         },
            new { name = "Shopee",     status = "warning", label = "Sem chave API"  },
            new { name = "Amazon",     status = "ok",      label = "Simulado"       },
            new { name = "Magalu",     status = "ok",      label = "Simulado"       },
            new { name = "Telegram",   status = tStatus,   label = tLabel          }
        }
    });
});

// GET /api/logs — retorna os logs técnicos reais em memória
app.MapGet("/api/logs", (BotStateService state) =>
{
    return Results.Ok(state.Logs);
});

// DELETE /api/logs — limpa a lista de logs em memória
app.MapDelete("/api/logs", (BotStateService state) =>
{
    state.ClearLogs();
    return Results.Ok();
});

// GET /api/watchlist — retorna a watchlist atual
app.MapGet("/api/watchlist", (WatchlistLoader watchlist) =>
    Results.Ok(watchlist.Current));

// PUT /api/watchlist — salva nova configuração enviada pelo frontend
app.MapPut("/api/watchlist", (WatchlistConfig config, WatchlistLoader watchlist) =>
{
    watchlist.Save(config);
    return Results.Ok(new { saved = true });
});

// GET /api/results — retorna os últimos clusters comparados
app.MapGet("/api/results", (BotStateService state) =>
    Results.Ok(state.GetLastResults()));

// POST /api/scan/run — solicita scan imediato (acorda o Worker antes dos 15min)
app.MapPost("/api/scan/run", (BotStateService state) =>
{
    state.RequestImmediateScan();
    return Results.Accepted("/api/results", new { message = "Scan solicitado. O Worker iniciará em instantes." });
});

// GET /api/settings — retorna as chaves atuais do appsettings.json
app.MapGet("/api/settings", () =>
{
    var path = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
    if (!File.Exists(path)) return Results.NotFound();

    try
    {
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var shopee = root.TryGetProperty("Shopee", out var s) ? new
        {
            appId = s.TryGetProperty("AppId", out var sId) ? sId.GetString() : "",
            appSecret = s.TryGetProperty("AppSecret", out var sSec) ? sSec.GetString() : "",
            partnerTag = s.TryGetProperty("PartnerTag", out var sTag) ? sTag.GetString() : ""
        } : new { appId = "", appSecret = "", partnerTag = "" };

        var amazon = root.TryGetProperty("Amazon", out var a) ? new
        {
            partnerTag = a.TryGetProperty("PartnerTag", out var aTag) ? aTag.GetString() : ""
        } : new { partnerTag = "" };

        var magalu = root.TryGetProperty("Magalu", out var m) ? new
        {
            storeId = m.TryGetProperty("StoreId", out var mStore) ? mStore.GetString() : ""
        } : new { storeId = "" };

        var telegram = root.TryGetProperty("Telegram", out var t) ? new
        {
            botToken = t.TryGetProperty("BotToken", out var tBot) ? tBot.GetString() : "",
            channelId = t.TryGetProperty("ChannelId", out var tChan) ? tChan.GetString() : ""
        } : new { botToken = "", channelId = "" };

        return Results.Ok(new { shopee, amazon, magalu, telegram });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Erro ao ler configurações: {ex.Message}");
    }
});

// PUT /api/settings — salva as configurações no appsettings.json
app.MapPut("/api/settings", async (HttpContext context) =>
{
    var path = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
    if (!File.Exists(path)) return Results.NotFound();

    try
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();
        using var updateDoc = JsonDocument.Parse(body);
        var updateRoot = updateDoc.RootElement;

        var originalJson = File.ReadAllText(path);
        var node = System.Text.Json.Nodes.JsonNode.Parse(originalJson);
        if (node == null) return Results.BadRequest("JSON inválido");

        if (updateRoot.TryGetProperty("shopee", out var shopeeProp))
        {
            var shopeeNode = node["Shopee"];
            if (shopeeNode != null)
            {
                if (shopeeProp.TryGetProperty("appId", out var appId)) shopeeNode["AppId"] = appId.GetString();
                if (shopeeProp.TryGetProperty("appSecret", out var appSecret)) shopeeNode["AppSecret"] = appSecret.GetString();
                if (shopeeProp.TryGetProperty("partnerTag", out var partnerTag)) shopeeNode["PartnerTag"] = partnerTag.GetString();
            }
        }

        if (updateRoot.TryGetProperty("amazon", out var amazonProp))
        {
            var amazonNode = node["Amazon"];
            if (amazonNode != null)
            {
                if (amazonProp.TryGetProperty("partnerTag", out var partnerTag)) amazonNode["PartnerTag"] = partnerTag.GetString();
            }
        }

        if (updateRoot.TryGetProperty("magalu", out var magaluProp))
        {
            var magaluNode = node["Magalu"];
            if (magaluNode != null)
            {
                if (magaluProp.TryGetProperty("storeId", out var storeId)) magaluNode["StoreId"] = storeId.GetString();
            }
        }

        if (updateRoot.TryGetProperty("telegram", out var telegramProp))
        {
            var telegramNode = node["Telegram"];
            if (telegramNode == null)
            {
                node["Telegram"] = new System.Text.Json.Nodes.JsonObject();
                telegramNode = node["Telegram"];
            }
            if (telegramNode != null)
            {
                if (telegramProp.TryGetProperty("botToken", out var botToken)) telegramNode["BotToken"] = botToken.GetString();
                if (telegramProp.TryGetProperty("channelId", out var channelId)) telegramNode["ChannelId"] = channelId.GetString();
            }
        }

        var writeOptions = new JsonSerializerOptions { WriteIndented = true };
        var updatedJson = node.ToJsonString(writeOptions);
        File.WriteAllText(path, updatedJson);

        return Results.Ok(new { saved = true });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Erro ao salvar configurações: {ex.Message}");
    }
});

await app.RunAsync();

// ── Helpers ──────────────────────────────────────────────────────────────



/// <summary>Formata um TimeSpan para exibição amigável.</summary>
static string FormatUptime(TimeSpan ts)
{
    if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes:D2}m";
    if (ts.TotalMinutes >= 1) return $"{(int)ts.TotalMinutes}m {ts.Seconds:D2}s";
    return $"{ts.Seconds}s";
}

/// <summary>Configurações para publicação automática no Telegram.</summary>
public sealed class TelegramSettings
{
    public string BotToken { get; set; } = "";
    public string ChannelId { get; set; } = "";
}
