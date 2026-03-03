using System.Net;

namespace ExternalApiUtilities;

/// <summary>
/// Resultado de uma chamada a uma API externa (sem deserialização tipada).
/// </summary>
public class RespostaApi
{
    /// <summary>
    /// Indica se a requisição foi bem-sucedida (status 2xx).
    /// </summary>
    public bool Sucesso { get; set; }

    /// <summary>
    /// Código de status HTTP retornado.
    /// </summary>
    public HttpStatusCode CodigoStatus { get; set; }

    /// <summary>
    /// Conteúdo bruto da resposta (string).
    /// </summary>
    public string? ConteudoBruto { get; set; }

    /// <summary>
    /// Mensagem de erro, caso a requisição tenha falhado.
    /// </summary>
    public string? MensagemErro { get; set; }
}

/// <summary>
/// Resultado de uma chamada a uma API externa com dados deserializados.
/// </summary>
/// <typeparam name="T">Tipo do dado deserializado da resposta.</typeparam>
public class RespostaApi<T> : RespostaApi
{
    /// <summary>
    /// Dados deserializados da resposta. <c>null</c> se a requisição falhou ou não houve conteúdo.
    /// </summary>
    public T? Dados { get; set; }
}
