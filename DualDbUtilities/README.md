# DualDbUtilities

Biblioteca para gerenciamento de **banco de dados dual** com Entity Framework Core — escrita rápida em SQLite (temporário) e leitura/persistência em SQL Server (final), com sincronização transacional e thread-safe.

## Conceito

```
┌─────────────────┐        ┌─────────────────┐
│   Temporário    │  sync  │      Final      │
│    (SQLite)     │ ────►  │  (SQL Server)   │
│                 │        │                 │
│  Escrita rápida │        │  Leitura / SoT  │
└─────────────────┘        └─────────────────┘
```

- **Escrita** vai para o banco temporário (SQLite) — rápido e local.
- **Leitura** vem do banco final (SQL Server) — source of truth.
- **Sincronização** transfere tudo do temporário para o final via upsert transacional.

## Instalação

Adicione a referência ao projeto:

```xml
<ProjectReference Include="..\DualDbUtilities\DualDbUtilities.csproj" />
```

Dependências (já incluídas no pacote):
- `Microsoft.EntityFrameworkCore` 8.0
- `Microsoft.EntityFrameworkCore.Sqlite` 8.0
- `Microsoft.EntityFrameworkCore.SqlServer` 8.0

## Uso

### 1. Definir entidades

Implemente `IEntidade` nas suas classes de domínio. A configuração do EF Core é feita diretamente na entidade via `Configurar`:

```csharp
using DualDbUtilities;
using Microsoft.EntityFrameworkCore;

public class Produto : IEntidade
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public Guid CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;

    public static void Configurar(ModelBuilder builder)
    {
        builder.Entity<Produto>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Nome).HasMaxLength(200).IsRequired();
            e.HasOne(p => p.Categoria)
                .WithMany(c => c.Produtos)
                .HasForeignKey(p => p.CategoriaId);
        });
    }
}
```

### 2. Registrar no DI

No `Program.cs` (ou `Startup.cs`):

```csharp
builder.Services.AddDualDb(options =>
{
    options.SqliteConnectionString = "Data Source=temp.db";
    options.SqlServerConnectionString = builder.Configuration.GetConnectionString("Default")!;
    options.AssembliesParaEscanear = [typeof(Produto).Assembly];
    options.TamanhoBatch = 500; // opcional, padrão 500
});
```

Isso registra automaticamente:
| Serviço | Lifetime | Descrição |
|---|---|---|
| `DualDbOptions` | Singleton | Configurações |
| `DualDbSyncCoordinator` | Singleton | Lock de sincronização |
| `TemporarioDbContext` | Scoped | DbContext SQLite |
| `FinalDbContext` | Scoped | DbContext SQL Server |
| `DualDbContext` | Scoped | Facade principal |
| `DualDbInitializer` | HostedService | Cria o SQLite + WAL na inicialização |

### 3. Usar o DualDbContext

Injete `DualDbContext` onde precisar:

```csharp
public class ProdutoService(DualDbContext dual)
{
    // Leitura — vem do banco final
    public async Task<List<Produto>> ListarAsync()
        => await dual.Consultar<Produto>().ToListAsync();

    public async Task<Produto?> BuscarAsync(Guid id)
        => await dual.BuscarAsync<Produto>(id);

    // Escrita — vai para o banco temporário
    public async Task CriarAsync(Produto produto)
        => await dual.AdicionarAsync(produto);

    public async Task CriarVariosAsync(IEnumerable<Produto> produtos)
        => await dual.AdicionarVariosAsync(produtos);

    public async Task AtualizarAsync(Produto produto)
        => await dual.AtualizarAsync(produto);

    public async Task RemoverAsync(Produto produto)
        => await dual.RemoverAsync(produto);
}
```

### 4. Sincronizar

Chame `SincronizarAsync` quando quiser transferir os dados do temporário para o final:

```csharp
public class SyncJob(DualDbContext dual)
{
    public async Task ExecutarAsync(CancellationToken ct)
    {
        await dual.SincronizarAsync(ct);
    }
}
```

A sincronização:
- Respeita a **ordem de FKs** (pais inseridos antes de filhos).
- Faz **upsert** — registros existentes são atualizados, novos são inseridos.
- Processa em **batches** configuráveis para evitar problemas de memória.
- Roda dentro de uma **transaction** no banco final com rollback em caso de erro.
- **Limpa** o banco temporário após sucesso (filhos antes de pais).
- É **thread-safe** — escritas concorrentes são permitidas, mas a sincronização bloqueia novas escritas e aguarda as em andamento.

### 5. Acesso avançado

Para cenários que precisam de acesso direto aos `DbSet`:

```csharp
// Acesso direto ao set temporário
var tempSet = dual.TemporarioSet<Produto>();
tempSet.Add(produto);
await dual.SalvarTemporarioAsync();

// Acesso direto ao set final (leitura)
var finalSet = dual.FinalSet<Produto>();
var count = await finalSet.CountAsync();
```

## Migrations

Apenas **uma migration** é necessária, contra o `FinalDbContext`. O banco SQLite temporário é criado automaticamente via `EnsureCreated()` na inicialização.

### Configurar Design-Time Factory

Crie uma classe no seu projeto que herda de `FinalDbContextDesignTimeFactory`:

```csharp
using System.Reflection;
using DualDbUtilities;

public class DesignTimeFactory : FinalDbContextDesignTimeFactory
{
    protected override string ConnectionString
        => "Server=localhost;Database=MeuDb;Trusted_Connection=True;TrustServerCertificate=True;";

    protected override Assembly[] Assemblies
        => [typeof(Produto).Assembly];
}
```

### Gerar migrations

