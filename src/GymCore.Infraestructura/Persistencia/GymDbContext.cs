using GymCore.Aplicacion.Abstractiones;
using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Infraestructura.Persistencia;

public class GymDbContext : DbContext
{
    private readonly IOperadorAuditoria? _operador;

    // El operador es opcional: herramientas de diseño y procesos sin request usan "Sistema".
    public GymDbContext(DbContextOptions<GymDbContext> options, IOperadorAuditoria? operador = null) : base(options)
    {
        _operador = operador;
    }

    public const string UsuarioSistema = "Sistema";

    public DbSet<Socio> Socios => Set<Socio>();
    public DbSet<Plan> Planes => Set<Plan>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<SocioPlan> SocioPlanes => Set<SocioPlan>();
    public DbSet<Asistencia> Asistencias => Set<Asistencia>();
    public DbSet<Entrenador> Entrenadores => Set<Entrenador>();
    public DbSet<EntrenadorSocio> EntrenadorSocios => Set<EntrenadorSocio>();
    public DbSet<EntrenadorEvento> EntrenadorEventos => Set<EntrenadorEvento>();
    public DbSet<MedicionFisica> MedicionesFisicas => Set<MedicionFisica>();
    public DbSet<EventoSistema> EventosSistema => Set<EventoSistema>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<CategoriaProducto> CategoriasProducto => Set<CategoriaProducto>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<MovimientoStock> MovimientosStock => Set<MovimientoStock>();
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<VentaItem> VentaItems => Set<VentaItem>();
    public DbSet<ConfiguracionGimnasio> ConfiguracionesGimnasio => Set<ConfiguracionGimnasio>();
    public DbSet<AvisoRecepcion> AvisosRecepcion => Set<AvisoRecepcion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GymDbContext).Assembly);
    }

    // Completa la auditoría antes de guardar:
    //  - Usuario de los eventos nuevos (operador actual).
    //  - EntidadId de eventos sobre entidades recién creadas: se guarda primero para obtener el Id y luego
    //    se actualiza el evento, todo en una misma transacción (o el evento y la entidad quedan, o ninguno).
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var usuario = _operador?.Nombre is { Length: > 0 } n ? n : UsuarioSistema;
        var usuarioId = _operador?.UsuarioId;
        foreach (var m in ChangeTracker.Entries<MovimientoStock>().Where(e => e.State == EntityState.Added))
        {
            if (m.Entity.Usuario is null) { m.Entity.Usuario = usuario; m.Entity.UsuarioId ??= usuarioId; }
        }
        foreach (var e in ChangeTracker.Entries<EntrenadorEvento>().Where(e => e.State == EntityState.Added))
            e.Entity.Usuario ??= usuario;

        var eventos = ChangeTracker.Entries<EventoSistema>().Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToList();
        foreach (var ev in eventos)
        {
            // Si el evento ya trae responsable (ej. inicio de sesión) se respeta
            if (ev.Usuario is null) { ev.Usuario = usuario; ev.UsuarioId ??= usuarioId; }
        }

        var pendientes = eventos.Where(ev => ev.Referencia is not null && ev.EntidadId is null).ToList();
        if (pendientes.Count == 0) return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        var transaccion = Database.CurrentTransaction is null ? await Database.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            var filas = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            foreach (var ev in pendientes)
            {
                ev.EntidadId = Entry(ev.Referencia!).Property("Id").CurrentValue as int?;
                if (ev.Entidad == EntidadAuditada.Socio) ev.SocioId ??= ev.EntidadId;
                ev.Referencia = null;
            }
            filas += await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            if (transaccion is not null) await transaccion.CommitAsync(cancellationToken);
            return filas;
        }
        finally
        {
            if (transaccion is not null) await transaccion.DisposeAsync();
        }
    }
}
