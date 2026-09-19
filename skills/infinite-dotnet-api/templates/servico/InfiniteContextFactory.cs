using Infinite.Core.Postgres.Design;
using Infinite.{{Servico}}.Infrastructure.Mappings.{{Modulo}};
using System.Reflection;

namespace Infinite.{{Servico}}.Postgres;

// Forneça a mesma seção PostgresConfig do runtime via appsettings/variáveis de ambiente.
// A base compartilha as convenções e UseNodaTime com AddInfiniteContext.
public sealed class InfiniteContextFactory : InfiniteContextFactoryBase
{
    protected override Assembly MappingsAssembly => typeof({{Entidade}}Mapping).Assembly;
}
