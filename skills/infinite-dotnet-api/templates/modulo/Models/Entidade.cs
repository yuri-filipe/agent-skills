using Infinite.Core.Postgres.Models;

namespace Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Models;

// Id, Ativo, DataCriacao e demais colunas base vêm de CoreEntity — não redeclarar.
public sealed class {{Entidade}} : CoreEntity
{
    public string {{Campo}} { get; set; } = string.Empty;

    public string? {{CampoOpcional}} { get; set; } = string.Empty;

    public long Id{{Relacionada}} { get; set; }

    public {{Relacionada}} {{Relacionada}} { get; set; } = null!;

    public List<{{Filho}}> {{Filhos}} { get; set; } = [];
}
