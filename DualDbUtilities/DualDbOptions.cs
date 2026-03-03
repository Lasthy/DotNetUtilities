using System.Reflection;

namespace DualDbUtilities;

/// <summary>
/// Opções de configuração para o sistema DualDb.
/// </summary>
public class DualDbOptions
{
    /// <summary>
    /// Connection string para o banco temporário (SQLite).
    /// Exemplo: "Data Source=temp.db"
    /// </summary>
    public required string SqliteConnectionString { get; set; }

    /// <summary>
    /// Connection string para o banco final (SQL Server).
    /// </summary>
    public required string SqlServerConnectionString { get; set; }

    /// <summary>
    /// Assemblies que serão escaneados em busca de classes que implementam <see cref="IEntidade"/>.
    /// </summary>
    public required Assembly[] AssembliesParaEscanear { get; set; }

    /// <summary>
    /// Tamanho do lote para operações de sincronização em batch.
    /// Padrão: 500.
    /// </summary>
    public int TamanhoBatch { get; set; } = 500;
}
