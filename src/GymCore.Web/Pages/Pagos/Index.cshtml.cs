using GymCore.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using GymCore.Dominio.Entidades;

namespace GymCore.Web.Pages.Pagos;

public class IndexModel : PageModel
{
    private readonly GymDbContext _db;
    public IndexModel(GymDbContext db) => _db = db;

    public List<Pago> Items { get; set; } = new();

    public async Task OnGetAsync()
    {
        Items = await _db.Pagos
            .AsNoTracking()
            .Include(p => p.Socio)
            .OrderByDescending(p => p.FechaPago)
            .Take(200)
            .ToListAsync();
    }
}
