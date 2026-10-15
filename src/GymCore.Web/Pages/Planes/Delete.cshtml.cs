using GymCore.Web.Seguridad;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Planes;

[RequierePermiso(Permisos.PlanesEditar)]
public class DeleteModel : PageModel
{
    private readonly GymDbContext _db;
    public DeleteModel(GymDbContext db) => _db = db;

    [BindProperty] public int Id { get; set; }
    [BindProperty] public string Nombre { get; set; } = string.Empty;
    [BindProperty] public TipoPlan Tipo { get; set; }
    [BindProperty] public decimal Precio { get; set; }
    [BindProperty] public string Moneda { get; set; } = string.Empty;

    // Suscripciones que referencian al plan: la base no permite borrarlo (FK Restrict)
    public int Suscripciones { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var p = await _db.Planes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return RedirectToPage("/Planes/Planes");
        Id = p.Id; Nombre = p.Nombre; Tipo = p.Tipo; Precio = p.Precio; Moneda = p.Moneda;
        Suscripciones = await _db.SocioPlanes.CountAsync(sp => sp.PlanId == id);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var p = await _db.Planes.FirstOrDefaultAsync(x => x.Id == Id);
        if (p is not null)
        {
            if (await _db.SocioPlanes.AnyAsync(sp => sp.PlanId == p.Id))
            {
                TempData["FlashError"] = $"No se puede eliminar {p.Nombre}: tiene suscripciones registradas.";
                return RedirectToPage("/Planes/Planes");
            }
            _db.Auditar(TipoEventoSistema.PlanEliminado, EntidadAuditada.Plan, p.Id, p.Nombre,
                GymCore.Web.Pages.Socios.SocioPresentacion.Dinero(p.Precio, p.Moneda));
            _db.Planes.Remove(p);
            await _db.SaveChangesAsync();
            TempData["Flash"] = $"Se eliminó el plan {p.Nombre}.";
        }
        return RedirectToPage("/Planes/Planes");
    }
}
