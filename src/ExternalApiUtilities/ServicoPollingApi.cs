using DualDbUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExternalApiUtilities;

/// <summary>
/// Serviço hospedado que realiza polling periódico em um endpoint de API externa,
/// mapeia a resposta para entidades e persiste no DualDb.
/// </summary>
/// <typeparam name="TResposta">Tipo do dado retornado pela API (DTO).</typeparam>
/// <typeparam name="TEntidade">Tipo da entidade do domínio.</typeparam>
/// <example>
/// <code>
/// // Registrado automaticamente via:
/// builder.AdicionarPolling&lt;AlunoDto, Aluno&gt;(polling =>
/// {
///     polling.Nome = "polling-alunos";
///     polling.NomeApi = "academia-api";
///     polling.NomeRota = "listar-alunos";
///     polling.Intervalo = TimeSpan.FromMinutes(5);
/// });
/// </code>
/// </example>
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

        var adapterFactory = scope.ServiceProvider.GetRequiredService<IApiAdapterFactory>();
        var mapper = scope.ServiceProvider.GetRequiredService<IRespostaMapper<TResposta, TEntidade>>();
        var dualDb = scope.ServiceProvider.GetRequiredService<DualDbContext>();

        var adapter = adapterFactory.Obter(_config.NomeApi);

        var resposta = await adapter.EnviarAsync<TResposta>(
            _config.NomeRota,
            _config.ParametrosCaminho,
            _config.ParametrosQuery,
            ct: ct);

        if (!resposta.Sucesso)
        {
            _logger.LogWarning(
                "Polling [{Nome}] requisição falhou: {Status} - {Erro}",
                _config.Nome, resposta.CodigoStatus, resposta.MensagemErro);
            return;
        }

        if (resposta.Dados is null)
        {
            _logger.LogWarning("Polling [{Nome}] resposta sem dados", _config.Nome);
            return;
        }

        var entidades = mapper.Mapear(resposta.Dados).ToList();

        if (entidades.Count == 0)
        {
            _logger.LogDebug("Polling [{Nome}] sem entidades para salvar", _config.Nome);
            return;
        }

        await dualDb.AdicionarVariosAsync(entidades, ct);

        _logger.LogInformation(
            "Polling [{Nome}] salvou {Count} entidade(s) no banco temporário",
            _config.Nome, entidades.Count);
    }
}
