using FluentValidation;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Commands;

namespace Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Validators;

public class Deletar{{Entidade}}CommandValidator : AbstractValidator<Deletar{{Entidade}}Command>
{
    public Deletar{{Entidade}}CommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0);
    }
}
