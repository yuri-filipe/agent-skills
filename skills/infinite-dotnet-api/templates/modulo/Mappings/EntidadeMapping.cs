// Destino: Infinite.{{Servico}}.Infrastructure/Mappings/{{Modulo}}/{{Entidade}}Mapping.cs
using Infinite.Core.Postgres.Mapping;
using Infinite.{{Servico}}.Domain.{{Modulo}}.{{Entidades}}.Models;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infinite.{{Servico}}.Infrastructure.Mappings.{{Modulo}};

public class {{Entidade}}Mapping : CoreTableMapping<{{Entidade}}>
{
    public {{Entidade}}Mapping() : base("{{tabela}}", "{{schema}}")
    {
    }

    // Só o que foge do default do CoreTableMapping.
    protected override void ConfigureAdditionalProperties(EntityTypeBuilder<{{Entidade}}> builder)
    {
        builder.Property(x => x.{{Campo}})
            .HasMaxLength({{tamanho}})
            .IsRequired();

        builder.Property(x => x.{{CampoOpcional}})
            .HasMaxLength({{tamanho}});

        // Relacionamento entre módulos: FK opcional + Restrict.
        // builder.HasOne(x => x.{{Relacionada}})
        //     .WithMany()
        //     .HasForeignKey(x => x.Id{{Relacionada}})
        //     .OnDelete(DeleteBehavior.Restrict);
    }
}
