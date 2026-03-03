using System.Text.Json;

namespace ExternalApiUtilities;

/// <summary>
/// Interface para deserialização customizada de respostas de API.
/// <para>
/// Use quando a API retorna uma estrutura de envelope padrão e você quer
/// extrair automaticamente os dados internos antes de passar para o mapper.
/// </para>
/// </summary>
/// <example>
/// <code>
/// // Envelope padrão da API:
/// // { "success": true, "data": { ... }, "message": "OK" }
///
/// public class DesserializadorComEnvelope : IDesserializadorResposta
/// {
///     public T? Desserializar&lt;T&gt;(string conteudo, JsonSerializerOptions options)
///     {
///         var envelope = JsonSerializer.Deserialize&lt;EnvelopePadrao&lt;T&gt;&gt;(conteudo, options);
///         return envelope is { Sucesso: true } ? envelope.Dados : default;
///     }
/// }
///
/// public class EnvelopePadrao&lt;T&gt;
/// {
///     public bool Sucesso { get; set; }
///     public T? Dados { get; set; }
///     public string? Mensagem { get; set; }
/// }
/// </code>
/// </example>
public interface IDesserializadorResposta
{
    /// <summary>
    /// Deserializa o conteúdo bruto da resposta para o tipo especificado.
    /// </summary>
    /// <typeparam name="T">Tipo alvo da deserialização.</typeparam>
    /// <param name="conteudo">JSON bruto da resposta.</param>
    /// <param name="options">Opções de serialização JSON configuradas no adapter.</param>
    /// <returns>Objeto deserializado, ou <c>null</c> se não for possível.</returns>
    T? Desserializar<T>(string conteudo, JsonSerializerOptions options);
}
