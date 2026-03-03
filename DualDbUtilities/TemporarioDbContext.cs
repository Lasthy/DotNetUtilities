using Microsoft.EntityFrameworkCore;

namespace DualDbUtilities;

/// <summary>
/// Contexto do banco de dados temporário (SQLite).
/// Utilizado para escrita rápida de dados que serão posteriormente sincronizados ao banco final.
/// </summary>
public class TemporarioDbContext : EntidadeDbContext
{
    public TemporarioDbContext(DbContextOptions<TemporarioDbContext> options, DualDbOptions dualDbOptions)
        : base(options, dualDbOptions.AssembliesParaEscanear)
    {
    }
}
