namespace DualDbUtilities;

/// <summary>
/// Coordenador de sincronização thread-safe para o DualDb.
/// Utiliza um padrão async reader-writer lock:
/// <list type="bullet">
///   <item><description>Operações de escrita no banco temporário adquirem lock compartilhado (concurrent).</description></item>
///   <item><description>Sincronização adquire lock exclusivo (bloqueia escritas e aguarda as em andamento).</description></item>
/// </list>
/// Deve ser registrado como singleton no container de DI.
/// </summary>
public sealed class DualDbSyncCoordinator
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private int _operacaoCount;

    /// <summary>
    /// Adquire um lock compartilhado para operações de escrita no banco temporário.
    /// Múltiplas operações podem executar simultaneamente.
    /// Bloqueia enquanto uma sincronização estiver em andamento.
    /// </summary>
    public async Task<IAsyncDisposable> AdquirirOperacaoAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (++_operacaoCount == 1)
                await _syncLock.WaitAsync(ct);
        }
        finally
        {
            _gate.Release();
        }

        return new LockReleaser(async () =>
        {
            await _gate.WaitAsync(CancellationToken.None);
            try
            {
                if (--_operacaoCount == 0)
                    _syncLock.Release();
            }
            finally
            {
                _gate.Release();
            }
        });
    }

    /// <summary>
    /// Adquire um lock exclusivo para sincronização.
    /// Aguarda todas as operações de escrita em andamento finalizarem antes de prosseguir.
    /// </summary>
    public async Task<IAsyncDisposable> AdquirirSincronizacaoAsync(CancellationToken ct = default)
    {
        await _syncLock.WaitAsync(ct);
        return new LockReleaser(() =>
        {
            _syncLock.Release();
            return ValueTask.CompletedTask;
        });
    }

    private sealed class LockReleaser(Func<ValueTask> releaseFunc) : IAsyncDisposable
    {
        private int _disposed;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
                return releaseFunc();
            return ValueTask.CompletedTask;
        }
    }
}
