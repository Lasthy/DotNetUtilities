using Microsoft.EntityFrameworkCore;

namespace DualDbUtilities;

/// <summary>
/// Contexto do banco de dados final (SQL Server).
/// Fonte principal de leitura e destino da sincronização.
/// </summary>
public class FinalDbContext : EntidadeDbContext
{
    public FinalDbContext(DbContextOptions<FinalDbContext> options, DualDbOptions dualDbOptions)
        : base(options, dualDbOptions.AssembliesParaEscanear)
    {
    }
}
