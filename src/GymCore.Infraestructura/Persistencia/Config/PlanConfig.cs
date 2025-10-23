using GymCore.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymCore.Infraestructura.Persistencia.Config;

public class PlanConfig : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> b)
    {
        b.Property(x => x.Nombre).IsRequired().HasMaxLength(120);
        b.Property(x => x.Precio).HasColumnType("decimal(18,2)");
        b.Property(x => x.Moneda).HasMaxLength(10);
    }
}

public class SocioPlanConfig : IEntityTypeConfiguration<SocioPlan>
{
    public void Configure(EntityTypeBuilder<SocioPlan> b)
    {
        b.HasOne(x => x.Socio)
         .WithMany(s => s.Suscripciones)
         .HasForeignKey(x => x.SocioId)
         .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Plan)
         .WithMany(p => p.Suscripciones)
         .HasForeignKey(x => x.PlanId)
         .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PagoConfig : IEntityTypeConfiguration<Pago>
{
    public void Configure(EntityTypeBuilder<Pago> b)
    {
        b.Property(x => x.Importe).HasColumnType("decimal(18,2)");
        b.Property(x => x.Moneda).HasMaxLength(10);

        b.HasOne(x => x.Socio)
         .WithMany(s => s.Pagos)
         .HasForeignKey(x => x.SocioId)
         .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.SocioPlan)
         .WithMany()
         .HasForeignKey(x => x.SocioPlanId)
         .OnDelete(DeleteBehavior.SetNull);
    }
}

public class AsistenciaConfig : IEntityTypeConfiguration<Asistencia>
{
    public void Configure(EntityTypeBuilder<Asistencia> b)
    {
        b.HasOne(x => x.Socio)
         .WithMany(s => s.Asistencias)
         .HasForeignKey(x => x.SocioId)
         .OnDelete(DeleteBehavior.Restrict);
    }
}
