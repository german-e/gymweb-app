using GymCore.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymCore.Infraestructura.Persistencia.Config;

public class UsuarioConfig : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("Usuarios");
        b.Property(x => x.NombreCompleto).IsRequired().HasMaxLength(120);
        b.Property(x => x.NombreUsuario).IsRequired().HasMaxLength(60);
        b.Property(x => x.Email).HasMaxLength(160);
        b.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500);
        b.Property(x => x.SelloSeguridad).IsRequired().HasMaxLength(64);
        b.HasIndex(x => x.NombreUsuario).IsUnique();

        b.HasOne(x => x.Rol).WithMany(r => r.Usuarios).HasForeignKey(x => x.RolId).OnDelete(DeleteBehavior.Restrict);
        // Si se elimina la ficha del entrenador, el usuario sigue existiendo sin vínculo
        b.HasOne(x => x.Entrenador).WithMany().HasForeignKey(x => x.EntrenadorId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class RolConfig : IEntityTypeConfiguration<Rol>
{
    public void Configure(EntityTypeBuilder<Rol> b)
    {
        b.ToTable("Roles");
        b.Property(x => x.Nombre).IsRequired().HasMaxLength(60);
        b.Property(x => x.Descripcion).HasMaxLength(300);
        b.HasIndex(x => x.Nombre).IsUnique();
        b.HasMany(x => x.Permisos).WithOne(p => p.Rol).HasForeignKey(p => p.RolId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RolPermisoConfig : IEntityTypeConfiguration<RolPermiso>
{
    public void Configure(EntityTypeBuilder<RolPermiso> b)
    {
        b.ToTable("RolPermisos");
        b.HasKey(x => new { x.RolId, x.Permiso });
        b.Property(x => x.Permiso).HasMaxLength(60);
    }
}
