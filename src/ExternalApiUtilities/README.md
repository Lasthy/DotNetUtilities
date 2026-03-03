# ExternalApiUtilities

Biblioteca para consumo de **APIs externas** com suporte a múltiplos adaptadores, mapeamento de respostas para entidades, deserializadores customizados (envelopes), e polling periódico com persistência via DualDb.

## Conceito

```
┌──────────────┐         ┌──────────────┐         ┌──────────────┐
│  API Externa │  HTTP   │  ApiAdapter  │  Mapper  │   DualDb     │
│  (endpoint)  │ ◄────►  │  (adaptador) │ ──────►  │  (entidade)  │
└──────────────┘         └──────────────┘         └──────────────┘
                              ▲                        ▲
                              │                        │
                    IDesserializadorResposta     IRespostaMapper
                      (envelope unwrap)         (DTO → Entidade)
```

- **Adapter** faz a chamada HTTP com substituição de parâmetros, query string e corpo JSON.
- **Desserializador** (opcional) extrai dados de envelopes padrão antes da deserialização.
- **Mapper** converte DTOs da API em entidades do domínio (`IEntidade`).
- **Polling** chama endpoints periodicamente e salva no DualDb automaticamente.

## Instalação

Adicione a referência ao projeto:

```xml
<ProjectReference Include="..\ExternalApiUtilities\ExternalApiUtilities.csproj" />
```

Dependências (já incluídas):
- `Microsoft.Extensions.Http` 8.0
- `Microsoft.Extensions.Hosting.Abstractions` 8.0
- `DualDbUtilities` (referência de projeto)

## Uso

### 1. Configurar APIs no DI

No `Program.cs`:

```csharp
builder.Services.AddExternalApi(api =>
{
    // Registrar uma API externa
    api.AdicionarApi(config =>
    {
        config.Nome = "academia-api";
        config.UrlBase = "https://api.academia.com";
        config.Timeout = TimeSpan.FromSeconds(15);
        config.HeadersPadrao["Authorization"] = "Bearer meu-token";
        config.Rotas.Add(new RotaApi { Nome = "listar-alunos", Caminho = "/api/alunos" });
        config.Rotas.Add(new RotaApi { Nome = "buscar-aluno", Caminho = "/api/alunos/{id}" });
        config.Rotas.Add(new RotaApi { Nome = "criar-aluno", Caminho = "/api/alunos", Metodo = HttpMethod.Post });
    });

    // Registrar mappers
    api.AdicionarMapper<ListaAlunosDto, Aluno, AlunoMapper>();

    // Registrar polling (opcional)
    api.AdicionarPolling<ListaAlunosDto, Aluno>(polling =>
    {
        polling.Nome = "polling-alunos";
        polling.NomeApi = "academia-api";
        polling.NomeRota = "listar-alunos";
        polling.Intervalo = TimeSpan.FromMinutes(5);
    });
});
```

Isso registra automaticamente:

| Serviço | Lifetime | Descrição |
|---|---|---|
| `ConfiguracaoApi` | Singleton | Configuração de cada API |
| `IApiAdapterFactory` | Singleton | Fábrica de adaptadores por nome |
| `IRespostaMapper<TResp, TEnt>` | Transient | Conversor DTO → Entidade |
| `ServicoPollingApi<TResp, TEnt>` | HostedService | Polling periódico |
| Named `HttpClient` | Via Factory | Um por API, com base URL e headers |

### 2. Fazer requisições

Injete `IApiAdapterFactory` e resolva o adaptador por nome:

```csharp
public class AlunoService(IApiAdapterFactory adapterFactory)
{
    public async Task<List<AlunoDto>?> ListarAsync(CancellationToken ct)
    {
        var adapter = adapterFactory.Obter("academia-api");
        var resposta = await adapter.EnviarAsync<List<AlunoDto>>("listar-alunos", ct: ct);

        if (!resposta.Sucesso)
            throw new Exception($"Erro ao listar alunos: {resposta.MensagemErro}");

        return resposta.Dados;
    }

    public async Task<AlunoDto?> BuscarAsync(int id, CancellationToken ct)
    {
        var adapter = adapterFactory.Obter("academia-api");
        var resposta = await adapter.EnviarAsync<AlunoDto>("buscar-aluno",
            parametrosCaminho: new() { ["id"] = id.ToString() },
            ct: ct);

        return resposta.Dados;
    }

    public async Task CriarAsync(CriarAlunoRequest request, CancellationToken ct)
    {
        var adapter = adapterFactory.Obter("academia-api");
        var resposta = await adapter.EnviarAsync("criar-aluno", corpo: request, ct: ct);

        if (!resposta.Sucesso)
            throw new Exception($"Erro ao criar aluno: {resposta.MensagemErro}");
    }
}
```

