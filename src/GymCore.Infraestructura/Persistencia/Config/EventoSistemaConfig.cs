using GymCore.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymCore.Infraestructura.Persistencia.Config;

public class EventoSistemaConfig : IEntityTypeConfiguration<EventoSistema>
{
    public void Configure(EntityTypeBuilder<EventoSistema> b)
    {
        b.ToTable("EventosSistema");
        b.Ignore(x => x.Referencia);
        b.Property(x => x.Usuario).HasMaxLength(120);
        b.Property(x => x.EntidadNombre).IsRequired().HasMaxLength(200);
        b.Property(x => x.Resumen).HasMaxLength(500);
        b.Property(x => x.Detalle).HasMaxLength(4000);

        // Consultas habituales: timeline general, historial de una entidad y de un socio
        b.HasIndex(x => x.FechaUtc);
        b.HasIndex(x => new { x.Entidad, x.EntidadId });
        b.HasIndex(x => x.SocioId);
        b.HasIndex(x => x.UsuarioId);
    }
}
