
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
        optionsBuilder.UseSqlite("Data Source=../data/gym.db"); // ruta relativa al proyecto Infra

        return new GymDbContext(optionsBuilder.Options);
    }
}