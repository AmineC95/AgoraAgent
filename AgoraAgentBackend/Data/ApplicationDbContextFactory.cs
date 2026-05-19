using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AgoraAgentBackend.Data;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        var conn = Environment.GetEnvironmentVariable("DEFAULT_CONNECTION") ?? "server=localhost;port=3306;database=agora_db;user=root;password=REMPLACER_PAR_ENV_VAR";
        optionsBuilder.UseMySql(conn, ServerVersion.AutoDetect(conn));
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
