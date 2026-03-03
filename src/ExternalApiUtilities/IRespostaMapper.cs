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
///     public IEnumerable&lt;Aluno&gt; Mapear(AlunoDto resposta)
///     {
///         yield return new Aluno
///         {
///             Id = resposta.Id,
///             Nome = resposta.NomeCompleto,
///             Graduacao = resposta.Faixa
///         };
///     }
/// }
/// </code>
/// </example>
public interface IRespostaMapper<in TResposta, out TEntidade>
    where TEntidade : class, IEntidade
{
    /// <summary>
    /// Converte a resposta da API em uma ou mais entidades do domínio.
    /// </summary>
    /// <param name="resposta">Dado deserializado da resposta da API.</param>
    /// <returns>Entidades mapeadas.</returns>
    IEnumerable<TEntidade> Mapear(TResposta resposta);
}
