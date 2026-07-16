using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace RZND.Bot.Watchlist;

/// <summary>
/// Carrega e mantém a configuração da watchlist.json com hot-reload automático.
/// Quando o usuário edita o arquivo, o bot recarrega sem precisar reiniciar.
/// </summary>
public sealed class WatchlistLoader : IDisposable
{
    private readonly string _filePath;
    private readonly ILogger<WatchlistLoader> _logger;
    private readonly FileSystemWatcher _watcher;
    private readonly Lock _lock = new();

    private WatchlistConfig _config;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public WatchlistLoader(ILogger<WatchlistLoader> logger)
    {
        _logger = logger;

        // Procura watchlist.json ao lado do executável ou na raiz do projeto
        _filePath = ResolveFilePath();

        _config = LoadFromDisk();

        // Hot-reload: monitora mudanças no arquivo
        _watcher = new FileSystemWatcher(Path.GetDirectoryName(_filePath)!, Path.GetFileName(_filePath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnFileChanged;
        _logger.LogInformation("[Watchlist] Monitorando '{FilePath}' (hot-reload ativo).", _filePath);
    }

    /// <summary>
    /// Retorna a configuração atual da watchlist (thread-safe).
    /// </summary>
    public WatchlistConfig Current
    {
        get
        {
            lock (_lock)
            {
                return _config;
            }
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Pequeno delay para evitar leitura durante escrita parcial do editor
        Thread.Sleep(300);

        try
        {
            var newConfig = LoadFromDisk();
            lock (_lock)
            {
                _config = newConfig;
            }
            _logger.LogInformation("[Watchlist] Arquivo recarregado. Mode={Mode}, Entradas={Count}.",
                newConfig.Mode, newConfig.Entries.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Watchlist] Falha ao recarregar watchlist.json. Mantendo configuração anterior.");
        }
    }

    private WatchlistConfig LoadFromDisk()
    {
        if (!File.Exists(_filePath))
        {
            _logger.LogWarning("[Watchlist] Arquivo '{FilePath}' não encontrado. Criando padrão com modo 'both'.", _filePath);
            var defaultConfig = CreateDefault();
            SaveDefault(defaultConfig);
            return defaultConfig;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var config = JsonSerializer.Deserialize<WatchlistConfig>(json, _jsonOptions);
            if (config is null) throw new InvalidDataException("Arquivo JSON resultou em null.");

            _logger.LogInformation("[Watchlist] Carregado. Mode={Mode}, Total={Total}, Ativos={Active}.",
                config.Mode, config.Entries.Count, System.Linq.Enumerable.Count(config.ActiveEntries));

            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Watchlist] Erro ao parsear watchlist.json. Usando configuração padrão.");
            return CreateDefault();
        }
    }

    private static WatchlistConfig CreateDefault() => new(
        Mode: "both",
        Entries:
        [
            new WatchlistEntry("Smartphone Samsung Galaxy", MaxPrice: null, Enabled: true),
            new WatchlistEntry("Notebook Dell Inspiron", MaxPrice: 4500m, Enabled: true),
            new WatchlistEntry("Fone de Ouvido Bluetooth", MaxPrice: 300m, Enabled: true),
            new WatchlistEntry("iPhone 16", MaxPrice: 7000m, Enabled: false)
        ]
    );

    private void SaveDefault(WatchlistConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.WriteAllText(_filePath, json);
            _logger.LogInformation("[Watchlist] Arquivo padrão criado em '{FilePath}'.", _filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Watchlist] Não foi possível salvar watchlist.json padrão.");
        }
    }

    private static string ResolveFilePath()
    {
        // 1. Ao lado do executável (produção)
        var execDir = AppContext.BaseDirectory;
        var candidate = Path.Combine(execDir, "watchlist.json");
        if (File.Exists(candidate)) return candidate;

        // 2. Diretório de trabalho atual (debug via `dotnet run`)
        candidate = Path.Combine(Directory.GetCurrentDirectory(), "watchlist.json");
        return candidate;
    }

    /// <summary>
    /// Salva uma nova configuração em disco e atualiza o estado interno.
    /// Desabilita o FileSystemWatcher temporariamente para evitar auto-reload do próprio save.
    /// </summary>
    public void Save(WatchlistConfig config)
    {
        _watcher.EnableRaisingEvents = false;
        try
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            File.WriteAllText(_filePath, json);

            lock (_lock) { _config = config; }

            _logger.LogInformation("[Watchlist] Configuração salva via API. Mode={Mode}, Entradas={Count}.",
                config.Mode, config.Entries.Count);
        }
        finally
        {
            // Aguarda o OS liberar o file handle antes de reativar o watcher
            Thread.Sleep(250);
            _watcher.EnableRaisingEvents = true;
        }
    }

    public void Dispose()
    {
        _watcher.Changed -= OnFileChanged;
        _watcher.Dispose();
    }
}
