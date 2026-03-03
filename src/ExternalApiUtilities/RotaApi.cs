namespace ExternalApiUtilities;

/// <summary>
/// Define uma rota (endpoint) de uma API externa.
/// </summary>
/// <example>
/// <code>
/// var rota = new RotaApi
/// {
///     Nome = "listar-alunos",
///     Caminho = "/api/alunos",
///     Metodo = HttpMethod.Get
/// };
///
/// // Com parâmetros de caminho (substituídos via dicionário):
/// var rotaDetalhe = new RotaApi
/// {
///     Nome = "buscar-aluno",
///     Caminho = "/api/alunos/{id}",
///     Metodo = HttpMethod.Get
/// };
/// </code>
/// </example>
public class RotaApi
{
    /// <summary>
    /// Nome identificador da rota (ex: "listar-alunos", "buscar-aluno").
    /// </summary>
    public required string Nome { get; set; }

    /// <summary>
    /// Caminho relativo do endpoint (ex: "/api/alunos", "/api/alunos/{id}").
    /// Suporta placeholders entre chaves que serão substituídos por parâmetros de caminho.
    /// </summary>
    public required string Caminho { get; set; }

    /// <summary>
    /// Método HTTP da rota. Padrão: GET.
    /// </summary>
    public HttpMethod Metodo { get; set; } = HttpMethod.Get;
}
