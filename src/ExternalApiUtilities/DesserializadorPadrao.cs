using System.Text.Json;

namespace ExternalApiUtilities;

/// <summary>
/// Deserializador padrão que usa <see cref="JsonSerializer.Deserialize{T}"/> diretamente.
/// Usado quando nenhum <see cref="IDesserializadorResposta"/> customizado é configurado para a API.
/// </summary>
internal sealed class DesserializadorPadrao : IDesserializadorResposta
{
    public T? Desserializar<T>(string conteudo, JsonSerializerOptions options)
        => JsonSerializer.Deserialize<T>(conteudo, options);
}
