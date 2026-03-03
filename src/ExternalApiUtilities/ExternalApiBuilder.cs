using DualDbUtilities;
using Microsoft.Extensions.DependencyInjection;

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
    /// <example>
    /// <code>
    /// builder.AdicionarApi(api =>
    /// {
    ///     api.Nome = "academia-api";
    ///     api.UrlBase = "https://api.academia.com";
    ///     api.HeadersPadrao["Authorization"] = "Bearer token";
    ///     api.Rotas.Add(new RotaApi { Nome = "listar-alunos", Caminho = "/api/alunos" });
    ///     api.Rotas.Add(new RotaApi { Nome = "buscar-aluno", Caminho = "/api/alunos/{id}" });
    /// });
    /// </code>
    /// </example>
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
    /// Adiciona um serviço de polling que requisita periodicamente um endpoint,
    /// mapeia a resposta e salva no DualDb.
    /// </summary>
    /// <typeparam name="TResposta">Tipo do DTO da resposta da API.</typeparam>
    /// <typeparam name="TEntidade">Tipo da entidade de domínio.</typeparam>
    /// <param name="configurar">Action para configurar o polling.</param>
    /// <example>
    /// <code>
    /// builder.AdicionarPolling&lt;AlunoDto, Aluno&gt;(polling =>
    /// {
    ///     polling.Nome = "polling-alunos";
    ///     polling.NomeApi = "academia-api";
    ///     polling.NomeRota = "listar-alunos";
    ///     polling.Intervalo = TimeSpan.FromMinutes(5);
    /// });
    /// </code>
    /// </example>
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
            svc.AddSingleton(config);
            svc.AddHostedService<ServicoPollingApi<TResposta, TEntidade>>();
        });

        return this;
    }
}
