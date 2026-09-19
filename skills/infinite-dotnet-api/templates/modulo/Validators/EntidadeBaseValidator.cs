using FluentValidation;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Commands;

namespace Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Validators;

// Regras comuns de Adicionar/Atualizar, sobre a interface writable.
public class {{Entidade}}BaseValidator<T> : AbstractValidator<T>
    where T : class, I{{Entidade}}Writable
{
    public {{Entidade}}BaseValidator()
    {
        RuleFor(x => x.{{Campo}})
            .NotEmpty()
            .MaximumLength({{tamanho}});

        RuleFor(x => x.{{CampoOpcional}})
            .MaximumLength({{tamanho}});

        RuleFor(x => x.Id{{Relacionada}})
            .GreaterThan(0);
    }
}
