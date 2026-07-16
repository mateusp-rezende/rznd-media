using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Microsoft.Extensions.Logging;

namespace RZND.Bot.Services;

/// <summary>
/// Serviço responsável por carregar templates HTML/CSS e renderizá-los em imagens de propaganda usando Playwright.
/// </summary>
public sealed class CreativeRendererService
{
    private readonly ILogger<CreativeRendererService> _logger;

    public CreativeRendererService(ILogger<CreativeRendererService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Renderiza um criativo PNG para o cluster de ofertas usando o template selecionado.
    /// </summary>
    public async Task<string> RenderCreativeAsync(ComparedOfferCluster cluster, string templateName, CancellationToken ct)
    {
        _logger.LogInformation("Iniciando renderização de criativo usando template '{TemplateName}'...", templateName);

        var templateDir = Path.Combine(Directory.GetCurrentDirectory(), "templates", templateName);
        var originalHtmlPath = Path.Combine(templateDir, "index.html");

        if (!File.Exists(originalHtmlPath))
        {
            throw new FileNotFoundException($"Template HTML não localizado em: {originalHtmlPath}");
        }

        var html = File.ReadAllText(originalHtmlPath);

        // Preencher as variáveis do template
        var savingsPct = 0;
        if (cluster.Offers.Count > 1)
        {
            var maxPrice = cluster.Offers.Max(o => o.Price);
            if (maxPrice > cluster.CheapestOffer.Price && maxPrice > 0)
            {
                savingsPct = (int)Math.Round(((maxPrice - cluster.CheapestOffer.Price) / maxPrice) * 100);
            }
        }

        var tagText = savingsPct > 0 ? $"{savingsPct}% OFF" : "MELHOR PREÇO";
        var averagePrice = cluster.Offers.Average(o => o.Price);
        var footerText = $"Preço médio de mercado: {averagePrice.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))}";

        html = html.Replace("{{Product.Image}}", cluster.CheapestOffer.ImageUrl)
                   .Replace("{{Product.Tag}}", tagText)
                   .Replace("{{Product.Name}}", cluster.CheapestOffer.Title)
                   .Replace("{{Product.Price}}", cluster.CheapestOffer.Price.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")))
                   .Replace("{{Product.Footer}}", footerText);

        // Salvar HTML temporário no próprio diretório do template para resolver os arquivos relativos (CSS, imagens)
        var guid = Guid.NewGuid().ToString("N");
        var tempHtmlPath = Path.Combine(templateDir, $"temp_{guid}.html");
        File.WriteAllText(tempHtmlPath, html);

        // Garantir que a pasta de output exista
        var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "output");
        Directory.CreateDirectory(outputDir);
        var outputPngPath = Path.Combine(outputDir, $"{guid}.png");

        try
        {
            using var playwright = await Playwright.CreateAsync();
            var launchOptions = new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[] { "--disable-web-security", "--no-sandbox" }
            };

            await using var browser = await playwright.Chromium.LaunchAsync(launchOptions);
            var page = await browser.NewPageAsync();

            // Definir tamanho da viewport (1080x1080 para carrossel/overlay Shopee)
            await page.SetViewportSizeAsync(1080, 1080);

            // Carregar o arquivo local
            var fileUri = new Uri(tempHtmlPath).AbsoluteUri;
            await page.GotoAsync(fileUri, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // Delay estratégico para garantir o carregamento de fontes e imagens externas
            await Task.Delay(1500, ct);

            // Capturar screenshot da tela
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = outputPngPath,
                Type = ScreenshotType.Png
            });

            _logger.LogInformation("Criativo renderizado com sucesso em: {Path}", outputPngPath);
            return outputPngPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante a renderização do criativo.");
            throw;
        }
        finally
        {
            // Limpeza
            try
            {
                if (File.Exists(tempHtmlPath))
                {
                    File.Delete(tempHtmlPath);
                }
            }
            catch { /* ignorar falhas de limpeza */ }
        }
    }
}
