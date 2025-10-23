using System.ComponentModel.DataAnnotations;
using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GymCore.Web.Pages.Planes;

public class CreateModel : PageModel
{
    private readonly GymDbContext _db;
    public CreateModel(GymDbContext db) => _db = db;

    [BindProperty, Required, StringLength(120)] public string Nombre { get; set; } = string.Empty;
    [BindProperty] public string? Descripcion { get; set; }
    [BindProperty, Required] public TipoPlan Tipo { get; set; } = TipoPlan.MensualIlimitado;
    [BindProperty, Range(0, double.MaxValue)] public decimal Precio { get; set; }
    [BindProperty, Required, StringLength(10)] public string Moneda { get; set; } = "ARS";
    [BindProperty, Range(1, 365)] public int DuracionDias { get; set; } = 30;
    [BindProperty] public int? MaxDiasPorSemana { get; set; }
    [BindProperty] public bool PermiteAccesoSabado { get; set; } = true;
    [BindProperty] public bool PermiteAccesoDomingo { get; set; } = true;

    public IEnumerable<SelectListItem> Tipos => Enum.GetValues(typeof(TipoPlan))
        .Cast<TipoPlan>()
        .Select(t => new SelectListItem(t.ToString(), ((int)t).ToString()));

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var plan = new Plan
        {
            Nombre = Nombre.Trim(),
            Descripcion = string.IsNullOrWhiteSpace(Descripcion) ? null : Descripcion.Trim(),
            Tipo = Tipo,
            Precio = Precio,
            Moneda = Moneda.Trim(),
            DuracionDias = DuracionDias,
            MaxDiasPorSemana = MaxDiasPorSemana,
            PermiteAccesoSabado = PermiteAccesoSabado,
            PermiteAccesoDomingo = PermiteAccesoDomingo
        };
        _db.Planes.Add(plan);
        await _db.SaveChangesAsync();
        return RedirectToPage("/Planes/Planes");
    }
}