### 3. Parâmetros de caminho e query string

Placeholders entre chaves no caminho são substituídos por `parametrosCaminho`:

```csharp
// Rota: "/api/alunos/{id}/treinos/{treinoId}"
var resposta = await adapter.EnviarAsync<TreinoDto>("buscar-treino",
    parametrosCaminho: new()
    {
        ["id"] = "42",
        ["treinoId"] = "7"
    });
// Resulta em: GET /api/alunos/42/treinos/7
```

Query strings são adicionadas via `parametrosQuery`:

```csharp
var resposta = await adapter.EnviarAsync<List<AlunoDto>>("listar-alunos",
    parametrosQuery: new()
    {
        ["faixa"] = "azul",
        ["pagina"] = "1"
    });
// Resulta em: GET /api/alunos?faixa=azul&pagina=1
```

### 4. Mappers

Implemente `IRespostaMapper<TResposta, TEntidade>` para converter DTOs da API em entidades do domínio:

```csharp
public class AlunoMapper : IRespostaMapper<ListaAlunosDto, Aluno>
{
    public IEnumerable<Aluno> Mapear(ListaAlunosDto resposta)
    {
        return resposta.Alunos.Select(dto => new Aluno
        {
            Id = dto.Id,
            Nome = dto.NomeCompleto,
            Graduacao = dto.Faixa
        });
    }
}
```

Registre com:

```csharp
api.AdicionarMapper<ListaAlunosDto, Aluno, AlunoMapper>();
```

O mapper é usado automaticamente pelo serviço de polling, mas também pode ser injetado manualmente via `IRespostaMapper<TResposta, TEntidade>`.

### 5. Deserializador customizado (envelope)

Quando a API retorna um envelope padrão:

```json
{
  "sucesso": true,
  "dados": { "id": 1, "nome": "Carlos" },
  "mensagem": "OK"
}
```

Crie um `IDesserializadorResposta` para extrair os dados automaticamente:

```csharp
public class EnvelopePadrao<T>
{
    public bool Sucesso { get; set; }
    public T? Dados { get; set; }
    public string? Mensagem { get; set; }
}

public class DesserializadorComEnvelope : IDesserializadorResposta
{
    public T? Desserializar<T>(string conteudo, JsonSerializerOptions options)
    {
        var envelope = JsonSerializer.Deserialize<EnvelopePadrao<T>>(conteudo, options);
        return envelope is { Sucesso: true } ? envelope.Dados : default;
    }
}
```

Configure na API:

```csharp
api.AdicionarApi(config =>
{
    config.Nome = "academia-api";
    config.UrlBase = "https://api.academia.com";
    config.Desserializador = new DesserializadorComEnvelope();
    // ...
});
```

Se nenhum deserializador for configurado, o comportamento padrão é `JsonSerializer.Deserialize<T>` direto.

### 6. Polling periódico

O serviço de polling chama um endpoint em intervalos regulares, mapeia a resposta e salva no DualDb:

```csharp
api.AdicionarPolling<ListaAlunosDto, Aluno>(polling =>
{
    polling.Nome = "polling-alunos";
    polling.NomeApi = "academia-api";
    polling.NomeRota = "listar-alunos";
    polling.Intervalo = TimeSpan.FromMinutes(5);
    polling.ParametrosQuery = new() { ["ativo"] = "true" }; // opcional
});
```

O polling:
- Aguarda 5 segundos após o startup antes da primeira execução.
- Usa `PeriodicTimer` para intervalos precisos.
- Chama `IRespostaMapper<TResposta, TEntidade>.Mapear()` nos dados recebidos.
- Salva via `DualDbContext.AdicionarVariosAsync()` — upsert no banco temporário.
- Loga warnings em caso de falha e continua tentando no próximo ciclo.
- É cancelado graciosamente via `CancellationToken` no shutdown da aplicação.

