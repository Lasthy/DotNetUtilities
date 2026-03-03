namespace ExternalApiUtilities;

/// <summary>
/// Configuração para um serviço de polling que requisita periodicamente um endpoint de API.
/// </summary>
public class ConfiguracaoPolling
{
    /// <summary>
    /// Nome identificador do polling job.
    /// </summary>
    public required string Nome { get; set; }

    /// <summary>
    /// Nome da API registrada (deve corresponder ao <see cref="ConfiguracaoApi.Nome"/>).
    /// </summary>
    public required string NomeApi { get; set; }

    /// <summary>
    /// Nome da rota a ser chamada (deve corresponder ao <see cref="RotaApi.Nome"/>).
    /// </summary>
    public required string NomeRota { get; set; }

    /// <summary>
    /// Intervalo entre cada chamada de polling.
    /// </summary>
    public required TimeSpan Intervalo { get; set; }

    /// <summary>
    /// Parâmetros de caminho fixos para a rota (opcional).
    /// </summary>
    public Dictionary<string, string>? ParametrosCaminho { get; set; }

    /// <summary>
    /// Parâmetros de query string fixos para a rota (opcional).
    /// </summary>
    public Dictionary<string, string>? ParametrosQuery { get; set; }
}
