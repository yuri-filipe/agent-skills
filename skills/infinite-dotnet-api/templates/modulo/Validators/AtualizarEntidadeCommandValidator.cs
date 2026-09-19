using FluentValidation;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Commands;

namespace Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Validators;

public class Atualizar{{Entidade}}CommandValidator : AbstractValidator<Atualizar{{Entidade}}Command>
{
    public Atualizar{{Entidade}}CommandValidator()
    {
        Include(new {{Entidade}}BaseValidator<Atualizar{{Entidade}}Command>());

        RuleFor(x => x.Id)
            .GreaterThan(0);
    }
}
