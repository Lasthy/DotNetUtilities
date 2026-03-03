namespace ExternalApiUtilities;

/// <summary>
/// Fábrica para resolver <see cref="IApiAdapter"/> por nome da API.
/// Registrada como singleton no container de DI.
/// </summary>
public interface IApiAdapterFactory
{
    /// <summary>
    /// Retorna o adaptador para a API com o nome especificado.
    /// </summary>
    /// <param name="nomeApi">Nome da API configurada.</param>
    /// <exception cref="InvalidOperationException">Se a API não estiver registrada.</exception>
    IApiAdapter Obter(string nomeApi);

    /// <summary>
    /// Retorna todos os adaptadores registrados.
    /// </summary>
    IEnumerable<IApiAdapter> ObterTodos();
}
