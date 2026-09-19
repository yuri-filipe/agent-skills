namespace Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Dtos;

// DTO de detalhe (GET por id): representação completa.
public sealed class {{Entidade}}Dto
{
    public long Id { get; set; }

    public string {{Campo}} { get; set; } = string.Empty;

    public string? {{CampoOpcional}} { get; set; } = string.Empty;

    public long Id{{Relacionada}} { get; set; }
}
