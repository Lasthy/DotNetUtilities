using DualDbUtilities;
using Microsoft.EntityFrameworkCore;

namespace DualDbUtilities.Tests.Fixtures;

/// <summary>
/// Entidade dependente de AlunoTeste — usada para testar cascata de FK durante remap de PK.
/// </summary>
public class MatriculaTeste : IEntidade
{
    public int Id { get; set; }
    public int AlunoId { get; set; }
    public string Turma { get; set; } = string.Empty;
    public AlunoTeste Aluno { get; set; } = null!;

    public static void Configurar(ModelBuilder builder)
    {
        builder.Entity<MatriculaTeste>(e =>
        {
            e.ToTable("Matriculas");
            e.HasKey(m => m.Id);
            e.Property(m => m.Turma).HasMaxLength(100).IsRequired();
        });
    }
}
