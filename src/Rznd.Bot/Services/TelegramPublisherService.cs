using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RZND.Bot.Services;

/// <summary>
/// Serviço de publicação automática no Telegram (TelegramPublisherService).
/// Envia anúncios estruturados com imagens e links de afiliados para canais configurados pelo usuário.
/// </summary>
public sealed class TelegramPublisherService
{
    private readonly HttpClient _http;
    private readonly TelegramSettings _settings;
    private readonly ILogger<TelegramPublisherService> _logger;

    public TelegramPublisherService(
        HttpClient http,
        IOptions<TelegramSettings> settings,
        ILogger<TelegramPublisherService> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Envia a imagem e texto promocional para o canal configurado via Telegram Bot API.
    /// </summary>
    public async Task PublishPhotoAsync(string imagePath, string caption, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_settings.BotToken) || string.IsNullOrWhiteSpace(_settings.ChannelId))
        {
            _logger.LogWarning("[Telegram] Token do Bot ou ID do Canal não configurados. Pulando divulgação automática.");
            return;
        }

        _logger.LogInformation("[Telegram] Enviando criativo para o canal: {ChannelId}...", _settings.ChannelId);

        try
        {
            var url = $"https://api.telegram.org/bot{_settings.BotToken}/sendPhoto";

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(_settings.ChannelId), "chat_id");
            content.Add(new StringContent(caption), "caption");
            content.Add(new StringContent("HTML"), "parse_mode");

            var fileBytes = await File.ReadAllBytesAsync(imagePath, ct);
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            content.Add(fileContent, "photo", Path.GetFileName(imagePath));

            var response = await _http.PostAsync(url, content, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[Telegram] Anúncio enviado com sucesso!");
            }
            else
            {
                var errorMsg = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("[Telegram] Falha ao enviar foto para API. Status: {StatusCode}, Resposta: {Response}", 
                    response.StatusCode, errorMsg);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Telegram] Erro de rede ao conectar à API do Telegram.");
        }
    }
}
