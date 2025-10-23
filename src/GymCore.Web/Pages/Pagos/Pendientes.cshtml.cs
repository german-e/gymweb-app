using GymCore.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Pagos;

public class PendientesModel : PageModel
{
    private readonly GymDbContext _db;
    public PendientesModel(GymDbContext db) => _db = db;

    public string? Q { get; set; }
    public List<ItemVM> Items { get; set; } = new();

    public class ItemVM
    {
        public int SocioId { get; set; }
        public int SocioPlanId { get; set; }
        public string Dni { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Plan { get; set; } = string.Empty;
        public string Moneda { get; set; } = "ARS";
        public decimal Precio { get; set; }
        public decimal Pagado { get; set; }
        public decimal Faltante => Math.Max(0m, Precio - Pagado);
        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }
    }

    public async Task OnGetAsync(string? q)
    {
        Q = (q ?? string.Empty).Trim();
        var ahora = DateTime.Now;

        // Suscripciones activas con total pagado < precio
        var baseQuery = _db.SocioPlanes
            .AsNoTracking()
            .Include(sp => sp.Socio)
            .Include(sp => sp.Plan)
            .Where(sp => sp.Estado == GymCore.Dominio.Enums.EstadoSuscripcion.Activo
                       && sp.FechaInicio <= ahora && sp.FechaFin >= ahora);

        if (!string.IsNullOrWhiteSpace(Q))
        {
            baseQuery = baseQuery.Where(sp => sp.Socio.Dni.Contains(Q) || sp.Socio.Apellido.Contains(Q) || sp.Socio.Nombre.Contains(Q));
        }

        Items = await baseQuery
            .Select(sp => new
            {
                sp.SocioId,
                SocioPlanId = sp.Id,
                Dni = sp.Socio.Dni,
                Nombre = sp.Socio.Apellido + ", " + sp.Socio.Nombre,
                Plan = sp.Plan.Nombre,
                Moneda = sp.Plan.Moneda,
                Precio = sp.Plan.Precio,
                Pagado = _db.Pagos.Where(p => p.SocioPlanId == sp.Id).Sum(p => (decimal?)p.Importe) ?? 0m,
                Desde = sp.FechaInicio,
                Hasta = sp.FechaFin
            })
            .Where(x => (x.Precio - x.Pagado) > 0m)
            .OrderByDescending(x => (double)(x.Precio - x.Pagado))
            .ThenBy(x => x.Nombre)
            .Take(500)
            .Select(x => new ItemVM
            {
                SocioId = x.SocioId,
                SocioPlanId = x.SocioPlanId,
                Dni = x.Dni,
                Nombre = x.Nombre,
                Plan = x.Plan,
                Moneda = x.Moneda,
                Precio = x.Precio,
                Pagado = x.Pagado,
                Desde = x.Desde,
                Hasta = x.Hasta
            })
            .ToListAsync();
    }
}
