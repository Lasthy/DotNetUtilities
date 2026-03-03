namespace ExternalApiUtilities;

/// <summary>
/// Configuração de uma API externa. Define URL base, headers padrão, timeout e rotas disponíveis.
/// </summary>
public class ConfiguracaoApi
{
    /// <summary>
    /// Nome identificador da API (ex: "pagamentos", "ranking").
    /// Usado como chave para resolver o <see cref="IApiAdapter"/> correspondente.
    /// </summary>
    public required string Nome { get; set; }

    /// <summary>
    /// URL base da API (ex: "https://api.exemplo.com").
    /// </summary>
    public required string UrlBase { get; set; }

    /// <summary>
    /// Headers padrão enviados em todas as requisições para esta API.
    /// </summary>
    public Dictionary<string, string> HeadersPadrao { get; set; } = new();

    /// <summary>
    /// Timeout padrão para requisições. Padrão: 30 segundos.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Rotas (endpoints) disponíveis nesta API.
    /// </summary>
    public List<RotaApi> Rotas { get; set; } = [];

    /// <summary>
    /// Deserializador customizado para respostas desta API.
    /// <para>
    /// Use quando a API retorna um envelope padrão (ex: <c>{ "success": true, "data": ... }</c>)
    /// e você quer extrair os dados automaticamente antes de passar para o mapper.
    /// </para>
    /// Se <c>null</c>, usa o deserializador padrão (<see cref="System.Text.Json.JsonSerializer"/>).
    /// </summary>
    public IDesserializadorResposta? Desserializador { get; set; }
}
