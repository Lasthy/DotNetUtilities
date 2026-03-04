using DualDbUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExternalApiUtilities;

/// <summary>
/// Serviço hospedado que realiza polling periódico multi-tenant em um endpoint de API externa,
/// mapeia a resposta para entidades e persiste no DualDb.
/// <para>
/// Para cada ciclo, obtém os contextos via <see cref="IProvedorContextoPolling"/>
/// e executa o polling para cada um.
/// </para>
/// </summary>
/// <typeparam name="TResposta">Tipo do dado retornado pela API (DTO).</typeparam>
/// <typeparam name="TEntidade">Tipo da entidade do domínio.</typeparam>
public sealed class ServicoPollingApi<TResposta, TEntidade> : BackgroundService
    where TEntidade : class, IEntidade
{
    private readonly ConfiguracaoPolling _config;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ServicoPollingApi<TResposta, TEntidade>> _logger;

    public ServicoPollingApi(
        ConfiguracaoPolling config,
        IServiceScopeFactory scopeFactory,
        ILogger<ServicoPollingApi<TResposta, TEntidade>> logger)
    {
        _config = config;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Polling [{Nome}] iniciado - API: {Api}, Rota: {Rota}, Intervalo: {Intervalo}",
            _config.Nome, _config.NomeApi, _config.NomeRota, _config.Intervalo);

        // Aguarda um ciclo inicial antes de começar (evita sobrecarga no startup)
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        using var timer = new PeriodicTimer(_config.Intervalo);

        // Executa imediatamente na primeira vez, depois aguarda o timer
        do
        {
            try
            {
                await ExecutarPollingAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Polling [{Nome}] falhou com exceção", _config.Nome);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ExecutarPollingAsync(CancellationToken ct)
    {
        _logger.LogDebug("Polling [{Nome}] executando...", _config.Nome);

        await using var scope = _scopeFactory.CreateAsyncScope();

        var contextProvider = scope.ServiceProvider.GetService<IProvedorContextoPolling>();

        if (contextProvider is null)
        {
            // Sem provedor de contexto — execução single-tenant (compatibilidade)
            await ExecutarParaContextoAsync(scope.ServiceProvider, new ContextoPolling { Id = 0 }, ct);
            return;
        }

        var contextos = await contextProvider.ObterContextosAsync(ct);

        if (contextos.Count == 0)
        {
            _logger.LogDebug("Polling [{Nome}] sem contextos para processar", _config.Nome);
            return;
        }

        foreach (var contexto in contextos)
        {
            try
            {
                await ExecutarParaContextoAsync(scope.ServiceProvider, contexto, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex,
                    "Polling [{Nome}] falhou para contexto {ContextoId}",
                    _config.Nome, contexto.Id);
            }
        }
    }

    private async Task ExecutarParaContextoAsync(
        IServiceProvider sp, ContextoPolling contexto, CancellationToken ct)
    {
        var adapterFactory = sp.GetRequiredService<IApiAdapterFactory>();
        var mapper = sp.GetRequiredService<IRespostaMapper<TResposta, TEntidade>>();
        var dualDb = sp.GetRequiredService<DualDbContext>();

        var adapter = adapterFactory.Obter(_config.NomeApi);

        // Mescla parâmetros fixos com os do contexto
        var queryParams = MesclarParametros(_config.ParametrosQuery, contexto.ParametrosAdicionais);

        var resposta = await adapter.EnviarAsync<TResposta>(
            _config.NomeRota,
            _config.ParametrosCaminho,
            queryParams,
            ct: ct);

        if (!resposta.Sucesso)
        {
            _logger.LogWarning(
                "Polling [{Nome}] ctx={ContextoId} requisição falhou: {Status} - {Erro}",
                _config.Nome, contexto.Id, resposta.CodigoStatus, resposta.MensagemErro);
            return;
        }

        if (resposta.Dados is null)
        {
            _logger.LogWarning("Polling [{Nome}] ctx={ContextoId} resposta sem dados",
                _config.Nome, contexto.Id);
            return;
        }

        var entidades = await mapper.MapearAsync(resposta.Dados, contexto.Id, ct);

        if (entidades.Count == 0)
        {
            _logger.LogDebug("Polling [{Nome}] ctx={ContextoId} sem entidades para salvar",
                _config.Nome, contexto.Id);
            return;
        }

        await dualDb.AdicionarVariosAsync(entidades, ct);

        _logger.LogInformation(
            "Polling [{Nome}] ctx={ContextoId} salvou {Count} entidade(s) no banco temporário",
            _config.Nome, contexto.Id, entidades.Count);
    }

    private static Dictionary<string, string>? MesclarParametros(
        Dictionary<string, string>? fixos,
        Dictionary<string, string>? adicionais)
    {
        if ((fixos is null || fixos.Count == 0) && (adicionais is null || adicionais.Count == 0))
            return null;

        var resultado = new Dictionary<string, string>(fixos ?? []);

        if (adicionais is not null)
        {
            foreach (var (chave, valor) in adicionais)
                resultado[chave] = valor;
        }

        return resultado;
    }
}
