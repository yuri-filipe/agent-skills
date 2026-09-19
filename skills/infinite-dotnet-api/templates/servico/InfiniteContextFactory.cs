using Infinite.Core.Postgres.Context;
using Infinite.{{Servico}}.Infrastructure.Mappings.{{Modulo}};
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infinite.{{Servico}}.Postgres;

public class InfiniteContextFactory : IDesignTimeDbContextFactory<InfiniteContext>
{
    private const string MigrationsTableName = "__EFMigrationsHistory_{{Servico}}";

    public InfiniteContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<InfiniteContext>();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Postgres");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Connection string 'Postgres' não encontrada.");

        var migrationsAssembly = typeof({{Entidade}}Mapping).Assembly.GetName().Name;

        builder.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsAssembly(migrationsAssembly);
            npgsql.MigrationsHistoryTable(MigrationsTableName, "migrations");
        });

        return new InfiniteContext(builder.Options, typeof({{Entidade}}Mapping).Assembly);
    }
}
