using GymCore.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymCore.Infraestructura.Persistencia.Config;

public class ConfiguracionGimnasioConfig : IEntityTypeConfiguration<ConfiguracionGimnasio>
{
    public void Configure(EntityTypeBuilder<ConfiguracionGimnasio> b)
    {
        b.ToTable("ConfiguracionGimnasio");
        b.Property(x => x.NombreComercial).IsRequired().HasMaxLength(80);
        b.Property(x => x.Eslogan).HasMaxLength(80);
        b.Property(x => x.LogoUrl).HasMaxLength(260);
        b.Property(x => x.LogoReducidoUrl).HasMaxLength(260);
        b.Property(x => x.FaviconUrl).HasMaxLength(260);
        b.Property(x => x.ImagenBienvenidaUrl).HasMaxLength(260);
        b.Property(x => x.ColorPrincipal).IsRequired().HasMaxLength(7);
        b.Property(x => x.ColorSecundario).IsRequired().HasMaxLength(7);
        b.Property(x => x.Direccion).HasMaxLength(160);
        b.Property(x => x.Telefono).HasMaxLength(40);
        b.Property(x => x.Email).HasMaxLength(120);
        b.Property(x => x.SitioWeb).HasMaxLength(160);
        b.Property(x => x.TextoCredencial).HasMaxLength(120);
        b.Property(x => x.KioscoMensaje).HasMaxLength(80);
        b.Property(x => x.UsuarioModifico).HasMaxLength(120);
    }
}

public class AvisoRecepcionConfig : IEntityTypeConfiguration<AvisoRecepcion>
{
    public void Configure(EntityTypeBuilder<AvisoRecepcion> b)
    {
        b.ToTable("AvisosRecepcion");
        b.Property(x => x.Titulo).IsRequired().HasMaxLength(80);
        b.Property(x => x.Texto).HasMaxLength(300);
        b.Property(x => x.ImagenUrl).HasMaxLength(260);
        b.HasIndex(x => new { x.Activo, x.Orden });
    }
}
