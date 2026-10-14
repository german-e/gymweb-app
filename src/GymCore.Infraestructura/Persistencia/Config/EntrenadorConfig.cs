using GymCore.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymCore.Infraestructura.Persistencia.Config;

public class EntrenadorConfig : IEntityTypeConfiguration<Entrenador>
{
    public void Configure(EntityTypeBuilder<Entrenador> b)
    {
        b.ToTable("Entrenadores");

        b.Property(x => x.Nombre).IsRequired().HasMaxLength(120);
        b.Property(x => x.Apellido).IsRequired().HasMaxLength(120);
        b.Property(x => x.Dni).IsRequired().HasMaxLength(20);
        b.Property(x => x.FotoUrl).HasMaxLength(260);
        b.Property(x => x.Telefono).HasMaxLength(40);
        b.Property(x => x.Celular).HasMaxLength(40);
        b.Property(x => x.Email).HasMaxLength(160);
        b.Property(x => x.Direccion).HasMaxLength(200);
        b.Property(x => x.Especialidad).HasMaxLength(80);
        b.Property(x => x.Certificaciones).HasMaxLength(2000);
        b.Property(x => x.InstitucionFormadora).HasMaxLength(160);
        b.Property(x => x.DescripcionProfesional).HasMaxLength(2000);
        b.Property(x => x.Observaciones).HasMaxLength(2000);

        b.Ignore(x => x.NombreCompleto);
        b.Ignore(x => x.Iniciales);
        b.Ignore(x => x.ListaCertificaciones);
        b.Ignore(x => x.HorasSemanales);

        b.HasIndex(x => x.Dni).IsUnique();
        b.HasIndex(x => x.Activo);

        b.HasMany(x => x.Asignaciones).WithOne(a => a.Entrenador).HasForeignKey(a => a.EntrenadorId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Historial).WithOne(e => e.Entrenador).HasForeignKey(e => e.EntrenadorId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class EntrenadorSocioConfig : IEntityTypeConfiguration<EntrenadorSocio>
{
    public void Configure(EntityTypeBuilder<EntrenadorSocio> b)
    {
        b.ToTable("EntrenadorSocios");
        b.Property(x => x.Objetivo).HasMaxLength(200);
        b.HasOne(x => x.Socio).WithMany().HasForeignKey(x => x.SocioId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.EntrenadorId, x.SocioId }).IsUnique();
    }
}

public class EntrenadorEventoConfig : IEntityTypeConfiguration<EntrenadorEvento>
{
    public void Configure(EntityTypeBuilder<EntrenadorEvento> b)
    {
        b.ToTable("EntrenadorEventos");
        b.Property(x => x.Titulo).IsRequired().HasMaxLength(160);
        b.Property(x => x.Detalle).HasMaxLength(2000);
        b.HasIndex(x => new { x.EntrenadorId, x.Fecha });
    }
}
