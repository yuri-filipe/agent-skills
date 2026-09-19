using FluentValidation;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Commands;

namespace Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Validators;

public class Adicionar{{Entidade}}CommandValidator : AbstractValidator<Adicionar{{Entidade}}Command>
{
    public Adicionar{{Entidade}}CommandValidator()
    {
        Include(new {{Entidade}}BaseValidator<Adicionar{{Entidade}}Command>());
    }
}
