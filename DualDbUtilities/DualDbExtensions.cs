using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DualDbUtilities;

/// <summary>
/// Métodos de extensão para configuração do DualDb no container de DI.
/// </summary>
public static class DualDbExtensions
{
    /// <summary>
    /// Configura o sistema DualDb com banco temporário (SQLite) e banco final (SQL Server).
    /// Registra <see cref="TemporarioDbContext"/>, <see cref="FinalDbContext"/>,
    /// <see cref="DualDbContext"/>, e o <see cref="DualDbSyncCoordinator"/> (singleton).
    /// Também registra um <see cref="DualDbInitializer"/> que cria o banco SQLite automaticamente na inicialização.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddDualDb(options =>
    /// {
    ///     options.SqliteConnectionString = "Data Source=temp.db";
    ///     options.SqlServerConnectionString = connectionString;
    ///     options.AssembliesParaEscanear = [typeof(MinhaEntidade).Assembly];
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddDualDb(
        this IServiceCollection services,
        Action<DualDbOptions> configurar)
    {
        var options = new DualDbOptions
        {
            SqliteConnectionString = null!,
            SqlServerConnectionString = null!,
            AssembliesParaEscanear = null!
        };
        configurar(options);

        ArgumentException.ThrowIfNullOrWhiteSpace(options.SqliteConnectionString, nameof(options.SqliteConnectionString));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.SqlServerConnectionString, nameof(options.SqlServerConnectionString));
        ArgumentNullException.ThrowIfNull(options.AssembliesParaEscanear, nameof(options.AssembliesParaEscanear));

        if (options.AssembliesParaEscanear.Length == 0)
            throw new ArgumentException("É necessário informar ao menos um assembly para escanear.", nameof(options.AssembliesParaEscanear));

        services.AddSingleton(options);
        services.AddSingleton<DualDbSyncCoordinator>();

        services.AddDbContext<TemporarioDbContext>((_, dbOptions) =>
        {
            dbOptions.UseSqlite(options.SqliteConnectionString)
                .AddInterceptors(new DesabilitarFKInterceptor());
        });

        services.AddDbContext<FinalDbContext>((_, dbOptions) =>
        {
            dbOptions.UseSqlServer(options.SqlServerConnectionString);
        });

        services.AddScoped<DualDbContext>();
        services.AddHostedService<DualDbInitializer>();

        return services;
    }
}
