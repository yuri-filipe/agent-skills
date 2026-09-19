using Infinite.Core.Postgres.Extensions;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Models;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Queries;
using System.Linq.Expressions;

namespace Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Filters;

public static class Pesquisar{{Entidade}}FilterExtension
{
    public static Expression<Func<{{Entidade}}, bool>> GerarFiltro(this Pesquisar{{Entidade}}Query request)
    {
        var predicate = PredicateBuilder.True<{{Entidade}}>();

        if (!string.IsNullOrWhiteSpace(request.{{Campo}}))
            predicate = predicate.And(x => x.{{Campo}}.Contains(request.{{Campo}}));

        if (!string.IsNullOrWhiteSpace(request.{{CampoOpcional}}))
            predicate = predicate.And(x => x.{{CampoOpcional}} != null && x.{{CampoOpcional}}.Contains(request.{{CampoOpcional}}));

        if (request.Id{{Relacionada}}.HasValue && request.Id{{Relacionada}} > 0)
            predicate = predicate.And(x => x.Id{{Relacionada}} == request.Id{{Relacionada}});

        return predicate;
    }
}
