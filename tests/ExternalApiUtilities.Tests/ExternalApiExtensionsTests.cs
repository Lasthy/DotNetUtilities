using ExternalApiUtilities.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExternalApiUtilities.Tests;

public class ExternalApiExtensionsTests
{
    [Fact]
    public void DeveRegistrarApiEResolverFactory()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddExternalApi(api =>
        {
            api.AdicionarApi(config =>
            {
                config.Nome = "minha-api";
                config.UrlBase = "https://api.teste.com";
                config.Rotas.Add(new RotaApi { Nome = "listar", Caminho = "/api/items" });
            });
        });

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IApiAdapterFactory>();

        var adapter = factory.Obter("minha-api");

        Assert.NotNull(adapter);
        Assert.Equal("minha-api", adapter.NomeApi);
    }

    [Fact]
    public void DeveLancarExcecaoParaApiNaoRegistrada()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddExternalApi(api =>
        {
            api.AdicionarApi(config =>
            {
                config.Nome = "unica-api";
                config.UrlBase = "https://api.teste.com";
            });
        });

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IApiAdapterFactory>();

        Assert.Throws<InvalidOperationException>(() => factory.Obter("api-inexistente"));
    }

    [Fact]
    public void DeveRegistrarMultiplasApis()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddExternalApi(api =>
        {
            api.AdicionarApi(config =>
            {
                config.Nome = "api-a";
                config.UrlBase = "https://api-a.com";
            });
            api.AdicionarApi(config =>
            {
                config.Nome = "api-b";
                config.UrlBase = "https://api-b.com";
            });
        });

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IApiAdapterFactory>();

        var adapterA = factory.Obter("api-a");
        var adapterB = factory.Obter("api-b");

        Assert.Equal("api-a", adapterA.NomeApi);
        Assert.Equal("api-b", adapterB.NomeApi);
    }

    [Fact]
    public void DeveRegistrarMapper()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddExternalApi(api =>
        {
            api.AdicionarMapper<AlunoExternoDto, AlunoExterno, AlunoExternoMapper>();
        });

        var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IRespostaMapper<AlunoExternoDto, AlunoExterno>>();

        Assert.NotNull(mapper);
        Assert.IsType<AlunoExternoMapper>(mapper);
    }

    [Fact]
    public void DeveValidarNomeDaApiObrigatorio()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        Assert.Throws<ArgumentException>(() =>
        {
            services.AddExternalApi(api =>
            {
                api.AdicionarApi(config =>
                {
                    config.Nome = "";
                    config.UrlBase = "https://api.teste.com";
                });
            });
        });
    }

    [Fact]
    public void DeveValidarUrlBaseDaApiObrigatoria()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        Assert.Throws<ArgumentException>(() =>
        {
            services.AddExternalApi(api =>
            {
                api.AdicionarApi(config =>
                {
                    config.Nome = "api-ok";
                    config.UrlBase = "";
                });
            });
        });
    }

    [Fact]
    public void DeveRegistrarHeadersPadraoNoHttpClient()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddExternalApi(api =>
        {
            api.AdicionarApi(config =>
            {
                config.Nome = "api-com-headers";
                config.UrlBase = "https://api.test.com";
                config.HeadersPadrao["X-Api-Key"] = "my-secret-key";
                config.HeadersPadrao["Accept"] = "application/json";
            });
        });

        var provider = services.BuildServiceProvider();
        var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
        var client = httpClientFactory.CreateClient("ExternalApi_api-com-headers");

        Assert.NotNull(client);
        Assert.Equal(new Uri("https://api.test.com"), client.BaseAddress);
    }

    [Fact]
    public void DeveObterTodosOsAdapters()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddExternalApi(api =>
        {
            api.AdicionarApi(c => { c.Nome = "api-1"; c.UrlBase = "https://a.com"; });
            api.AdicionarApi(c => { c.Nome = "api-2"; c.UrlBase = "https://b.com"; });
            api.AdicionarApi(c => { c.Nome = "api-3"; c.UrlBase = "https://c.com"; });
        });

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IApiAdapterFactory>();

        var todos = factory.ObterTodos().ToList();

        Assert.Equal(3, todos.Count);
    }
}
