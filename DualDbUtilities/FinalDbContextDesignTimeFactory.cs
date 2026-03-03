using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DualDbUtilities;

/// <summary>
/// Factory base para criação do <see cref="FinalDbContext"/> em tempo de design (migrations).
/// <para>
/// Herde desta classe no seu projeto e implemente <see cref="ConnectionString"/> e <see cref="Assemblies"/>
/// para habilitar <c>dotnet ef migrations</c> contra o banco final.
/// </para>
/// </summary>
/// <example>
/// <code>
/// public class MinhaDesignTimeFactory : FinalDbContextDesignTimeFactory
/// {
///     protected override string ConnectionString => "Server=localhost;Database=MeuDb;Trusted_Connection=True;";
///     protected override Assembly[] Assemblies => [typeof(MinhaEntidade).Assembly];
/// }
/// </code>
/// Uso:
/// <c>dotnet ef migrations add Initial --context FinalDbContext --project MeuProjeto</c>
/// </example>
public abstract class FinalDbContextDesignTimeFactory : IDesignTimeDbContextFactory<FinalDbContext>
{
    /// <summary>
    /// Connection string do SQL Server para uso em design-time.
    /// </summary>
    protected abstract string ConnectionString { get; }

    /// <summary>
    /// Assemblies contendo as entidades que implementam <see cref="IEntidade"/>.
    /// </summary>
    protected abstract Assembly[] Assemblies { get; }

    public FinalDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<FinalDbContext>();
        optionsBuilder.UseSqlServer(ConnectionString);

        var dualOptions = new DualDbOptions
        {
            SqliteConnectionString = string.Empty,
            SqlServerConnectionString = ConnectionString,
            AssembliesParaEscanear = Assemblies
        };

        return new FinalDbContext(optionsBuilder.Options, dualOptions);
    }
}
