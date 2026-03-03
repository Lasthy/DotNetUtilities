using DualDbUtilities.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DualDbUtilities.Tests;

public class DualDbContextTests : IAsyncLifetime
{
    private readonly string _tempFile;
    private readonly string _finalFile;
    private DualDbOptions _options = null!;
    private DualDbSyncCoordinator _coordinator = null!;

    public DualDbContextTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"dualdb_temp_{Guid.NewGuid()}.db");
        _finalFile = Path.Combine(Path.GetTempPath(), $"dualdb_final_{Guid.NewGuid()}.db");
    }

    public async Task InitializeAsync()
    {
        _options = new DualDbOptions
        {
            SqliteConnectionString = $"Data Source={_tempFile}",
            SqlServerConnectionString = $"Data Source={_finalFile}",
            AssembliesParaEscanear = [typeof(ProdutoTeste).Assembly],
            TamanhoBatch = 100
        };

        _coordinator = new DualDbSyncCoordinator();

        // Create both databases using SQLite for testing
        await using var tempDb = CreateTempContext();
        await using var finalDb = CreateFinalContext();
        await tempDb.Database.EnsureCreatedAsync();
        await finalDb.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync()
    {
        TryDelete(_tempFile);
        TryDelete(_finalFile);
        return Task.CompletedTask;
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* best-effort cleanup */ }
    }

    private TemporarioDbContext CreateTempContext()
    {
        var options = new DbContextOptionsBuilder<TemporarioDbContext>()
            .UseSqlite(_options.SqliteConnectionString)
            .Options;
        return new TemporarioDbContext(options, _options);
    }

    private FinalDbContext CreateFinalContext()
    {
        var options = new DbContextOptionsBuilder<FinalDbContext>()
            .UseSqlite(_options.SqlServerConnectionString) // SQLite for testing
            .Options;
        return new FinalDbContext(options, _options);
    }

    private DualDbContext CreateDualContext()
    {
        var logger = LoggerFactory
            .Create(b => b.SetMinimumLevel(LogLevel.Debug))
            .CreateLogger<DualDbContext>();

        return new DualDbContext(
            CreateTempContext(),
            CreateFinalContext(),
            _options,
            _coordinator,
            logger);
    }

    // ────────────────────────────────────────────────────────────
    // Testes
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeveAdicionarEntidadeNoBancoTemporario()
    {
        var dual = CreateDualContext();
        var categoria = new CategoriaTeste { Id = 1, Nome = "Eletrônicos" };

        await dual.AdicionarAsync(categoria);

        await using var tempDb = CreateTempContext();
        var saved = await tempDb.Set<CategoriaTeste>().FirstOrDefaultAsync();
        Assert.NotNull(saved);
        Assert.Equal("Eletrônicos", saved.Nome);
    }

    [Fact]
    public async Task DeveConsultarDoBancoFinal()
    {
        await using (var finalDb = CreateFinalContext())
        {
            finalDb.Set<CategoriaTeste>().Add(new CategoriaTeste { Id = 1, Nome = "Roupas" });
            await finalDb.SaveChangesAsync();
        }

        var dual = CreateDualContext();
        var categorias = await dual.Consultar<CategoriaTeste>().ToListAsync();

        Assert.Single(categorias);
        Assert.Equal("Roupas", categorias[0].Nome);
    }

    [Fact]
    public async Task DeveSincronizarDadosDoTemporarioParaFinal()
    {
        var dual = CreateDualContext();
        await dual.AdicionarAsync(new CategoriaTeste { Id = 1, Nome = "Livros" });

        await dual.SincronizarAsync();

        await using var finalDb = CreateFinalContext();
        var synced = await finalDb.Set<CategoriaTeste>().FirstOrDefaultAsync();
        Assert.NotNull(synced);
        Assert.Equal("Livros", synced.Nome);

        await using var tempDb = CreateTempContext();
        Assert.Equal(0, await tempDb.Set<CategoriaTeste>().CountAsync());
    }

    [Fact]
    public async Task DeveFazerUpsertNaSincronizacao()
    {
        // Insere registro existente no final
        await using (var finalDb = CreateFinalContext())
        {
            finalDb.Set<CategoriaTeste>().Add(new CategoriaTeste { Id = 1, Nome = "Original" });
            await finalDb.SaveChangesAsync();
        }

        // Mesmo ID no temporário com valor diferente
        var dual = CreateDualContext();
        await dual.AdicionarAsync(new CategoriaTeste { Id = 1, Nome = "Atualizado" });

        await dual.SincronizarAsync();

        await using var finalDb2 = CreateFinalContext();
        var all = await finalDb2.Set<CategoriaTeste>().ToListAsync();
        Assert.Single(all);
        Assert.Equal("Atualizado", all[0].Nome);
    }

    [Fact]
    public async Task DeveSincronizarComRelacionamentosFK()
    {
        var dual = CreateDualContext();

        await dual.AdicionarAsync(new CategoriaTeste { Id = 1, Nome = "Tech" });
        await dual.AdicionarAsync(new ProdutoTeste { Id = 1, Nome = "Notebook", Preco = 3500m, CategoriaId = 1 });

        await dual.SincronizarAsync();

        await using var finalDb = CreateFinalContext();
        var cat = await finalDb.Set<CategoriaTeste>()
            .Include(c => c.Produtos)
            .FirstOrDefaultAsync();

        Assert.NotNull(cat);
        Assert.Single(cat.Produtos);
        Assert.Equal("Notebook", cat.Produtos[0].Nome);
    }

    [Fact]
    public async Task DeveSincronizarVariosRegistrosEmBatch()
    {
        _options.TamanhoBatch = 2; // batch pequeno para testar paginação
        var dual = CreateDualContext();

        for (int i = 1; i <= 5; i++)
            await dual.AdicionarAsync(new CategoriaTeste { Id = i, Nome = $"Cat {i}" });

        await dual.SincronizarAsync();

        await using var finalDb = CreateFinalContext();
        Assert.Equal(5, await finalDb.Set<CategoriaTeste>().CountAsync());
    }

    [Fact]
    public async Task DeveLimparTemporarioAposSincronizacao()
    {
        var dual = CreateDualContext();
        await dual.AdicionarAsync(new CategoriaTeste { Id = 1, Nome = "Cat" });
        await dual.AdicionarAsync(new ProdutoTeste { Id = 1, Nome = "Prod", Preco = 10m, CategoriaId = 1 });

        await dual.SincronizarAsync();

        await using var tempDb = CreateTempContext();
        Assert.Equal(0, await tempDb.Set<CategoriaTeste>().CountAsync());
        Assert.Equal(0, await tempDb.Set<ProdutoTeste>().CountAsync());
    }

    [Fact]
    public async Task DeveSerThreadSafe_EscritasConcorrentes()
    {
        var tasks = new List<Task>();

        for (int i = 1; i <= 10; i++)
        {
            var id = i;
            tasks.Add(Task.Run(async () =>
            {
                var dual = CreateDualContext();
                await dual.AdicionarAsync(new CategoriaTeste { Id = id, Nome = $"Thread {id}" });
            }));
        }

        await Task.WhenAll(tasks);

        await using var tempDb = CreateTempContext();
        Assert.Equal(10, await tempDb.Set<CategoriaTeste>().CountAsync());

        // Sync after concurrent writes must succeed
        var dualSync = CreateDualContext();
        await dualSync.SincronizarAsync();

        await using var finalDb = CreateFinalContext();
        Assert.Equal(10, await finalDb.Set<CategoriaTeste>().CountAsync());
    }

    [Fact]
    public async Task DeveAdicionarVariosDeUmaVez()
    {
        var dual = CreateDualContext();
        var categorias = Enumerable.Range(1, 5)
            .Select(i => new CategoriaTeste { Id = i, Nome = $"Batch {i}" });

        await dual.AdicionarVariosAsync(categorias);

        await using var tempDb = CreateTempContext();
        Assert.Equal(5, await tempDb.Set<CategoriaTeste>().CountAsync());
    }

    [Fact]
    public async Task NaoDeveSincronizarQuandoTemporarioVazio()
    {
        var dual = CreateDualContext();
        await dual.SincronizarAsync(); // should not throw

        await using var finalDb = CreateFinalContext();
        Assert.Equal(0, await finalDb.Set<CategoriaTeste>().CountAsync());
    }

    // ────────────────────────────────────────────────────────────
    // Upsert no Temporário
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeveAtualizarNoTemporarioQuandoPKJaExiste()
    {
        var dual = CreateDualContext();
        await dual.AdicionarAsync(new CategoriaTeste { Id = 1, Nome = "Original" });
        await dual.AdicionarAsync(new CategoriaTeste { Id = 1, Nome = "Atualizado" });

        await using var tempDb = CreateTempContext();
        var all = await tempDb.Set<CategoriaTeste>().ToListAsync();
        Assert.Single(all);
        Assert.Equal("Atualizado", all[0].Nome);
    }

    [Fact]
    public async Task DeveAtualizarVariosNoTemporarioQuandoPKJaExiste()
    {
        var dual = CreateDualContext();
        await dual.AdicionarVariosAsync(new[]
        {
            new CategoriaTeste { Id = 1, Nome = "Cat A" },
            new CategoriaTeste { Id = 2, Nome = "Cat B" }
        });

        // Atualiza uma e insere outra
        await dual.AdicionarVariosAsync(new[]
        {
            new CategoriaTeste { Id = 1, Nome = "Cat A v2" },
            new CategoriaTeste { Id = 3, Nome = "Cat C" }
        });

        await using var tempDb = CreateTempContext();
        var all = await tempDb.Set<CategoriaTeste>().OrderBy(c => c.Id).ToListAsync();
        Assert.Equal(3, all.Count);
        Assert.Equal("Cat A v2", all[0].Nome);
        Assert.Equal("Cat B", all[1].Nome);
        Assert.Equal("Cat C", all[2].Nome);
    }

    // ────────────────────────────────────────────────────────────
    // Placeholder
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task PlaceholderNaoDeveSobrescreverEntidadeCompletaNoTemporario()
    {
        var dual = CreateDualContext();

        // Insere entidade completa
        await dual.AdicionarAsync(new AlunoTeste
        {
            Id = 1, Nome = "João", Email = "joao@email.com", EhPlaceholder = false
        });

        // Tenta sobrescrever com placeholder — deve ser ignorado
        await dual.AdicionarAsync(new AlunoTeste
        {
            Id = 1, Nome = "Placeholder", EhPlaceholder = true
        });

        await using var tempDb = CreateTempContext();
        var aluno = await tempDb.Set<AlunoTeste>().FindAsync(1);
        Assert.NotNull(aluno);
        Assert.Equal("João", aluno.Nome);
        Assert.Equal("joao@email.com", aluno.Email);
        Assert.False(aluno.EhPlaceholder);
    }

    [Fact]
    public async Task EntidadeCompletaDeveSobrescreverPlaceholderNoTemporario()
    {
        var dual = CreateDualContext();

        // Insere placeholder
        await dual.AdicionarAsync(new AlunoTeste
        {
            Id = 1, Nome = "Placeholder", EhPlaceholder = true
        });

        // Sobrescreve com entidade completa
        await dual.AdicionarAsync(new AlunoTeste
        {
            Id = 1, Nome = "João", Email = "joao@email.com", EhPlaceholder = false
        });

        await using var tempDb = CreateTempContext();
        var aluno = await tempDb.Set<AlunoTeste>().FindAsync(1);
        Assert.NotNull(aluno);
        Assert.Equal("João", aluno.Nome);
        Assert.False(aluno.EhPlaceholder);
    }

    [Fact]
    public async Task PlaceholderDeveSobrescreverOutroPlaceholderNoTemporario()
    {
        var dual = CreateDualContext();

        await dual.AdicionarAsync(new AlunoTeste
        {
            Id = 1, Nome = "Placeholder v1", EhPlaceholder = true
        });

        await dual.AdicionarAsync(new AlunoTeste
        {
            Id = 1, Nome = "Placeholder v2", EhPlaceholder = true
        });

        await using var tempDb = CreateTempContext();
        var aluno = await tempDb.Set<AlunoTeste>().FindAsync(1);
        Assert.NotNull(aluno);
        Assert.Equal("Placeholder v2", aluno.Nome);
    }

    [Fact]
    public async Task PlaceholderNaoDeveSobrescreverEntidadeCompletaNoFinalNaSync()
    {
        // Insere entidade completa diretamente no final
        await using (var finalDb = CreateFinalContext())
        {
            finalDb.Set<AlunoTeste>().Add(new AlunoTeste
            {
                Id = 1, Nome = "João Completo", Email = "joao@email.com", EhPlaceholder = false
            });
            await finalDb.SaveChangesAsync();
        }

        // Adiciona placeholder no temporário
        var dual = CreateDualContext();
        await dual.AdicionarAsync(new AlunoTeste
        {
            Id = 1, Nome = "Placeholder", EhPlaceholder = true
        });

        await dual.SincronizarAsync();

        // O registro completo no final não deve ter sido sobrescrito
        await using var finalDb2 = CreateFinalContext();
        var aluno = await finalDb2.Set<AlunoTeste>().FindAsync(1);
        Assert.NotNull(aluno);
        Assert.Equal("João Completo", aluno.Nome);
        Assert.False(aluno.EhPlaceholder);
    }

    [Fact]
    public async Task EntidadeCompletaDeveSobrescreverPlaceholderNoFinalNaSync()
    {
        // Insere placeholder diretamente no final
        await using (var finalDb = CreateFinalContext())
        {
            finalDb.Set<AlunoTeste>().Add(new AlunoTeste
            {
                Id = 1, Nome = "Placeholder", EhPlaceholder = true
            });
            await finalDb.SaveChangesAsync();
        }

        // Adiciona entidade completa no temporário
        var dual = CreateDualContext();
        await dual.AdicionarAsync(new AlunoTeste
        {
            Id = 1, Nome = "João Completo", Email = "joao@email.com", EhPlaceholder = false
        });

        await dual.SincronizarAsync();

        await using var finalDb2 = CreateFinalContext();
        var aluno = await finalDb2.Set<AlunoTeste>().FindAsync(1);
        Assert.NotNull(aluno);
        Assert.Equal("João Completo", aluno.Nome);
        Assert.False(aluno.EhPlaceholder);
    }

    [Fact]
    public async Task EntidadeSemPlaceholderInterfaceDeveFazerUpsertNormalmente()
    {
        var dual = CreateDualContext();

        // CategoriaTeste não implementa IEntidadePlaceholder — upsert puro
        await dual.AdicionarAsync(new CategoriaTeste { Id = 1, Nome = "Original" });
        await dual.AdicionarAsync(new CategoriaTeste { Id = 1, Nome = "Atualizado" });

        await using var tempDb = CreateTempContext();
        var cat = await tempDb.Set<CategoriaTeste>().FindAsync(1);
        Assert.NotNull(cat);
        Assert.Equal("Atualizado", cat.Nome);
    }
}
