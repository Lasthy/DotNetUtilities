using DualDbUtilities;
using Microsoft.EntityFrameworkCore;

namespace DualDbUtilities.Tests.Fixtures;

public class ProdutoTeste : IEntidade
{
    public int Id { get; set; }
    public required string Nome { get; set; }
    public decimal Preco { get; set; }
    public int CategoriaId { get; set; }
    public CategoriaTeste Categoria { get; set; } = null!;

    public static void Configurar(ModelBuilder builder)
    {
        builder.Entity<ProdutoTeste>(e =>
        {
            e.ToTable("Produtos");
            e.HasKey(p => p.Id);
            e.Property(p => p.Nome).HasMaxLength(200).IsRequired();
        });
    }
}
