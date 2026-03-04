using ExternalApiUtilities.Tests.Fixtures;
using Xunit;

namespace ExternalApiUtilities.Tests;

public class RespostaMapperTests
{
    [Fact]
    public async Task DeveMapearDtoParaEntidade()
    {
        var mapper = new AlunoExternoMapper();
        var dto = new AlunoExternoDto { Id = 1, NomeCompleto = "João Silva", Faixa = "azul" };

        var entidades = await mapper.MapearAsync(dto, clienteId: 1);

        Assert.Single(entidades);
        Assert.Equal(1, entidades[0].Id);
        Assert.Equal("João Silva", entidades[0].Nome);
        Assert.Equal("azul", entidades[0].Graduacao);
    }

    [Fact]
    public async Task DeveMapearListaDeDtosParaEntidades()
    {
        var mapper = new ListaAlunosMapper();
        var dto = new ListaAlunosDto
        {
            Total = 2,
            Alunos =
            [
                new AlunoExternoDto { Id = 1, NomeCompleto = "Carlos", Faixa = "branca" },
                new AlunoExternoDto { Id = 2, NomeCompleto = "Maria", Faixa = "roxa" }
            ]
        };

        var entidades = await mapper.MapearAsync(dto, clienteId: 1);

        Assert.Equal(2, entidades.Count);
        Assert.Equal("Carlos", entidades[0].Nome);
        Assert.Equal("branca", entidades[0].Graduacao);
        Assert.Equal("Maria", entidades[1].Nome);
        Assert.Equal("roxa", entidades[1].Graduacao);
    }

    [Fact]
    public async Task DeveRetornarListaVaziaQuandoSemAlunos()
    {
        var mapper = new ListaAlunosMapper();
        var dto = new ListaAlunosDto { Total = 0, Alunos = [] };

        var entidades = await mapper.MapearAsync(dto, clienteId: 1);

        Assert.Empty(entidades);
    }
}
