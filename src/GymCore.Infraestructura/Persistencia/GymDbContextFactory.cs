
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;



namespace GymCore.Infraestructura.Persistencia;

public class GymDbContextFactory : IDesignTimeDbContextFactory<GymDbContext>
{
    public GymDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<GymDbContext>();
        optionsBuilder.UseSqlite("Data Source=../data/gym.db"); // ruta relativa al proyecto Infra

        return new GymDbContext(optionsBuilder.Options);
    }
}