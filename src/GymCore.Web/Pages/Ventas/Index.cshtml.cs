using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Comercial;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Periodo = GymCore.Web.Reportes.Periodo;

namespace GymCore.Web.Pages.Ventas;

[RequierePermiso(Permisos.VentasVer)]
public class IndexModel : PageModel
{
    private readonly GymDbContext _db;
    private readonly ReportesComerciales _reportes;
    public IndexModel(GymDbContext db, ReportesComerciales reportes) { _db = db; _reportes = reportes; }

    public const int Maximo = 1000;

    [BindProperty(SupportsGet = true)] public DateOnly? Desde { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? Hasta { get; set; }
    [BindProperty(SupportsGet = true)] public MedioPago? Medio { get; set; }

    public record Fila(int Id, string Numero, DateTime Fecha, string Cliente, int? SocioId, int Unidades, string Detalle,
        bool ConCuota, MedioPago Medio, decimal Total, bool Anulada, string? Usuario);

    public List<Fila> Filas { get; private set; } = new();
    public int TotalEnRango { get; private set; }
    public decimal VentasHoy { get; private set; }
    public int TicketsHoy { get; private set; }

    public IEnumerable<Fila> Cobradas => Filas.Where(f => !f.Anulada);
    public decimal TotalCobrado => Cobradas.Sum(f => f.Total);
    public decimal TicketPromedio => Cobradas.Any() ? Math.Round(TotalCobrado / Cobradas.Count(), 0) : 0;

    public async Task OnGetAsync()
    {
        var hoy = DateOnly.FromDateTime(DateTime.Now);
        Hasta ??= hoy;
        Desde ??= Hasta.Value.AddDays(-29);
        if (Desde > Hasta) (Desde, Hasta) = (Hasta, Desde);

        var desdeUtc = Periodo.InicioUtc(Desde.Value);
        var hastaUtc = Periodo.InicioUtc(Hasta.Value.AddDays(1));
        var q = _db.Ventas.AsNoTracking().Where(v => v.Fecha >= desdeUtc && v.Fecha < hastaUtc);
        if (Medio is { } m) q = q.Where(v => v.Medio == m);

        TotalEnRango = await q.CountAsync();
        var filas = await q.OrderByDescending(v => v.Fecha).ThenByDescending(v => v.Id).Take(Maximo)
            .Select(v => new
            {
                v.Id, v.Fecha, v.SocioId,
                Socio = v.Socio != null ? v.Socio.Apellido + ", " + v.Socio.Nombre : null,
                v.ClienteNombre, v.Medio, v.Total, v.Anulada, v.Usuario,
                Items = v.Items.Select(i => new { i.Descripcion, i.Cantidad }).ToList(),
                ConCuota = _db.Pagos.IgnoreQueryFilters().Any(p => p.VentaId == v.Id)
            }).ToListAsync();
        Filas = filas.Select(v => new Fila(v.Id, $"V-{v.Id:D6}", v.Fecha, v.Socio ?? v.ClienteNombre ?? "Cliente ocasional", v.SocioId,
            v.Items.Sum(i => i.Cantidad),
            string.Join(", ", v.Items.Select(i => i.Cantidad > 1 ? $"{i.Cantidad}× {i.Descripcion}" : i.Descripcion)),
            v.ConCuota, v.Medio, v.Total, v.Anulada, v.Usuario)).ToList();

        (VentasHoy, TicketsHoy) = await _reportes.DelDiaAsync(hoy);
    }
}