> **Requer** que o DualDb esteja configurado (`AddDualDb(...)`) para que a persistência funcione.

### 7. Múltiplas APIs

Você pode registrar quantas APIs quiser — cada uma tem seu próprio `HttpClient`, headers e rotas:

```csharp
builder.Services.AddExternalApi(api =>
{
    api.AdicionarApi(config =>
    {
        config.Nome = "pagamentos-api";
        config.UrlBase = "https://api.pagamentos.com";
        config.HeadersPadrao["X-Api-Key"] = "chave-secreta";
        config.Rotas.Add(new RotaApi { Nome = "cobrar", Caminho = "/v1/cobrancas", Metodo = HttpMethod.Post });
    });

    api.AdicionarApi(config =>
    {
        config.Nome = "ranking-api";
        config.UrlBase = "https://api.ranking.com";
        config.Desserializador = new DesserializadorComEnvelope();
        config.Rotas.Add(new RotaApi { Nome = "top-10", Caminho = "/api/ranking/top" });
    });
});

// Depois, resolva por nome:
var pagAdapter = factory.Obter("pagamentos-api");
var rankAdapter = factory.Obter("ranking-api");
```

## Tratamento de Erros

O `ApiAdapter` captura erros de rede e retorna um `RespostaApi` com `Sucesso = false`:

```csharp
var resposta = await adapter.EnviarAsync("listar-alunos");

if (!resposta.Sucesso)
{
    // resposta.CodigoStatus — HTTP status (ou 500 para exceções de rede)
    // resposta.MensagemErro — mensagem de erro ou corpo da resposta
    logger.LogWarning("Falha: {Status} - {Erro}", resposta.CodigoStatus, resposta.MensagemErro);
}
```

Erros de deserialização (JSON inválido) também são capturados e retornam `Sucesso = false` com a mensagem de erro.

## API Reference

### `ConfiguracaoApi`

| Propriedade | Tipo | Descrição |
|---|---|---|
| `Nome` | `string` | Nome identificador da API |
| `UrlBase` | `string` | URL base (ex: `https://api.exemplo.com`) |
| `HeadersPadrao` | `Dictionary<string, string>` | Headers enviados em todas as requisições |
| `Timeout` | `TimeSpan` | Timeout padrão (30s) |
| `Rotas` | `List<RotaApi>` | Endpoints disponíveis |
| `Desserializador` | `IDesserializadorResposta?` | Deserializador customizado (opcional) |

### `RotaApi`

| Propriedade | Tipo | Descrição |
|---|---|---|
| `Nome` | `string` | Nome identificador da rota |
| `Caminho` | `string` | Caminho relativo com placeholders (`/api/{id}`) |
| `Metodo` | `HttpMethod` | Método HTTP (padrão: GET) |

### `IApiAdapter`

| Método | Descrição |
|---|---|
| `EnviarAsync(nomeRota, ...)` | Envia requisição e retorna `RespostaApi` |
| `EnviarAsync<T>(nomeRota, ...)` | Envia e deserializa para `RespostaApi<T>` |

### `RespostaApi` / `RespostaApi<T>`

| Propriedade | Tipo | Descrição |
|---|---|---|
| `Sucesso` | `bool` | Se a requisição retornou status 2xx |
| `CodigoStatus` | `HttpStatusCode` | Código HTTP retornado |
| `ConteudoBruto` | `string?` | Corpo bruto da resposta |
| `MensagemErro` | `string?` | Mensagem de erro (se falhou) |
| `Dados` | `T?` | Dados deserializados (apenas `RespostaApi<T>`) |

### `ConfiguracaoPolling`

| Propriedade | Tipo | Descrição |
|---|---|---|
| `Nome` | `string` | Nome do job de polling |
| `NomeApi` | `string` | Nome da API registrada |
| `NomeRota` | `string` | Nome da rota a ser chamada |
| `Intervalo` | `TimeSpan` | Intervalo entre chamadas |
| `ParametrosCaminho` | `Dictionary<string, string>?` | Parâmetros de caminho fixos |
| `ParametrosQuery` | `Dictionary<string, string>?` | Query string fixa |
