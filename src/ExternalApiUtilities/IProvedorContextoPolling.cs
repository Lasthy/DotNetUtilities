namespace ExternalApiUtilities;

/// <summary>
/// Provedor de contextos para polling multi-tenant.
/// Retorna a lista de contextos (clientes) que devem ser processados a cada ciclo de polling.
/// </summary>
/// <example>
/// <code>
/// public class ProvedorClientes : IProvedorContextoPolling
/// {
///     private readonly DualDbContext _db;
///     public ProvedorClientes(DualDbContext db) => _db = db;
///
///     public async Task&lt;IReadOnlyList&lt;ContextoPolling&gt;&gt; ObterContextosAsync(CancellationToken ct)
///     {
///         var clientes = await _db.Consultar&lt;Cliente&gt;().ToListAsync(ct);
///         return clientes.Select(c => new ContextoPolling
///         {
///             Id = c.Id,
///             ParametrosAdicionais = { ["key"] = c.Chave }
///         }).ToList();
///     }
/// }
/// </code>
/// </example>
public interface IProvedorContextoPolling
{
    /// <summary>
    /// Obtém os contextos (clientes/tenants) a serem processados no ciclo de polling.
    /// </summary>
    Task<IReadOnlyList<ContextoPolling>> ObterContextosAsync(CancellationToken ct = default);
}
