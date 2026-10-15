using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Comercial;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Ventas;

[RequierePermiso(Permisos.VentasVer, Permisos.VentasCrear)]
public class TicketModel : PageModel
{
    private readonly GymDbContext _db;
    private readonly ServicioVentas _ventas;
    public TicketModel(GymDbContext db, ServicioVentas ventas) { _db = db; _ventas = ventas; }

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty(SupportsGet = true)] public bool Nuevo { get; set; }

    public Venta Venta { get; private set; } = null!;
    public Pago? Cuota { get; private set; }
    public List<EventoSistema> Eventos { get; private set; } = new();

    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashError { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var v = await _db.Ventas.AsNoTracking()
            .Include(x => x.Items)
            .Include(x => x.Socio)
            .FirstOrDefaultAsync(x => x.Id == Id);
        if (v is null) return NotFound();
        Venta = v;
        Cuota = await _db.Pagos.IgnoreQueryFilters().AsNoTracking().Include(p => p.SocioPlan).ThenInclude(sp => sp!.Plan)
            .FirstOrDefaultAsync(p => p.VentaId == Id);
        Eventos = await _db.EventosSistema.AsNoTracking()
            .Where(e => e.Entidad == EntidadAuditada.Venta && e.EntidadId == Id)
            .OrderByDescending(e => e.FechaUtc).ToListAsync();
        return Page();
    }

    [RequierePermiso(Permisos.VentasAnular)]
    public async Task<IActionResult> OnPostAnularAsync(string? motivo, string? detalle)
    {
        motivo = motivo?.Trim();
        detalle = string.IsNullOrWhiteSpace(detalle) ? null : detalle.Trim();
        if (string.IsNullOrEmpty(motivo) || !InventarioPresentacion.MotivosAnulacion.Contains(motivo))
        {
            FlashError = "Elegí el motivo de la anulación.";
            return RedirectToPage(new { id = Id });
        }
        if (motivo == "Otro" && detalle is null)
        {
            FlashError = "Describí el motivo de la anulación.";
            return RedirectToPage(new { id = Id });
        }
        var texto = motivo == "Otro" ? detalle! : detalle is null ? motivo : $"{motivo}: {detalle}";
        var r = await _ventas.AnularAsync(Id, texto);
        if (r.Ok) Flash = $"Venta {r.Venta!.Numero} anulada. El stock volvió al inventario{(await _db.Pagos.IgnoreQueryFilters().AnyAsync(p => p.VentaId == Id) ? " y la cuota incluida quedó anulada" : "")}.";
        else FlashError = r.Error;
        return RedirectToPage(new { id = Id });
    }
}
