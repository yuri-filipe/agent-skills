namespace Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Commands;

// Campos compartilhados por Adicionar/Atualizar. Existe para permitir um único validator base.
public interface I{{Entidade}}Writable
{
    string {{Campo}} { get; set; }

    string? {{CampoOpcional}} { get; set; }

    long Id{{Relacionada}} { get; set; }
}
