using System.ComponentModel.DataAnnotations;
using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Pagos;

public class NuevoModel : PageModel
{
    private readonly GymDbContext _db;
    public NuevoModel(GymDbContext db) => _db = db;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Display(Name = "DNI del socio")]
        public string Dni { get; set; } = string.Empty;

        // Permite seleccionar directamente por ID cuando se usa el buscador
        [Display(Name = "Socio")]
        public int? SocioId { get; set; }

        [Required]
        [Range(1, 1_000_000)]
        [Display(Name = "Importe")]
        public decimal Importe { get; set; }

        [Display(Name = "Moneda")]
        public string Moneda { get; set; } = "ARS";

        [Required]
        [Display(Name = "Medio de pago")]
        public MedioPago Medio { get; set; } = MedioPago.Efectivo;

        [Display(Name = "Referencia externa")]
        public string? ReferenciaExterna { get; set; }

        [Display(Name = "Observaciones")]
        public string? Observaciones { get; set; }
    }

    // Preselección desde la ficha del socio (?socioId=)
    public string? SocioPreseleccionado { get; set; }

    [BindProperty]
    public bool VolverAFicha { get; set; }

    public async Task OnGetAsync(int? socioId)
    {
        if (socioId is null) return;
        var s = await _db.Socios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == socioId);
        if (s is null) return;
        Input.SocioId = s.Id;
        Input.Dni = s.Dni;
        SocioPreseleccionado = $"{s.Apellido}, {s.Nombre} · DNI {s.Dni}";
        VolverAFicha = true;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        Socio? socio = null;
        if (Input.SocioId.HasValue)
        {
            socio = await _db.Socios
                .Include(s => s.Suscripciones)
                    .ThenInclude(sp => sp.Plan)
                .FirstOrDefaultAsync(s => s.Id == Input.SocioId.Value);
        }
        else
        {
            var dni = Input.Dni?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(dni))
            {
                ModelState.AddModelError(string.Empty, "Debe seleccionar un socio o ingresar DNI.");
                return Page();
            }
            socio = await _db.Socios
            .Include(s => s.Suscripciones)
                .ThenInclude(sp => sp.Plan)
                .FirstOrDefaultAsync(s => s.Dni == dni);
        }

        if (socio is null)
        {
            ModelState.AddModelError(string.Empty, "No se encontró el socio seleccionado.");
            return Page();
        }

        // Buscar suscripción activa (opcional)
        var ahora = DateTime.Now;
        var activa = socio.Suscripciones
            .Where(sp => sp.Estado == EstadoSuscripcion.Activo
                       && sp.FechaInicio <= ahora
                       && sp.FechaFin >= ahora)
            .OrderByDescending(sp => sp.FechaFin)
            .FirstOrDefault();

        var pago = new Pago
        {
            SocioId = socio.Id,
            SocioPlanId = activa?.Id,
            Importe = Input.Importe,
            Moneda = string.IsNullOrWhiteSpace(Input.Moneda) ? "ARS" : Input.Moneda.Trim().ToUpperInvariant(),
            Medio = Input.Medio,
            ReferenciaExterna = Input.ReferenciaExterna,
            Observaciones = Input.Observaciones,
            FechaPago = DateTime.UtcNow
        };

        _db.Pagos.Add(pago);
        await _db.SaveChangesAsync();

        if (VolverAFicha)
        {
            TempData["Flash"] = $"Pago de $ {pago.Importe:N0} registrado.";
            return RedirectToPage("/Socios/Detalle", null, new { id = socio.Id }, "tab-pagos");
        }
        TempData["PagoOk"] = $"Pago registrado para {socio.Apellido}, {socio.Nombre}.";
        return RedirectToPage("/Pagos/Index");
    }

    // Handlers auxiliares para búsqueda y saldo
    public async Task<IActionResult> OnGetBuscarSociosAsync(string term)
    {
        term = (term ?? string.Empty).Trim();
        var q = _db.Socios.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(term))
        {
            q = q.Where(s => s.Dni.Contains(term) || s.Apellido.Contains(term) || s.Nombre.Contains(term));
        }
        var items = await q.OrderBy(s => s.Apellido).ThenBy(s => s.Nombre)
            .Take(20)
            .Select(s => new {
                id = s.Id,
                dni = s.Dni,
                nombre = s.Apellido + ", " + s.Nombre
            })
            .ToListAsync();
        return new JsonResult(items);
    }

    public async Task<IActionResult> OnGetSaldoSocioAsync(int socioId)
    {
        var socio = await _db.Socios
            .Include(s => s.Suscripciones)
                .ThenInclude(sp => sp.Plan)
            .FirstOrDefaultAsync(s => s.Id == socioId);
        if (socio == null)
            return new JsonResult(new { ok = false, mensaje = "Socio no encontrado" });

        var ahora = DateTime.Now;
        var activa = socio.Suscripciones
            .Where(sp => sp.Estado == EstadoSuscripcion.Activo
                       && sp.FechaInicio <= ahora
                       && sp.FechaFin >= ahora)
            .OrderByDescending(sp => sp.FechaFin)
            .FirstOrDefault();

        if (activa == null)
            return new JsonResult(new { ok = true, sinPlan = true, faltante = (decimal?)null });

        var totalPagado = await _db.Pagos
            .Where(p => p.SocioId == socioId && p.SocioPlanId == activa.Id)
            .SumAsync(p => (decimal?)p.Importe) ?? 0m;

        var precio = activa.Plan.Precio;
        var faltante = Math.Max(0m, precio - totalPagado);

        return new JsonResult(new
        {
            ok = true,
            sinPlan = false,
            plan = activa.Plan.Nombre,
            precio,
            pagado = totalPagado,
            faltante,
            periodo = new { desde = activa.FechaInicio, hasta = activa.FechaFin },
            socioPlanId = activa.Id
        });
    }
}
