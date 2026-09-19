using FluentValidation;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Queries;

namespace Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Validators;

public class Pesquisar{{Entidade}}QueryValidator : AbstractValidator<Pesquisar{{Entidade}}Query>
{
    public Pesquisar{{Entidade}}QueryValidator()
    {
        RuleFor(x => x.{{Campo}})
            .MaximumLength({{tamanho}});

        RuleFor(x => x.Id{{Relacionada}})
            .GreaterThan(0)
            .When(x => x.Id{{Relacionada}}.HasValue);
    }
}
