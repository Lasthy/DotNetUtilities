using Microsoft.Extensions.DependencyInjection;

namespace ExternalApiUtilities;

/// <summary>
/// Implementação padrão de <see cref="IApiAdapterFactory"/>.
/// Resolve adaptadores criando instâncias de <see cref="ApiAdapter"/> via <see cref="IHttpClientFactory"/>.
/// </summary>
internal sealed class ApiAdapterFactory : IApiAdapterFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, ConfiguracaoApi> _configuracoes;

    public ApiAdapterFactory(
        IServiceProvider serviceProvider,
        IEnumerable<ConfiguracaoApi> configuracoes)
    {
        _serviceProvider = serviceProvider;
        _configuracoes = configuracoes.ToDictionary(c => c.Nome, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public IApiAdapter Obter(string nomeApi)
    {
        if (!_configuracoes.TryGetValue(nomeApi, out var config))
        {
            throw new InvalidOperationException(
                $"API '{nomeApi}' não está registrada. " +
                $"APIs disponíveis: {string.Join(", ", _configuracoes.Keys)}");
        }

        var httpClientFactory = _serviceProvider.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient($"ExternalApi_{config.Nome}");
        var logger = _serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ApiAdapter>>();

        return new ApiAdapter(httpClient, config, logger);
    }

    /// <inheritdoc />
    public IEnumerable<IApiAdapter> ObterTodos()
    {
        return _configuracoes.Keys.Select(Obter);
    }
}
