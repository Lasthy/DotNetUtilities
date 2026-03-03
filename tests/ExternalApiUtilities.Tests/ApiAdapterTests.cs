using System.Net;
using System.Text.Json;
using ExternalApiUtilities.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ExternalApiUtilities.Tests;

public class ApiAdapterTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private ConfiguracaoApi CriarConfiguracaoPadrao() => new()
    {
        Nome = "test-api",
        UrlBase = "https://api.teste.com",
        Rotas =
        [
            new RotaApi { Nome = "listar", Caminho = "/api/alunos" },
            new RotaApi { Nome = "buscar", Caminho = "/api/alunos/{id}" },
            new RotaApi { Nome = "criar", Caminho = "/api/alunos", Metodo = HttpMethod.Post },
            new RotaApi { Nome = "filtrar", Caminho = "/api/alunos/busca" }
        ]
    };

    private static ILogger<ApiAdapter> CriarLogger() =>
        LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Debug)).CreateLogger<ApiAdapter>();

    // ─── Testes de resolução de rota ───

    [Fact]
    public async Task DeveResolverRotaPorNome()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, "[]");
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.teste.com") };
        var adapter = new ApiAdapter(client, CriarConfiguracaoPadrao(), CriarLogger());

        var resposta = await adapter.EnviarAsync("listar");

        Assert.True(resposta.Sucesso);
        Assert.Equal(HttpStatusCode.OK, resposta.CodigoStatus);
        Assert.Contains("/api/alunos", handler.UltimaUrl);
    }

    [Fact]
    public async Task DeveLancarExcecaoParaRotaInexistente()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, "ok");
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.teste.com") };
        var adapter = new ApiAdapter(client, CriarConfiguracaoPadrao(), CriarLogger());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => adapter.EnviarAsync("rota-que-nao-existe"));
    }

    [Fact]
    public async Task DeveResolverRotaCaseInsensitive()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, "ok");
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.teste.com") };
        var adapter = new ApiAdapter(client, CriarConfiguracaoPadrao(), CriarLogger());

        var resposta = await adapter.EnviarAsync("LISTAR");

        Assert.True(resposta.Sucesso);
    }

    // ─── Testes de parâmetros de caminho ───

    [Fact]
    public async Task DeveSubstituirParametrosDeCaminho()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, "{}");
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.teste.com") };
        var adapter = new ApiAdapter(client, CriarConfiguracaoPadrao(), CriarLogger());

        var parametros = new Dictionary<string, string> { ["id"] = "42" };
        await adapter.EnviarAsync("buscar", parametrosCaminho: parametros);

        Assert.Contains("/api/alunos/42", handler.UltimaUrl);
    }

    // ─── Testes de query string ───

    [Fact]
    public async Task DeveAdicionarParametrosDeQuery()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, "[]");
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.teste.com") };
        var adapter = new ApiAdapter(client, CriarConfiguracaoPadrao(), CriarLogger());

        var query = new Dictionary<string, string>
        {
            ["nome"] = "João",
            ["faixa"] = "azul"
        };
        await adapter.EnviarAsync("filtrar", parametrosQuery: query);

        Assert.Contains("nome=Jo%C3%A3o", handler.UltimaUrl);
        Assert.Contains("faixa=azul", handler.UltimaUrl);
    }

    // ─── Testes de método HTTP ───

    [Fact]
    public async Task DeveUsarMetodoPostQuandoConfigurado()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.Created, "{}");
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.teste.com") };
        var adapter = new ApiAdapter(client, CriarConfiguracaoPadrao(), CriarLogger());

        var corpo = new { Nome = "Maria", Graduacao = "branca" };
        var resposta = await adapter.EnviarAsync("criar", corpo: corpo);

        Assert.True(resposta.Sucesso);
        Assert.Equal(HttpStatusCode.Created, resposta.CodigoStatus);
        Assert.Equal(HttpMethod.Post, handler.UltimoMetodo);
    }

    // ─── Testes de deserialização ───

    [Fact]
    public async Task DeveDeserializarRespostaComTipo()
    {
        var dto = new AlunoExternoDto { Id = 1, NomeCompleto = "Carlos", Faixa = "roxa" };
        var json = JsonSerializer.Serialize(dto, JsonOptions);

        var handler = new FakeHttpHandler(HttpStatusCode.OK, json);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.teste.com") };
        var adapter = new ApiAdapter(client, CriarConfiguracaoPadrao(), CriarLogger());

        var resposta = await adapter.EnviarAsync<AlunoExternoDto>("buscar",
            parametrosCaminho: new() { ["id"] = "1" });

        Assert.True(resposta.Sucesso);
        Assert.NotNull(resposta.Dados);
        Assert.Equal("Carlos", resposta.Dados.NomeCompleto);
        Assert.Equal("roxa", resposta.Dados.Faixa);
    }

    [Fact]
    public async Task DeveRetornarErroQuandoDeserializacaoFalha()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, "not-valid-json{{{");
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.teste.com") };
        var adapter = new ApiAdapter(client, CriarConfiguracaoPadrao(), CriarLogger());

        var resposta = await adapter.EnviarAsync<AlunoExternoDto>("listar");

        Assert.False(resposta.Sucesso);
        Assert.Contains("Falha ao deserializar", resposta.MensagemErro);
    }

    // ─── Testes de deserializador customizado (envelope) ───

    [Fact]
    public async Task DeveUsarDesserializadorCustomizadoParaExtrairDadosDoEnvelope()
    {
        var envelopeJson = """{"sucesso": true, "dados": {"id": 1, "nomeCompleto": "Carlos", "faixa": "roxa"}, "mensagem": "OK"}""";

        var handler = new FakeHttpHandler(HttpStatusCode.OK, envelopeJson);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.teste.com") };

        var config = CriarConfiguracaoPadrao();
        config.Desserializador = new FakeEnvelopeDesserializador();

        var adapter = new ApiAdapter(client, config, CriarLogger());

        var resposta = await adapter.EnviarAsync<AlunoExternoDto>("listar");

        Assert.True(resposta.Sucesso);
        Assert.NotNull(resposta.Dados);
        Assert.Equal("Carlos", resposta.Dados.NomeCompleto);
        Assert.Equal("roxa", resposta.Dados.Faixa);
    }

    [Fact]
    public async Task DeveUsarDesserializadorPadraoQuandoNenhumCustomizadoConfigurado()
    {
        var dto = new AlunoExternoDto { Id = 5, NomeCompleto = "Ana", Faixa = "preta" };
        var json = JsonSerializer.Serialize(dto, JsonOptions);

        var handler = new FakeHttpHandler(HttpStatusCode.OK, json);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.teste.com") };

        // Sem Desserializador = usa DesserializadorPadrao
        var adapter = new ApiAdapter(client, CriarConfiguracaoPadrao(), CriarLogger());

        var resposta = await adapter.EnviarAsync<AlunoExternoDto>("listar");

        Assert.True(resposta.Sucesso);
        Assert.NotNull(resposta.Dados);
        Assert.Equal("Ana", resposta.Dados.NomeCompleto);
    }

    // ─── Testes de tratamento de erro ───

    [Fact]
    public async Task DeveRetornarErroParaStatus4xx()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.NotFound, "Aluno não encontrado");
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.teste.com") };
        var adapter = new ApiAdapter(client, CriarConfiguracaoPadrao(), CriarLogger());

        var resposta = await adapter.EnviarAsync("buscar",
            parametrosCaminho: new() { ["id"] = "999" });

        Assert.False(resposta.Sucesso);
        Assert.Equal(HttpStatusCode.NotFound, resposta.CodigoStatus);
        Assert.Equal("Aluno não encontrado", resposta.MensagemErro);
    }

    [Fact]
    public async Task DeveCapturarExcecaoDeRede()
    {
        var handler = new FakeHttpHandler(new HttpRequestException("Connection refused"));
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.teste.com") };
        var adapter = new ApiAdapter(client, CriarConfiguracaoPadrao(), CriarLogger());

        var resposta = await adapter.EnviarAsync("listar");

        Assert.False(resposta.Sucesso);
        Assert.Equal(HttpStatusCode.InternalServerError, resposta.CodigoStatus);
        Assert.Contains("Connection refused", resposta.MensagemErro);
    }

    // ─── Fake HTTP Handler ───

    private class FakeHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode? _statusCode;
        private readonly string? _content;
        private readonly Exception? _exception;

        public string UltimaUrl { get; private set; } = "";
        public HttpMethod UltimoMetodo { get; private set; } = HttpMethod.Get;

        public FakeHttpHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        public FakeHttpHandler(Exception exception)
        {
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            UltimaUrl = request.RequestUri?.PathAndQuery ?? "";
            UltimoMetodo = request.Method;

            if (_exception is not null)
                throw _exception;

            return Task.FromResult(new HttpResponseMessage(_statusCode!.Value)
            {
                Content = new StringContent(_content!)
            });
        }
    }

    // ─── Fake Envelope Deserializer ───

    private class EnvelopePadrao<T>
    {
        public bool Sucesso { get; set; }
        public T? Dados { get; set; }
        public string? Mensagem { get; set; }
    }

    private class FakeEnvelopeDesserializador : IDesserializadorResposta
    {
        public T? Desserializar<T>(string conteudo, JsonSerializerOptions options)
        {
            var envelope = JsonSerializer.Deserialize<EnvelopePadrao<T>>(conteudo, options);
            return envelope is { Sucesso: true } ? envelope.Dados : default;
        }
    }
}
