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
        [Required]
        [Display(Name = "DNI del socio")]
        public string Dni { get; set; } = string.Empty;

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

    public void OnGet()
    {
        // Solo renderiza el formulario
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var dni = Input.Dni.Trim();
        var socio = await _db.Socios
            .Include(s => s.Suscripciones)
                .ThenInclude(sp => sp.Plan)
            .FirstOrDefaultAsync(s => s.Dni == dni);

        if (socio is null)
        {
            ModelState.AddModelError(string.Empty, $"No se encontró un socio con DNI {dni}.");
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

        TempData["PagoOk"] = $"Pago registrado para {socio.Apellido}, {socio.Nombre}.";
        return RedirectToPage("/Pagos/Index");
    }
}
