namespace ExternalApiUtilities;

/// <summary>
/// Interface para adaptadores de API externa.
/// Cada instância é associada a uma <see cref="ConfiguracaoApi"/> específica.
/// </summary>
public interface IApiAdapter
{
    /// <summary>
    /// Nome da API associada a este adaptador.
    /// </summary>
    string NomeApi { get; }

    /// <summary>
    /// Envia uma requisição para a rota especificada e retorna a resposta bruta.
    /// </summary>
    /// <param name="nomeRota">Nome da rota registrada na <see cref="ConfiguracaoApi"/>.</param>
    /// <param name="parametrosCaminho">Valores para substituir placeholders no caminho (ex: {id}).</param>
    /// <param name="parametrosQuery">Parâmetros de query string.</param>
    /// <param name="corpo">Corpo da requisição (será serializado como JSON).</param>
    /// <param name="ct">Token de cancelamento.</param>
    Task<RespostaApi> EnviarAsync(
        string nomeRota,
        Dictionary<string, string>? parametrosCaminho = null,
        Dictionary<string, string>? parametrosQuery = null,
        object? corpo = null,
        CancellationToken ct = default);

    /// <summary>
    /// Envia uma requisição para a rota especificada e deserializa a resposta.
    /// </summary>
    /// <typeparam name="T">Tipo para deserialização da resposta.</typeparam>
    /// <param name="nomeRota">Nome da rota registrada na <see cref="ConfiguracaoApi"/>.</param>
    /// <param name="parametrosCaminho">Valores para substituir placeholders no caminho (ex: {id}).</param>
    /// <param name="parametrosQuery">Parâmetros de query string.</param>
    /// <param name="corpo">Corpo da requisição (será serializado como JSON).</param>
    /// <param name="ct">Token de cancelamento.</param>
    Task<RespostaApi<T>> EnviarAsync<T>(
        string nomeRota,
        Dictionary<string, string>? parametrosCaminho = null,
        Dictionary<string, string>? parametrosQuery = null,
        object? corpo = null,
        CancellationToken ct = default);
}
