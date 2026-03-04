using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ExternalApiUtilities;

/// <summary>
/// Implementação padrão de <see cref="IApiAdapter"/> usando <see cref="HttpClient"/>.
/// <para>
/// Utiliza <see cref="IHttpClientFactory"/> internamente (via named HttpClient).
/// Suporta substituição de parâmetros de caminho, query string, e corpo JSON.
/// </para>
/// </summary>
public class ApiAdapter : IApiAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ConfiguracaoApi _configuracao;
    private readonly ILogger<ApiAdapter> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IDesserializadorResposta _desserializador;

    public ApiAdapter(
        HttpClient httpClient,
        ConfiguracaoApi configuracao,
        ILogger<ApiAdapter> logger)
    {
        _httpClient = httpClient;
        _configuracao = configuracao;
        _logger = logger;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        _desserializador = configuracao.Desserializador ?? new DesserializadorPadrao();
    }

    /// <inheritdoc />
    public string NomeApi => _configuracao.Nome;

    /// <inheritdoc />
    public async Task<RespostaApi> EnviarAsync(
        string nomeRota,
        Dictionary<string, string>? parametrosCaminho = null,
        Dictionary<string, string>? parametrosQuery = null,
        object? corpo = null,
        CancellationToken ct = default)
    {
        var rota = ResolverRota(nomeRota);
        var url = ConstruirUrl(rota, parametrosCaminho, parametrosQuery);

        _logger.LogDebug("API [{Api}] {Metodo} {Url}", NomeApi, rota.Metodo, url);

        try
        {
            using var request = CriarRequest(rota.Metodo, url, corpo);
            using var response = await _httpClient.SendAsync(request, ct);

            var conteudo = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "API [{Api}] rota [{Rota}] retornou {Status}: {Conteudo}",
                    NomeApi, nomeRota, (int)response.StatusCode, TruncarConteudo(conteudo));
            }

            return new RespostaApi
            {
                Sucesso = response.IsSuccessStatusCode,
                CodigoStatus = response.StatusCode,
                ConteudoBruto = conteudo,
                MensagemErro = response.IsSuccessStatusCode ? null : conteudo
            };
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API [{Api}] rota [{Rota}] falhou com exceção", NomeApi, nomeRota);

            return new RespostaApi
            {
                Sucesso = false,
                CodigoStatus = HttpStatusCode.InternalServerError,
                MensagemErro = ex.Message
            };
        }
    }

    /// <inheritdoc />
    public async Task<RespostaApi<T>> EnviarAsync<T>(
        string nomeRota,
        Dictionary<string, string>? parametrosCaminho = null,
        Dictionary<string, string>? parametrosQuery = null,
        object? corpo = null,
        CancellationToken ct = default)
    {
        var respostaBruta = await EnviarAsync(nomeRota, parametrosCaminho, parametrosQuery, corpo, ct);

        var resposta = new RespostaApi<T>
        {
            Sucesso = respostaBruta.Sucesso,
            CodigoStatus = respostaBruta.CodigoStatus,
            ConteudoBruto = respostaBruta.ConteudoBruto,
            MensagemErro = respostaBruta.MensagemErro
        };

        if (!respostaBruta.Sucesso || string.IsNullOrWhiteSpace(respostaBruta.ConteudoBruto))
            return resposta;

        try
        {
            resposta.Dados = _desserializador.Desserializar<T>(respostaBruta.ConteudoBruto, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "API [{Api}] rota [{Rota}] falha ao deserializar resposta para {Tipo}",
                NomeApi, nomeRota, typeof(T).Name);

            resposta.Sucesso = false;
            resposta.MensagemErro = $"Falha ao deserializar resposta: {ex.Message}";
        }

        return resposta;
    }

    private RotaApi ResolverRota(string nomeRota)
    {
        var rota = _configuracao.Rotas.FirstOrDefault(r =>
            r.Nome.Equals(nomeRota, StringComparison.OrdinalIgnoreCase));

        if (rota is null)
        {
            throw new InvalidOperationException(
                $"Rota '{nomeRota}' não encontrada na API '{NomeApi}'. " +
                $"Rotas disponíveis: {string.Join(", ", _configuracao.Rotas.Select(r => r.Nome))}");
        }

        return rota;
    }

    private string ConstruirUrl(
        RotaApi rota,
        Dictionary<string, string>? parametrosCaminho,
        Dictionary<string, string>? parametrosQuery)
    {
        var baseUrl = _configuracao.UrlBase.TrimEnd('/');
        var caminho = rota.Caminho.TrimStart('/');
        var url = $"{baseUrl}/{caminho}";

        if (parametrosCaminho is not null)
        {
            foreach (var (chave, valor) in parametrosCaminho)
            {
                url = url.Replace($"{{{chave}}}", Uri.EscapeDataString(valor));
            }
        }

        if (parametrosQuery is null || parametrosQuery.Count == 0)
            return url;

        var query = string.Join("&",
            parametrosQuery.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value).Replace("%3D", "=")}"));

        return $"{url}?{query}";
    }

    private HttpRequestMessage CriarRequest(HttpMethod metodo, string url, object? corpo)
    {
        var request = new HttpRequestMessage(metodo, url);

        if (corpo is not null)
        {
            var json = JsonSerializer.Serialize(corpo, _jsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return request;
    }

    private static string TruncarConteudo(string? conteudo, int maxLength = 500)
    {
        if (string.IsNullOrEmpty(conteudo)) return "(vazio)";
        return conteudo.Length <= maxLength ? conteudo : conteudo[..maxLength] + "...";
    }
}
