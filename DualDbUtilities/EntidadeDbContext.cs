using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace DualDbUtilities;

/// <summary>
/// DbContext base que escaneia assemblies e registra automaticamente todas as entidades
/// que implementam <see cref="IEntidade"/>.
/// </summary>
public abstract class EntidadeDbContext : DbContext
{
    private readonly Assembly[] _assemblies;

    protected EntidadeDbContext(DbContextOptions options, Assembly[] assemblies) : base(options)
    {
        _assemblies = assemblies ?? [];
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        var tiposEntidade = _assemblies
            .SelectMany(a =>
            {
                try { return a.GetExportedTypes(); }
                catch { return Type.EmptyTypes; }
            })
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && typeof(IEntidade).IsAssignableFrom(t))
            .Distinct();

        foreach (var tipo in tiposEntidade)
        {
            var metodo = tipo.GetMethod(
                nameof(IEntidade.Configurar),
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy,
                null,
                [typeof(ModelBuilder)],
                null);

            metodo?.Invoke(null, [builder]);
        }
    }
}
