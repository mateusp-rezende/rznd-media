using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RZND.Bot.Services;

namespace RZND.Bot.Api;

/// <summary>
/// Singleton que mantém o estado compartilhado entre o Worker e a Minimal API.
/// Thread-safe via lock e SemaphoreSlim.
/// </summary>
public sealed class BotStateService
{
    private readonly object _lock = new();
    private IReadOnlyCollection<ComparedOfferCluster> _lastResults = Array.Empty<ComparedOfferCluster>();
    private readonly List<GeneratedOutput> _outputs = new();
    private readonly List<string> _logs = new();
    private DateTimeOffset? _lastScanAt;
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private bool _isScanning;

    // Sinalização: acorda o Worker antes dos 15 minutos quando a API solicita scan imediato
    private readonly SemaphoreSlim _scanSignal = new(0, 1);

    /// <summary>Data e hora em que o bot foi iniciado.</summary>
    public DateTimeOffset StartedAt => _startedAt;

    /// <summary>Indica se o bot está executando uma varredura no momento.</summary>
    public bool IsScanning
    {
        get { lock (_lock) return _isScanning; }
        set { lock (_lock) _isScanning = value; }
    }

    /// <summary>Lista de logs técnicos coletados em memória.</summary>
    public IReadOnlyCollection<string> Logs
    {
        get { lock (_lock) return _logs.ToArray(); }
    }

    /// <summary>Adiciona uma linha de log com timestamp local.</summary>
    public void AddLog(string line)
    {
        lock (_lock)
        {
            var timeStr = DateTimeOffset.Now.ToString("HH:mm:ss");
            _logs.Add($"[{timeStr}] {line}");
            if (_logs.Count > 100) _logs.RemoveAt(0); // Limita a 100 logs
        }
    }

    /// <summary>Limpa todos os logs da memória.</summary>
    public void ClearLogs()
    {
        lock (_lock)
        {
            _logs.Clear();
        }
    }

    /// <summary>Retorna a lista de mídias geradas organizadas (thread-safe).</summary>
    public IReadOnlyCollection<GeneratedOutput> Outputs
    {
        get { lock (_lock) return _outputs.ToArray(); }
    }

    /// <summary>Registra um novo criativo gerado (thread-safe).</summary>
    public void RegisterOutput(GeneratedOutput output)
    {
        lock (_lock)
        {
            _outputs.Insert(0, output); // Mais recentes no topo
        }
    }

    /// <summary>Data e hora do último scan concluído.</summary>
    public DateTimeOffset? LastScanAt
    {
        get { lock (_lock) return _lastScanAt; }
    }

    /// <summary>
    /// Atualiza os resultados após um ciclo de comparação.
    /// Chamado pelo Worker ao final de cada scan.
    /// </summary>
    public void UpdateResults(IReadOnlyCollection<ComparedOfferCluster> results)
    {
        lock (_lock)
        {
            _lastResults = results;
            _lastScanAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>Retorna o último resultado de comparação (thread-safe).</summary>
    public IReadOnlyCollection<ComparedOfferCluster> GetLastResults()
    {
        lock (_lock) return _lastResults;
    }

    /// <summary>
    /// Sinaliza que um scan imediato foi solicitado pela API.
    /// O Worker aguarda esse sinal no lugar do delay de 15 minutos.
    /// </summary>
    public void RequestImmediateScan()
    {
        // CurrentCount == 0 significa que ninguém sinalizou ainda
        if (_scanSignal.CurrentCount == 0)
        {
            try { _scanSignal.Release(); }
            catch (SemaphoreFullException) { /* já sinalizado, ignorar */ }
        }
    }

    /// <summary>
    /// Bloqueia até que o timeout expire OU um scan imediato seja solicitado via API.
    /// Substitui o Task.Delay fixo no Worker para permitir scans sob demanda.
    /// </summary>
    public async Task WaitForNextScanAsync(TimeSpan timeout, CancellationToken ct)
    {
        try
        {
            await _scanSignal.WaitAsync(timeout, ct);
        }
        catch (OperationCanceledException)
        {
            // Normal: o host está encerrando
        }
    }
}

/// <summary>
/// Representa uma peça de mídia promocional gerada e seu texto de divulgação.
/// </summary>
public record GeneratedOutput(
    string Id,
    string Title,
    string Price,
    string Provider,
    string ImageUrl,
    string Caption,
    DateTimeOffset GeneratedAt
);

/// <summary>
/// Provedor de log personalizado para interceptar todas as saídas de logs reais e repassá-las à memória do BotStateService.
/// </summary>
public sealed class MemoryLoggerProvider : ILoggerProvider
{
    private readonly BotStateService _state;
    public MemoryLoggerProvider(BotStateService state) => _state = state;
    
    public ILogger CreateLogger(string categoryName) => new MemoryLogger(_state, categoryName);
    
    public void Dispose() { }

    private sealed class MemoryLogger : ILogger
    {
        private readonly BotStateService _state;
        private readonly string _category;

        public MemoryLogger(BotStateService state, string category)
        {
            _state = state;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var msg = formatter(state, exception);
            var categoryShort = _category.Split('.').Last();
            var levelStr = logLevel switch
            {
                LogLevel.Information => "INF",
                LogLevel.Warning => "WRN",
                LogLevel.Error => "ERR",
                _ => "DBG"
            };
            _state.AddLog($"[{levelStr}] [{categoryShort}] {msg}");
        }
    }
}
