namespace DualDbUtilities;

/// <summary>
/// Interface opcional para entidades que podem existir como "placeholder" (dados incompletos).
/// <para>
/// Regras de precedência:
/// <list type="bullet">
///   <item><description>Entidade completa pode sobrescrever placeholder ✓</description></item>
///   <item><description>Entidade completa pode sobrescrever entidade completa ✓</description></item>
///   <item><description>Placeholder pode sobrescrever placeholder ✓</description></item>
///   <item><description>Placeholder <b>NÃO</b> pode sobrescrever entidade completa ✗</description></item>
/// </list>
/// </para>
/// </summary>
/// <example>
/// <code>
/// public class Aluno : IEntidade, IEntidadePlaceholder
/// {
///     public Guid Id { get; set; }
///     public string Nome { get; set; }
///     public bool EhPlaceholder { get; set; }
///
///     public static void Configurar(ModelBuilder builder) { ... }
/// }
/// </code>
/// </example>
public interface IEntidadePlaceholder
{
    /// <summary>
    /// Indica se esta entidade é um placeholder (dados incompletos).
    /// Quando <c>true</c>, esta entidade não sobrescreverá uma entidade completa já existente.
    /// </summary>
    bool EhPlaceholder { get; }
}
