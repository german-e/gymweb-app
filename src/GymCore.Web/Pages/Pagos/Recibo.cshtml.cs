using GymCore.Web.Seguridad;
using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
using GymCore.Web.Pages.Socios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Pagos;

// Detalle de un pago con su comprobante imprimible y su anulación.
[RequierePermiso(Permisos.PagosVer)]
public class ReciboModel : PageModel
{
    private readonly GymDbContext _db;
    private readonly IUsuarioActual _usuario;
    public ReciboModel(GymDbContext db, IUsuarioActual usuario) { _db = db; _usuario = usuario; }

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty(SupportsGet = true)] public bool Imprimir { get; set; }

    public Pago Pago { get; private set; } = null!;
    public decimal? PagadoPeriodo { get; private set; }
    public ResumenSocio? Resumen { get; private set; }
    public bool PuedeAnular => !Pago.Anulado && _usuario.Puede(Permisos.PagosAnular);
    public string OperadorNombre => _usuario.Nombre;

    // Últimos pagos del socio (incluye el actual y los anulados) para el historial lateral
    public List<Pago> Historial { get; private set; } = new();
    public Dictionary<int, decimal> PagadoPorPeriodo { get; private set; } = new();
    public int TotalPagosSocio { get; private set; }

    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashError { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        // El detalle muestra el pago aunque esté anulado
        var p = await _db.Pagos.IgnoreQueryFilters().AsNoTracking()
            .Include(x => x.Socio).Include(x => x.SocioPlan).ThenInclude(sp => sp!.Plan)
            .FirstOrDefaultAsync(x => x.Id == Id);
        if (p is null) return NotFound();
        Pago = p;
        // Saldo del período: solo pagos vigentes (filtro global)
        if (p.SocioPlanId is { } spId)
            PagadoPeriodo = (await _db.Pagos.AsNoTracking().Where(x => x.SocioPlanId == spId).Select(x => x.Importe).ToListAsync()).Sum();

        var delSocio = _db.Pagos.IgnoreQueryFilters().AsNoTracking().Where(x => x.SocioId == p.SocioId);
        Historial = await delSocio.Include(x => x.SocioPlan).ThenInclude(sp => sp!.Plan)
            .OrderByDescending(x => x.FechaPago)
            .Take(6)
            .ToListAsync();
        if (Historial.All(x => x.Id != p.Id))
            Historial = Historial.Append(p).OrderByDescending(x => x.FechaPago).ToList();
        TotalPagosSocio = await delSocio.CountAsync();
        PagadoPorPeriodo = await PagoPresentacion.PagadoPorPeriodoAsync(_db, Historial);
        Resumen = (await SociosConsultas.ResumenAsync(_db, new[] { p.SocioId })).GetValueOrDefault(p.SocioId);
        return Page();
    }

    // Anulación lógica: el pago se conserva con su motivo, fecha y operador, y deja de contar en saldos e indicadores.
    [RequierePermiso(Permisos.PagosAnular)]
    public async Task<IActionResult> OnPostAnularAsync(string? motivo, string? detalle)
    {

        var p = await _db.Pagos.IgnoreQueryFilters().Include(x => x.Socio).FirstOrDefaultAsync(x => x.Id == Id);
        if (p is null) return NotFound();
        if (p.Anulado)
        {
            FlashError = "El pago ya estaba anulado.";
            return RedirectToPage(new { id = Id });
        }

        motivo = motivo?.Trim();
        detalle = string.IsNullOrWhiteSpace(detalle) ? null : detalle.Trim();
        if (string.IsNullOrEmpty(motivo) || !PagoPresentacion.MotivosAnulacion.Contains(motivo))
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

        p.Anular(texto, _usuario.Nombre, DateTime.UtcNow);
        _db.Auditar(TipoEventoSistema.PagoAnulado, EntidadAuditada.Pago, p.Id, $"{p.Socio.Apellido}, {p.Socio.Nombre}",
            $"{p.Numero()} · {SocioPresentacion.Dinero(p.Importe, p.Moneda)} · {p.MotivoAnulacion}", socioId: p.SocioId);
        await _db.SaveChangesAsync();

        Flash = $"Pago {p.Numero()} anulado. Ya no impacta en saldos ni recaudación.";
        return RedirectToPage(new { id = Id });
    }
}
