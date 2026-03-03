using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DualDbUtilities;

/// <summary>
/// Serviço de inicialização que cria o banco SQLite temporário e configura o modo WAL
/// para melhor performance em cenários concorrentes.
/// </summary>
internal sealed class DualDbInitializer(
    IServiceScopeFactory scopeFactory,
    ILogger<DualDbInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        logger.LogInformation("Inicializando banco de dados temporário (SQLite)...");

        using var scope = scopeFactory.CreateScope();
        var tempDb = scope.ServiceProvider.GetRequiredService<TemporarioDbContext>();

        await tempDb.Database.EnsureCreatedAsync(ct);

        // WAL mode melhora performance com leituras concorrentes durante escrita
        await tempDb.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);

        logger.LogInformation("Banco temporário inicializado com sucesso.");
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
