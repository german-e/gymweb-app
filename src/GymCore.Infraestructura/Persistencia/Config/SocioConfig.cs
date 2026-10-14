using GymCore.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymCore.Infraestructura.Persistencia.Config;

public class SocioConfig : IEntityTypeConfiguration<Socio>
{
    public void Configure(EntityTypeBuilder<Socio> b)
    {
        b.Property(x => x.Dni).IsRequired().HasMaxLength(20);
        b.Property(x => x.Apellido).IsRequired().HasMaxLength(120);
        b.Property(x => x.Nombre).IsRequired().HasMaxLength(120);
        b.Property(x => x.TarjetaCodigo).HasMaxLength(32);

        b.Property(x => x.FotoUrl).HasMaxLength(260);
        b.Property(x => x.Telefono).HasMaxLength(40);
        b.Property(x => x.Email).HasMaxLength(160);
        b.Property(x => x.Direccion).HasMaxLength(200);
        b.Property(x => x.ContactoEmergenciaNombre).HasMaxLength(120);
        b.Property(x => x.ContactoEmergenciaTelefono).HasMaxLength(40);

        b.Ignore(x => x.DadoDeBaja);
        b.Ignore(x => x.NombreCompleto);
        b.Ignore(x => x.Iniciales);
        b.Ignore(x => x.CodigoCredencial);
        b.Ignore(x => x.NumeroVisible);

        b.HasIndex(x => x.Dni);
        b.HasIndex(x => x.TarjetaCodigo).IsUnique(); // único; permite múltiples NULL en SQLite
        b.HasIndex(x => x.NumeroSocio).IsUnique();
    }
}

public class MedicionFisicaConfig : IEntityTypeConfiguration<MedicionFisica>
{
    public void Configure(EntityTypeBuilder<MedicionFisica> b)
    {
        b.ToTable("MedicionesFisicas");
        b.Property(x => x.PesoKg).HasColumnType("decimal(6,2)");
        b.Property(x => x.AlturaCm).HasColumnType("decimal(6,2)");
        b.Property(x => x.MasaMuscularKg).HasColumnType("decimal(6,2)");
        b.Property(x => x.PorcentajeGraso).HasColumnType("decimal(5,2)");
        b.Property(x => x.Observaciones).HasMaxLength(1000);
        b.Ignore(x => x.Imc);

        b.HasOne(x => x.Socio).WithMany(s => s.Mediciones).HasForeignKey(x => x.SocioId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Entrenador).WithMany().HasForeignKey(x => x.EntrenadorId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => new { x.SocioId, x.Fecha });
    }
}
