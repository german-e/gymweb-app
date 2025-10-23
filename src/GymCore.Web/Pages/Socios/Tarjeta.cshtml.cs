using GymCore.Infraestructura.Persistencia;
using GymCore.Dominio.Entidades;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Socios;

public class TarjetaModel : PageModel
{
    private readonly GymDbContext _db;
    public TarjetaModel(GymDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public Socio? Socio { get; set; }
    public string Codigo { get; set; } = string.Empty; // por ahora usamos DNI como código a escanear

    public async Task<IActionResult> OnGetAsync()
    {
        Socio = await _db.Socios.AsNoTracking().FirstOrDefaultAsync(s => s.Id == Id);
        if (Socio == null)
            return NotFound();

        // Preferir TarjetaCodigo; si no hay, usar DNI como fallback
        Codigo = string.IsNullOrWhiteSpace(Socio.TarjetaCodigo) ? Socio.Dni : Socio.TarjetaCodigo;
        return Page();
    }
}
