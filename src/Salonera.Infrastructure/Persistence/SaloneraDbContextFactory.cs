using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Salonera.Infrastructure.Time;

namespace Salonera.Infrastructure.Persistence;

public sealed class SaloneraDbContextFactory : IDesignTimeDbContextFactory<SaloneraDbContext>
{
    public SaloneraDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SALONERA_CONNECTION_STRING")
            ?? "Server=localhost\\SALONERA;Database=Salonera;Trusted_Connection=True;TrustServerCertificate=True;";
        var options = new DbContextOptionsBuilder<SaloneraDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(SaloneraDbContext).Assembly.FullName))
            .Options;

        return new SaloneraDbContext(options, new SystemClock());
    }
}
