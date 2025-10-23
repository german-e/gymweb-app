using GymCore.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Infraestructura.Persistencia;

public class GymDbContext : DbContext
{
    public GymDbContext(DbContextOptions<GymDbContext> options) : base(options) {}

    public DbSet<Socio> Socios => Set<Socio>();
    public DbSet<Plan> Planes => Set<Plan>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<SocioPlan> SocioPlanes => Set<SocioPlan>();
    public DbSet<Asistencia> Asistencias => Set<Asistencia>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GymDbContext).Assembly);
    }
}