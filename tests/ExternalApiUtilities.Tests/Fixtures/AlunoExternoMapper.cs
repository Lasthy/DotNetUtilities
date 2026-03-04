namespace ExternalApiUtilities.Tests.Fixtures;

/// <summary>
/// Mapper de teste: converte AlunoExternoDto → AlunoExterno.
/// </summary>
public class AlunoExternoMapper : IRespostaMapper<AlunoExternoDto, AlunoExterno>
{
    public Task<IReadOnlyList<AlunoExterno>> MapearAsync(AlunoExternoDto resposta, int clienteId, CancellationToken ct = default)
    {
        var lista = new List<AlunoExterno>
        {
            new()
            {
                Id = resposta.Id,
                Nome = resposta.NomeCompleto,
                Graduacao = resposta.Faixa
            }
        };
        return Task.FromResult<IReadOnlyList<AlunoExterno>>(lista);
    }
}

/// <summary>
/// Mapper de teste: converte ListaAlunosDto → vários AlunoExterno.
/// </summary>
public class ListaAlunosMapper : IRespostaMapper<ListaAlunosDto, AlunoExterno>
{
    public Task<IReadOnlyList<AlunoExterno>> MapearAsync(ListaAlunosDto resposta, int clienteId, CancellationToken ct = default)
    {
        IReadOnlyList<AlunoExterno> lista = resposta.Alunos.Select(a => new AlunoExterno
        {
            Id = a.Id,
            Nome = a.NomeCompleto,
            Graduacao = a.Faixa
        }).ToList();
        return Task.FromResult(lista);
    }
}
