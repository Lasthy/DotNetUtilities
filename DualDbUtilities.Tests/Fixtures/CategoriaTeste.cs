using DualDbUtilities;
using Microsoft.EntityFrameworkCore;

namespace DualDbUtilities.Tests.Fixtures;

public class CategoriaTeste : IEntidade
{
    public int Id { get; set; }
    public required string Nome { get; set; }
    public List<ProdutoTeste> Produtos { get; set; } = [];

    public static void Configurar(ModelBuilder builder)
    {
        builder.Entity<CategoriaTeste>(e =>
        {
            e.ToTable("Categorias");
            e.HasKey(c => c.Id);
            e.Property(c => c.Nome).HasMaxLength(200).IsRequired();
            e.HasMany(c => c.Produtos)
                .WithOne(p => p.Categoria)
                .HasForeignKey(p => p.CategoriaId);
        });
    }
}
