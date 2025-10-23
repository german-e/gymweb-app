using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Planes;

public class DeleteModel : PageModel
{
    private readonly GymDbContext _db;
    public DeleteModel(GymDbContext db) => _db = db;

    [BindProperty] public int Id { get; set; }
    [BindProperty] public string Nombre { get; set; } = string.Empty;
    [BindProperty] public TipoPlan Tipo { get; set; }
    [BindProperty] public decimal Precio { get; set; }
    [BindProperty] public string Moneda { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var p = await _db.Planes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return RedirectToPage("/Planes/Planes");
        Id = p.Id; Nombre = p.Nombre; Tipo = p.Tipo; Precio = p.Precio; Moneda = p.Moneda;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var p = await _db.Planes.FirstOrDefaultAsync(x => x.Id == Id);
        if (p is not null)
        {
            _db.Planes.Remove(p);
            await _db.SaveChangesAsync();
        }
        return RedirectToPage("/Planes/Planes");
    }
}
