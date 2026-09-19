using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Commands;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Dtos;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Models;
using Riok.Mapperly.Abstractions;

namespace Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Mappers;

// Registrar como Singleton em Startup.AddMappers.
[Mapper]
public partial class {{Entidade}}Mapper
{
    public partial Pesquisar{{Entidade}}Dto ToPesquisarDto({{Entidade}} src);

    public partial {{Entidade}}Dto To{{Entidade}}Dto({{Entidade}} src);

    public partial {{Entidade}} FromAdicionarCommand(Adicionar{{Entidade}}Command command);

    public partial void Update{{Entidade}}FromCommand(Atualizar{{Entidade}}Command command, {{Entidade}} entidade);
}
