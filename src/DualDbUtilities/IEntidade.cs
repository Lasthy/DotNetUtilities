using Microsoft.EntityFrameworkCore;

namespace DualDbUtilities;

/// <summary>
/// Interface marcadora para entidades do domínio.
/// Classes que implementam esta interface serão automaticamente registradas nos DbContexts do DualDb.
/// </summary>
/// <example>
/// <code>
/// public class Produto : IEntidade
/// {
///     public Guid Id { get; set; }
///     public string Nome { get; set; }
///
///     public static void Configurar(ModelBuilder builder)
///     {
///         builder.Entity&lt;Produto&gt;(e =>
///         {
///             e.HasKey(p => p.Id);
///             e.Property(p => p.Nome).HasMaxLength(200).IsRequired();
///         });
///     }
/// }
/// </code>
/// </example>
public interface IEntidade
{
    /// <summary>
    /// Configura a entidade no ModelBuilder do Entity Framework Core.
    /// Deve chamar <c>builder.Entity&lt;T&gt;()</c> para definir mapeamento, chaves, índices, etc.
    /// </summary>
    /// <param name="builder">O ModelBuilder do EF Core.</param>
    static abstract void Configurar(ModelBuilder builder);
}
