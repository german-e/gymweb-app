using GymCore.Web.Seguridad;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using GymCore.Dominio.Entidades;

namespace GymCore.Web.Pages.Pagos;

[RequierePermiso(Permisos.PagosVer)]
public class IndexModel : PageModel
{
    private readonly GymDbContext _db;
    public IndexModel(GymDbContext db) => _db = db;

    public List<Pago> Items { get; set; } = new();
    public Dictionary<int, decimal> PagadoPorPeriodo { get; private set; } = new();

    // KPIs y alertas (mismas definiciones que Panel y sidebar)
    public IndicadoresGimnasio.Cobranza Cobranza { get; private set; } = null!;
    public IndicadoresGimnasio.Membresias Membresias { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        // El listado muestra también los anulados (con su estado); los indicadores no los cuentan
        Items = await _db.Pagos
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(p => p.Socio)
            .Include(p => p.SocioPlan).ThenInclude(sp => sp!.Plan)
            .OrderByDescending(p => p.FechaPago)
            .Take(200)
            .ToListAsync();
        PagadoPorPeriodo = await PagoPresentacion.PagadoPorPeriodoAsync(_db, Items);

        (Cobranza, Membresias, _) = await IndicadoresGimnasio.DelRequestAsync(HttpContext, _db);
    }
}
