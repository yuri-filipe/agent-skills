// Destino: Infinite.{{Servico}}/Controllers/{{Modulo}}/{{Entidades}}Controller.cs
using Infinite.Core.WebHost.Controllers;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Commands;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Infinite.{{Servico}}.Controllers.{{Modulo}};

[Route("v1/{{rota}}")]
[ApiController]
public class {{Entidades}}Controller(IMediator mediator, ILogger<{{Entidades}}Controller> logger)
    : InfiniteApiController(mediator, logger)
{
    [HttpGet]
    public Task<IActionResult> Find([FromQuery] Pesquisar{{Entidade}}Query query)
        => Send(query);

    [HttpGet("{id:long}")]
    public Task<IActionResult> GetById([FromRoute] long id)
        => Send(new Obter{{Entidade}}PorIdQuery(id));

    [HttpPost]
    public Task<IActionResult> Post([FromBody] Adicionar{{Entidade}}Command command)
        => Send(command);

    [HttpPut("{id:long}")]
    public Task<IActionResult> Put([FromRoute] long id, [FromBody] Atualizar{{Entidade}}Command command)
    {
        command.Id = id;
        return Send(command);
    }

    [HttpDelete("{id:long}")]
    public Task<IActionResult> Delete([FromRoute] long id)
        => Send(new Deletar{{Entidade}}Command(id));
}
