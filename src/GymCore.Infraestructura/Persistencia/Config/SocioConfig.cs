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

        b.HasIndex(x => x.Dni);
        b.HasIndex(x => x.TarjetaCodigo).IsUnique(); // único; permite múltiples NULL en SQLite
    }
}
