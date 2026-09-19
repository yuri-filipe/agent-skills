using FluentValidation;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Queries;

namespace Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Validators;

public class Obter{{Entidade}}PorIdQueryValidator : AbstractValidator<Obter{{Entidade}}PorIdQuery>
{
    public Obter{{Entidade}}PorIdQueryValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0);
    }
}
