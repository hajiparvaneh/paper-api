using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace PaperAPI.Infrastructure.Persistence;

public sealed class PaperApiDesignTimeDbContextFactory : IDesignTimeDbContextFactory<PaperApiDbContext>
{
    public PaperApiDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var apiProjectPath = Path.Combine(basePath, "src", "Api");

        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(apiProjectPath, "appsettings.json"), optional: true)
            .AddJsonFile(Path.Combine(apiProjectPath, "appsettings.Development.json"), optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("Default") ??
                               "Host=localhost;Port=5432;Database=paperapi;Username=paperapi;Password=paperapi";

        var optionsBuilder = new DbContextOptionsBuilder<PaperApiDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsAssembly(typeof(PaperApiDbContext).Assembly.FullName);
        });

        return new PaperApiDbContext(optionsBuilder.Options);
    }
}
