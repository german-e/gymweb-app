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
        // Bases existentes: la columna se agrega como NOT NULL DEFAULT 1 (todos los planes previos quedan activos)
        b.Property(x => x.Activo).HasAnnotation(EsquemaSqlite.DefaultAlAgregar, "1");
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
        b.Property(x => x.MotivoAnulacion).HasMaxLength(Pago.MotivoMaxLength);
        // Bases existentes: la columna se agrega como NOT NULL DEFAULT 0 (ningún pago previo queda anulado)
        b.Property(x => x.Anulado).HasAnnotation(EsquemaSqlite.DefaultAlAgregar, "0");

        // Un pago anulado no impacta en recaudación, saldos ni indicadores. Para verlo (listado, detalle,
        // historial) la consulta debe pedirlo explícitamente con IgnoreQueryFilters().
        b.HasQueryFilter(x => !x.Anulado);

        // Reportes: recaudación por rango de fechas
        b.HasIndex(x => x.FechaPago);

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

        // Reportes: asistencia por período y última asistencia por socio
        b.HasIndex(x => x.FechaHoraIngreso);
        b.HasIndex(x => new { x.SocioId, x.FechaHoraIngreso });
    }
}
