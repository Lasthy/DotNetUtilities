using DualDbUtilities;
using Microsoft.EntityFrameworkCore;

namespace DualDbUtilities.Tests.Fixtures;

public class AlunoTeste : IEntidade, IEntidadePlaceholder
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool EhPlaceholder { get; set; }

    public static void Configurar(ModelBuilder builder)
    {
        builder.Entity<AlunoTeste>(e =>
        {
            e.ToTable("Alunos");
            e.HasKey(a => a.Id);
            e.Property(a => a.Nome).HasMaxLength(200).IsRequired();
            e.Property(a => a.Email).HasMaxLength(300);
        });
    }
}
