using DualDbUtilities;

namespace ExternalApiUtilities;

/// <summary>
/// Interface para mapeamento de respostas de API externa para entidades do domínio.
/// </summary>
/// <typeparam name="TResposta">Tipo do dado retornado pela API (DTO da resposta).</typeparam>
/// <typeparam name="TEntidade">Tipo da entidade do domínio que implementa <see cref="IEntidade"/>.</typeparam>
/// <example>
/// <code>
/// public class AlunoApiMapper : IRespostaMapper&lt;AlunoDto, Aluno&gt;
/// {
///     public Task&lt;IReadOnlyList&lt;Aluno&gt;&gt; MapearAsync(AlunoDto resposta, int clienteId, CancellationToken ct)
///     {
///         var lista = new List&lt;Aluno&gt;
///         {
///             new Aluno
///             {
///                 Id = resposta.Id,
///                 Nome = resposta.NomeCompleto,
///                 Graduacao = resposta.Faixa
///             }
///         };
///         return Task.FromResult&lt;IReadOnlyList&lt;Aluno&gt;&gt;(lista);
///     }
/// }
/// </code>
/// </example>
public interface IRespostaMapper<in TResposta, TEntidade>
    where TEntidade : class, IEntidade
{
    /// <summary>
    /// Converte a resposta da API em uma ou mais entidades do domínio.
    /// </summary>
    /// <param name="resposta">Dado deserializado da resposta da API.</param>
    /// <param name="clienteId">ID do cliente (tenant) dono dos dados.</param>
    /// <param name="ct">Token de cancelamento.</param>
    /// <returns>Entidades mapeadas.</returns>
    Task<IReadOnlyList<TEntidade>> MapearAsync(TResposta resposta, int clienteId, CancellationToken ct = default);
}
