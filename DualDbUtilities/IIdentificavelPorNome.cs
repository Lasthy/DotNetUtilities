namespace DualDbUtilities;

/// <summary>
/// Interface para entidades que possuem um nome identificador natural.
/// <para>
/// Entidades que implementam esta interface recebem automaticamente um índice na coluna <c>Nome</c>.
/// </para>
/// <para>
/// Quando combinada com <see cref="IEntidadePlaceholder"/>, habilita o mecanismo de
/// <b>remapeamento de chave primária</b>: se um placeholder com mesmo <c>Nome</c> já existir
/// no banco temporário, ao inserir a entidade real (com PK diferente), a PK antiga será
/// atualizada em cascata em todas as FKs dependentes.
/// </para>
/// </summary>
/// <example>
/// <code>
/// public class Categoria : IEntidade, IEntidadePlaceholder, IIdentificavelPorNome
/// {
///     public int Id { get; set; }
///     public string Nome { get; set; }
///     public bool EhPlaceholder { get; set; }
///
///     public static void Configurar(ModelBuilder builder) { ... }
/// }
/// </code>
/// </example>
public interface IIdentificavelPorNome
{
    /// <summary>
    /// Nome que identifica naturalmente esta entidade.
    /// Utilizado para localizar placeholders existentes durante o upsert.
    /// </summary>
    string Nome { get; }
}
