using Infinite.Core.Consul;
using Infinite.Core.Postgres.Design;
using Infinite.{{Servico}}.Infrastructure.Mappings.{{Modulo}};
using System.Reflection;

namespace Infinite.{{Servico}}.Postgres;

// Design-time (dotnet ef): mesmas convenções e UseNodaTime do AddInfiniteContext.
// PostgresConfig vive no Consul, então a factory lê de lá. Exige CONSUL_DOMAIN e
// CONSUL_BACKEND_TOKEN no ambiente; ASPNETCORE_ENVIRONMENT=Production lê prd, o resto lê qa.
public sealed class InfiniteContextFactory : InfiniteContextFactoryBase
{
    protected override Assembly MappingsAssembly => typeof({{Entidade}}Mapping).Assembly;

    protected override void ConfigureConfiguration(IConfigurationBuilder builder) => builder
        .AddEnvironmentVariables()
        .AddConsulConfig("/apis/{{servico}}");
}
