using Infinite.{{Servico}};
using Infinite.Core.Consul;
using Infinite.Core.WebHost.Extensoes.Controllers;

var builder = WebApplication.CreateBuilder(args);

builder.AddConsulConfig("/apis/{{servico}}");

var startup = new Startup(builder.Configuration);

startup.ConfigureServices(builder.Services);

var app = builder.Build();

app.UseInfiniteApi("Infinite {{Servico}} API");

await app.RunAsync();
