// Destino: Infinite.{{Servico}}.Domain/{{Modulo}}/Services/{{Externo}}ApiOptions.cs
// Padrão de integração HTTP externa: Options + cliente Refit + service de domínio.
namespace Infinite.{{Servico}}.Domain.{{Modulo}}.Services;

public sealed class {{Externo}}ApiOptions
{
    public const string SectionName = "{{Externo}}:Api";
    public const string DefaultBaseUrl = "{{url-default}}";

    public string BaseUrl { get; set; } = DefaultBaseUrl;
    public string? ApiKey { get; set; }
}

// Cliente Refit — só a superfície HTTP, sem regra:
//
// public interface I{{Externo}}ApiClient
// {
//     [Post("/{{recurso}}")]
//     Task<{{Externo}}ResponseDto> EnviarAsync([Body] {{Externo}}RequestDto request, CancellationToken cancellationToken);
// }
//
// Service de domínio (I{{Externo}}Service/{{Externo}}Service) encapsula o cliente e é
// o único ponto que os handlers conhecem. Registro no Startup:
//
// var options = configuration.GetSection({{Externo}}ApiOptions.SectionName).Get<{{Externo}}ApiOptions>()
//               ?? new {{Externo}}ApiOptions();
// services.AddSingleton(options);
// services.AddRefitClient<I{{Externo}}ApiClient>()
//     .ConfigureHttpClient((sp, client) => { /* BaseAddress + headers a partir das options */ });
// services.AddScoped<I{{Externo}}Service, {{Externo}}Service>();
