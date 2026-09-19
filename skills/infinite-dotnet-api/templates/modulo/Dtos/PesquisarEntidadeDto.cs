namespace Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Dtos;

// DTO de listagem: só o que a grid precisa. Nunca reutilize o DTO de detalhe aqui.
public class Pesquisar{{Entidade}}Dto
{
    public long Id { get; set; }

    public string {{Campo}} { get; set; } = string.Empty;

    public long Id{{Relacionada}} { get; set; }
}
