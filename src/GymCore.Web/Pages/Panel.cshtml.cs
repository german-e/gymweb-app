using GymCore.Dominio.Entidades;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages;

// Panel de gestión: todos los indicadores salen de la base (IndicadoresGimnasio), sin valores fijos.
public class PanelModel : PageModel
{
    private readonly GymDbContext _db;
    private readonly IUsuarioActual _usuario;
    private readonly GymCore.Web.Comercial.ServicioInventario _inventario;
    private readonly GymCore.Web.Comercial.ReportesComerciales _comercial;
    public PanelModel(GymDbContext db, IUsuarioActual usuario, GymCore.Web.Comercial.ServicioInventario inventario,
        GymCore.Web.Comercial.ReportesComerciales comercial)
    { _db = db; _usuario = usuario; _inventario = inventario; _comercial = comercial; }

    public IndicadoresGimnasio.Cobranza Cobranza { get; private set; } = null!;
    public IndicadoresGimnasio.Membresias Membresias { get; private set; } = null!;
    public (Plan Plan, int Altas)? PlanMasContratado { get; private set; }
    public List<(DateTime Mes, decimal Total)> RecaudacionMensual { get; private set; } = new();
    public List<Pago> UltimosPagos { get; private set; } = new();
    public int AsistenciasHoy { get; private set; }
    // Solo para quien ve únicamente sus socios asignados (rol tipo entrenador)
    public List<GymCore.Web.Pages.Socios.ResumenSocio>? MisSocios { get; private set; }
    public List<EventoSistema> Actividad { get; private set; } = new();   // últimos 10 eventos de auditoría
    public GymCore.Web.Comercial.ServicioInventario.ResumenStock? Stock { get; private set; }   // con PRODUCTOS_VER
    public (decimal Importe, int Tickets)? VentasHoy { get; private set; }                        // con VENTAS_VER

    public async Task OnGetAsync()
    {
        (Cobranza, Membresias, _) = await IndicadoresGimnasio.DelRequestAsync(HttpContext, _db);
        PlanMasContratado = await IndicadoresGimnasio.PlanMasContratadoAsync(_db);
        RecaudacionMensual = await IndicadoresGimnasio.RecaudacionMensualAsync(_db, 6);

        UltimosPagos = await _db.Pagos.AsNoTracking()
            .Include(p => p.Socio)
            .Include(p => p.SocioPlan).ThenInclude(sp => sp!.Plan)
            .OrderByDescending(p => p.FechaPago)
            .Take(5)
            .ToListAsync();

        if (!_usuario.Puede(GymCore.Web.Seguridad.Permisos.SociosVer) && _usuario.Puede(GymCore.Web.Seguridad.Permisos.SociosVerAsignados))
        {
            var ids = await GymCore.Web.Pages.Socios.SociosConsultas.AsignadosAsync(_db, _usuario.EntrenadorId);
            MisSocios = (await GymCore.Web.Pages.Socios.SociosConsultas.ResumenAsync(_db, ids)).Values
                .OrderBy(r => r.Socio.Apellido).ThenBy(r => r.Socio.Nombre).ToList();
        }

        Actividad = await _db.EventosSistema.AsNoTracking()
            .OrderByDescending(e => e.FechaUtc).ThenByDescending(e => e.Id)
            .Take(10)
            .ToListAsync();

        if (_usuario.Puede(GymCore.Web.Seguridad.Permisos.ProductosVer))
            Stock = await _inventario.ResumenDelRequestAsync(HttpContext);
        if (_usuario.Puede(GymCore.Web.Seguridad.Permisos.VentasVer))
            VentasHoy = await _comercial.DelDiaAsync(DateOnly.FromDateTime(DateTime.Now));

        var hoyUtc = DateTime.Today.ToUniversalTime();
        var mananaUtc = DateTime.Today.AddDays(1).ToUniversalTime();
        AsistenciasHoy = await _db.Asistencias.AsNoTracking()
            .CountAsync(a => a.FechaHoraIngreso >= hoyUtc && a.FechaHoraIngreso < mananaUtc);
    }
}
