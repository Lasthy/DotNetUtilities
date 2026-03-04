using DualDbUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExternalApiUtilities;

/// <summary>
/// Builder fluente para configurar APIs externas, mappers e polling no container de DI.
/// </summary>
public class ExternalApiBuilder
{
    internal readonly IServiceCollection Services;
    internal readonly List<ConfiguracaoApi> Configuracoes = [];
    internal readonly List<Action<IServiceCollection>> PollingRegistrations = [];

    internal ExternalApiBuilder(IServiceCollection services)
    {
        Services = services;
    }

    /// <summary>
    /// Adiciona uma API externa com a configuração especificada.
    /// </summary>
    /// <param name="configurar">Action para configurar a API.</param>
    public ExternalApiBuilder AdicionarApi(Action<ConfiguracaoApi> configurar)
    {
        var config = new ConfiguracaoApi { Nome = null!, UrlBase = null! };
        configurar(config);

        ArgumentException.ThrowIfNullOrWhiteSpace(config.Nome, nameof(config.Nome));
        ArgumentException.ThrowIfNullOrWhiteSpace(config.UrlBase, nameof(config.UrlBase));

        Configuracoes.Add(config);

        // Registra o HttpClient nomeado com base URL e headers
        Services.AddHttpClient($"ExternalApi_{config.Nome}", client =>
        {
            client.BaseAddress = new Uri(config.UrlBase);
            client.Timeout = config.Timeout;

            foreach (var (chave, valor) in config.HeadersPadrao)
                client.DefaultRequestHeaders.TryAddWithoutValidation(chave, valor);
        });

        return this;
    }

    /// <summary>
    /// Registra um <see cref="IRespostaMapper{TResposta, TEntidade}"/> no container de DI.
    /// </summary>
    /// <typeparam name="TResposta">Tipo do DTO da resposta da API.</typeparam>
    /// <typeparam name="TEntidade">Tipo da entidade de domínio.</typeparam>
    /// <typeparam name="TMapper">Implementação concreta do mapper.</typeparam>
    public ExternalApiBuilder AdicionarMapper<TResposta, TEntidade, TMapper>()
        where TEntidade : class, IEntidade
        where TMapper : class, IRespostaMapper<TResposta, TEntidade>
    {
        Services.AddTransient<IRespostaMapper<TResposta, TEntidade>, TMapper>();
        return this;
    }

    /// <summary>
    /// Registra um <see cref="IProvedorContextoPolling"/> para polling multi-tenant.
    /// </summary>
    /// <typeparam name="TProvedor">Implementação concreta do provedor de contextos.</typeparam>
    public ExternalApiBuilder AdicionarProvedorContexto<TProvedor>()
        where TProvedor : class, IProvedorContextoPolling
    {
        Services.AddScoped<IProvedorContextoPolling, TProvedor>();
        return this;
    }

    /// <summary>
    /// Adiciona um serviço de polling que requisita periodicamente um endpoint,
    /// mapeia a resposta e salva no DualDb.
    /// <para>
    /// Se um <see cref="IProvedorContextoPolling"/> estiver registrado, o polling
    /// itera sobre cada contexto (cliente/tenant) retornado.
    /// </para>
    /// </summary>
    /// <typeparam name="TResposta">Tipo do DTO da resposta da API.</typeparam>
    /// <typeparam name="TEntidade">Tipo da entidade de domínio.</typeparam>
    /// <param name="configurar">Action para configurar o polling.</param>
    public ExternalApiBuilder AdicionarPolling<TResposta, TEntidade>(Action<ConfiguracaoPolling> configurar)
        where TEntidade : class, IEntidade
    {
        var config = new ConfiguracaoPolling { Nome = null!, NomeApi = null!, NomeRota = null!, Intervalo = default };
        configurar(config);

        ArgumentException.ThrowIfNullOrWhiteSpace(config.Nome, nameof(config.Nome));
        ArgumentException.ThrowIfNullOrWhiteSpace(config.NomeApi, nameof(config.NomeApi));
        ArgumentException.ThrowIfNullOrWhiteSpace(config.NomeRota, nameof(config.NomeRota));

        if (config.Intervalo <= TimeSpan.Zero)
            throw new ArgumentException("Intervalo de polling deve ser positivo.", nameof(config.Intervalo));

        PollingRegistrations.Add(svc =>
        {
            // Usa factory para injetar a config capturada diretamente, evitando
            // ambiguidade quando há múltiplos pollings registrados.
            svc.AddSingleton<Microsoft.Extensions.Hosting.IHostedService>(sp =>
                new ServicoPollingApi<TResposta, TEntidade>(
                    config,
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    sp.GetRequiredService<ILogger<ServicoPollingApi<TResposta, TEntidade>>>()));
        });

        return this;
    }
}
