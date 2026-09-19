using Infinite.Core.Extensions.Cqrs;
using Infinite.Core.Postgres.Extensions;
using Infinite.Core.WebHost.Extensions.Controllers;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Mappers;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.QueryHandlers;
using Infinite.{{Servico}}.Infrastructure.Mappings.{{Modulo}};

namespace Infinite.{{Servico}};

public class Startup(IConfiguration configuration)
{
    private const string NameApi = "Infinite.{{Servico}}.Api";

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddInfiniteApiController(configuration, NameApi);

        // marcador de assembly do Infrastructure (onde vivem os *Mapping)
        services.AddInfiniteContext(configuration, typeof({{Entidade}}Mapping).Assembly);

        // marcador de assembly do Domain (handlers + validators)
        services.AddInfiniteCqrs(configuration, typeof(Pesquisar{{Entidade}}QueryHandler).Assembly);

        AddMappers(services);

        // Add{{Modulo}}Services(services);
    }

    public static IServiceCollection AddMappers(IServiceCollection services)
    {
        services.AddSingleton<{{Entidade}}Mapper>();

        return services;
    }

    // Um método por módulo que precise de serviços de orquestração/integração.
    // public static IServiceCollection Add{{Modulo}}Services(IServiceCollection services)
    // {
    //     services.AddScoped<I{{Modulo}}Service, {{Modulo}}Service>();
    //
    //     return services;
    // }
}
