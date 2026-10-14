using GymCore.Dominio.Entidades;
using GymCore.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Pagos;

// Comprobante imprimible de un pago (solo lectura).
public class ReciboModel : PageModel
{
    private readonly GymDbContext _db;
    public ReciboModel(GymDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty(SupportsGet = true)] public bool Imprimir { get; set; }

    public Pago Pago { get; private set; } = null!;
    public decimal? PagadoPeriodo { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var p = await _db.Pagos.AsNoTracking().Include(x => x.Socio).Include(x => x.SocioPlan).ThenInclude(sp => sp!.Plan)
            .FirstOrDefaultAsync(x => x.Id == Id);
        if (p is null) return NotFound();
        Pago = p;
        if (p.SocioPlanId is { } spId)
            PagadoPeriodo = (await _db.Pagos.AsNoTracking().Where(x => x.SocioPlanId == spId).Select(x => x.Importe).ToListAsync()).Sum();
        return Page();
    }
}
