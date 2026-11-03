
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;



namespace GymCore.Infraestructura.Persistencia;

// Fábrica de diseño: la usa "dotnet ef" para generar migraciones sin arrancar la aplicación.
// "migrations add" no se conecta a la base; "database update" usaría esta ruta (relativa a donde se ejecuta).
public class GymDbContextFactory : IDesignTimeDbContextFactory<GymDbContext>
{
    public GymDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<GymDbContext>();
        // "migrations add" no se conecta: alcanza con indicar el proveedor. Para "database update" se usa la
        // base de desarrollo de docker-compose.dev.yml.
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=gymcore;Username=gymcore;Password=gymcore_dev");

        return new GymDbContext(optionsBuilder.Options);
    }
}