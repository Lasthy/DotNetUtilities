using Microsoft.Extensions.DependencyInjection;

namespace ExternalApiUtilities;

/// <summary>
/// Métodos de extensão para registro dos serviços de API externa no container de DI.
/// </summary>
public static class ExternalApiExtensions
{
    /// <summary>
    /// Configura o sistema de APIs externas com adaptadores, mappers e polling.
    /// <para>Requer que o DualDb esteja configurado previamente para funcionalidades de polling com persistência.</para>
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddExternalApi(api =>
    /// {
    ///     api.AdicionarApi(config =>
    ///     {
    ///         config.Nome = "academia-api";
    ///         config.UrlBase = "https://api.academia.com";
    ///         config.Rotas.Add(new RotaApi { Nome = "listar-alunos", Caminho = "/api/alunos" });
    ///     });
    ///
    ///     api.AdicionarMapper&lt;AlunoDto, Aluno, AlunoMapper&gt;();
    ///
    ///     api.AdicionarPolling&lt;AlunoDto, Aluno&gt;(polling =>
    ///     {
    ///         polling.Nome = "polling-alunos";
    ///         polling.NomeApi = "academia-api";
    ///         polling.NomeRota = "listar-alunos";
    ///         polling.Intervalo = TimeSpan.FromMinutes(5);
    ///     });
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddExternalApi(
        this IServiceCollection services,
        Action<ExternalApiBuilder> configurar)
    {
        var builder = new ExternalApiBuilder(services);
        configurar(builder);

        // Registra todas as configurações de API como serviços
        foreach (var config in builder.Configuracoes)
            services.AddSingleton(config);

        // Registra a fábrica de adaptadores
        services.AddSingleton<IApiAdapterFactory, ApiAdapterFactory>();

        // Registra os serviços de polling
        foreach (var registration in builder.PollingRegistrations)
            registration(services);

        return services;
    }
}
