using System.ComponentModel.DataAnnotations;
using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Planes;

public class EditModel : PageModel
{
    private readonly GymDbContext _db;
    public EditModel(GymDbContext db) => _db = db;

    [BindProperty] public int Id { get; set; }
    [BindProperty, Required, StringLength(120)] public string Nombre { get; set; } = string.Empty;
    [BindProperty] public string? Descripcion { get; set; }
    [BindProperty, Required] public TipoPlan Tipo { get; set; }
    [BindProperty, Range(0, double.MaxValue)] public decimal Precio { get; set; }
    [BindProperty, Required, StringLength(10)] public string Moneda { get; set; } = "ARS";
    [BindProperty, Range(1, 365)] public int DuracionDias { get; set; } = 30;
    [BindProperty] public int? MaxDiasPorSemana { get; set; }
    [BindProperty] public bool PermiteAccesoSabado { get; set; } = true;
    [BindProperty] public bool PermiteAccesoDomingo { get; set; } = true;

    public IEnumerable<SelectListItem> Tipos => Enum.GetValues(typeof(TipoPlan))
        .Cast<TipoPlan>()
        .Select(t => new SelectListItem(t.ToString(), ((int)t).ToString()));

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var p = await _db.Planes.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return RedirectToPage("/Planes/Planes");
        Id = p.Id; Nombre = p.Nombre; Descripcion = p.Descripcion; Tipo = p.Tipo; Precio = p.Precio; Moneda = p.Moneda; DuracionDias = p.DuracionDias; MaxDiasPorSemana = p.MaxDiasPorSemana; PermiteAccesoSabado = p.PermiteAccesoSabado; PermiteAccesoDomingo = p.PermiteAccesoDomingo;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var p = await _db.Planes.FirstOrDefaultAsync(x => x.Id == Id);
        if (p is null) return RedirectToPage("/Planes/Planes");
        p.Nombre = Nombre.Trim();
        p.Descripcion = string.IsNullOrWhiteSpace(Descripcion) ? null : Descripcion.Trim();
        p.Tipo = Tipo;
        p.Precio = Precio;
        p.Moneda = Moneda.Trim();
        p.DuracionDias = DuracionDias;
        p.MaxDiasPorSemana = MaxDiasPorSemana;
        p.PermiteAccesoSabado = PermiteAccesoSabado;
        p.PermiteAccesoDomingo = PermiteAccesoDomingo;
        await _db.SaveChangesAsync();
        return RedirectToPage("/Planes/Planes");
    }
}
