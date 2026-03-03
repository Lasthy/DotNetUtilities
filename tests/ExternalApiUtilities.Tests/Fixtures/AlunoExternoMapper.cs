namespace ExternalApiUtilities.Tests.Fixtures;

/// <summary>
/// Mapper de teste: converte AlunoExternoDto → AlunoExterno.
/// </summary>
public class AlunoExternoMapper : IRespostaMapper<AlunoExternoDto, AlunoExterno>
{
    public IEnumerable<AlunoExterno> Mapear(AlunoExternoDto resposta)
    {
        yield return new AlunoExterno
        {
            Id = resposta.Id,
            Nome = resposta.NomeCompleto,
            Graduacao = resposta.Faixa
        };
    }
}

/// <summary>
/// Mapper de teste: converte ListaAlunosDto → vários AlunoExterno.
/// </summary>
public class ListaAlunosMapper : IRespostaMapper<ListaAlunosDto, AlunoExterno>
{
    public IEnumerable<AlunoExterno> Mapear(ListaAlunosDto resposta)
    {
        return resposta.Alunos.Select(a => new AlunoExterno
        {
            Id = a.Id,
            Nome = a.NomeCompleto,
            Graduacao = a.Faixa
        });
    }
}
