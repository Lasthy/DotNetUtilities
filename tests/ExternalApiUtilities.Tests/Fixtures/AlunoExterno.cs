using DualDbUtilities;
using Microsoft.EntityFrameworkCore;

namespace ExternalApiUtilities.Tests.Fixtures;

/// <summary>
/// Entidade de teste que simula um aluno retornado por uma API externa.
/// </summary>
public class AlunoExterno : IEntidade
{
    public int Id { get; set; }
    public required string Nome { get; set; }
    public string? Graduacao { get; set; }

    public static void Configurar(ModelBuilder builder)
    {
        builder.Entity<AlunoExterno>(e =>
        {
            e.ToTable("AlunosExternos");
            e.HasKey(a => a.Id);
            e.Property(a => a.Nome).HasMaxLength(200).IsRequired();
            e.Property(a => a.Graduacao).HasMaxLength(100);
        });
    }
}

/// <summary>
/// DTO simulando a resposta de uma API de alunos.
/// </summary>
public class AlunoExternoDto
{
    public int Id { get; set; }
    public string NomeCompleto { get; set; } = "";
    public string? Faixa { get; set; }
}

/// <summary>
/// DTO de lista simulando resposta paginada.
/// </summary>
public class ListaAlunosDto
{
    public List<AlunoExternoDto> Alunos { get; set; } = [];
    public int Total { get; set; }
}
