namespace ExternalApiUtilities;

/// <summary>
/// Representa o contexto de um cliente/tenant para polling multi-tenant.
/// Cada instância define um ID de contexto e parâmetros adicionais que serão
/// mesclados com os parâmetros fixos do <see cref="ConfiguracaoPolling"/>.
/// </summary>
public class ContextoPolling
{
    /// <summary>
    /// Identificador do contexto (tipicamente o ID do cliente/tenant).
    /// Passado para o mapper como <c>clienteId</c>.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Parâmetros adicionais de query string específicos deste contexto.
    /// São mesclados com os <see cref="ConfiguracaoPolling.ParametrosQuery"/> fixos.
    /// </summary>
    public Dictionary<string, string> ParametrosAdicionais { get; set; } = new();
}