```bash
dotnet ef migrations add Inicial --context FinalDbContext --project SeuProjeto
```

### Aplicar migrations

```bash
dotnet ef database update --context FinalDbContext --project SeuProjeto
```

## Thread Safety

O `DualDbSyncCoordinator` implementa um padrão **async reader-writer lock**:

- **Escritas** (`AdicionarAsync`, `AtualizarAsync`, etc.) adquirem um **lock compartilhado** — múltiplas threads podem escrever simultaneamente.
- **Sincronização** (`SincronizarAsync`) adquire um **lock exclusivo** — aguarda todas as escritas em andamento finalizarem e bloqueia novas escritas até concluir.

Isso garante que nenhum dado será perdido durante a transferência.

## Foreign Keys no Banco Temporário

O banco temporário (SQLite) tem as **constraints de FK desabilitadas** propositalmente via `DesabilitarFKInterceptor`. Isso significa que:

- Você pode inserir entidades no temporário que referenciam FKs de registros que **só existem no banco final** — sem erros.
- Isso é essencial no cenário dual: um `Produto` pode referenciar `CategoriaId = 10`, onde a `Categoria` já foi sincronizada e só existe no final.
- A integridade referencial é **validada durante a sincronização**, quando os dados chegam ao banco final (SQL Server), que mantém FK constraints ativas.

```csharp
// Categoria existe apenas no banco final (já foi sincronizada anteriormente)
// Inserir Produto no temporário com FK para ela funciona normalmente:
await dual.AdicionarAsync(new Produto { Id = 1, Nome = "Notebook", CategoriaId = 10 });

// Na sincronização, o SQL Server validará que CategoriaId = 10 existe
await dual.SincronizarAsync();
```

> **Nota:** Se o banco temporário for acessado manualmente (via `TemporarioSet<T>()`), lembre-se que não há validação de FK — é responsabilidade do chamador garantir que os dados fazem sentido para a sincronização.

## Placeholder e Identificação por Nome

### `IEntidadePlaceholder`

Interface opcional para marcar entidades com dados incompletos:

```csharp
public class Aluno : IEntidade, IEntidadePlaceholder
{
    public int Id { get; set; }
    public string Nome { get; set; }
    public bool EhPlaceholder { get; set; }
    // ...
}
```

Regras de precedência:

| Incoming | Existing | Resultado |
|---|---|---|
| Completa | Placeholder | ✅ Sobrescreve |
| Completa | Completa | ✅ Sobrescreve |
| Placeholder | Placeholder | ✅ Sobrescreve |
| Placeholder | Completa | ❌ Ignorado |

Estas regras se aplicam tanto na escrita no temporário quanto na sincronização para o final.

### `IIdentificavelPorNome`

Interface para entidades com um nome como identificador natural:

```csharp
public class Aluno : IEntidade, IEntidadePlaceholder, IIdentificavelPorNome
{
    public int Id { get; set; }
    public string Nome { get; set; }
    public bool EhPlaceholder { get; set; }
    // ...
}
```

Funcionalidades:
- **Índice automático** na coluna `Nome` (criado no `OnModelCreating`).
- **Remapeamento de PK**: quando combinada com `IEntidadePlaceholder`, habilita a troca de chave primária com cascata em FKs.

### Remapeamento de PK

Cenário típico:

```
1. Placeholder inserido: Id = -1, Nome = "João", EhPlaceholder = true
2. Matrícula referencia:  AlunoId = -1
3. Entidade real chega:   Id = 42, Nome = "João", EhPlaceholder = false
```

O DualDb detecta o placeholder pelo `Nome`, e automaticamente:
- Atualiza a PK do Aluno de `-1` para `42`
- Atualiza `AlunoId` de `-1` para `42` em todas as Matrículas
- Atualiza os demais campos com os dados reais

```csharp
// Uso transparente — a lógica é automática no AdicionarAsync
await dual.AdicionarAsync(new Aluno { Id = -1, Nome = "João", EhPlaceholder = true });
await dual.AdicionarAsync(new Matricula { Id = 1, AlunoId = -1, Turma = "A" });

// Quando a entidade real chega, o remap acontece automaticamente
await dual.AdicionarAsync(new Aluno { Id = 42, Nome = "João", Email = "joao@email.com", EhPlaceholder = false });
// Agora: Aluno.Id = 42, Matricula.AlunoId = 42
```

## API Reference

### `DualDbContext`

| Método | Descrição |
|---|---|
| `Consultar<T>()` | `IQueryable<T>` do banco final (NoTracking) |
| `BuscarAsync<T>(params object[])` | Busca por PK no banco final |
| `AdicionarAsync<T>(T)` | Insere no banco temporário |
| `AdicionarVariosAsync<T>(IEnumerable<T>)` | Insere vários no banco temporário |
| `AtualizarAsync<T>(T)` | Atualiza no banco temporário |
| `RemoverAsync<T>(T)` | Remove do banco temporário |
| `TemporarioSet<T>()` | Acesso direto ao DbSet temporário |
| `FinalSet<T>()` | Acesso direto ao DbSet final |
| `SalvarTemporarioAsync()` | Salva alterações pendentes no temporário |
| `SincronizarAsync()` | Transfere tudo do temporário para o final |

### `DualDbOptions`

| Propriedade | Tipo | Descrição |
|---|---|---|
| `SqliteConnectionString` | `string` | Connection string do SQLite |
| `SqlServerConnectionString` | `string` | Connection string do SQL Server |
| `AssembliesParaEscanear` | `Assembly[]` | Assemblies com as entidades `IEntidade` |
| `TamanhoBatch` | `int` | Tamanho do lote de sincronização (padrão: 500) |
