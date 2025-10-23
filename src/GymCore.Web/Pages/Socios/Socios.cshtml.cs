using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Aplicacion.Abstractiones;
using GymCore.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Socios
{
    public class SociosModel : PageModel
    {
        private readonly ISocioRepository _repo;
        private readonly GymDbContext _db;
        public SociosModel(ISocioRepository repo, GymDbContext db) { _repo = repo; _db = db; }

        public List<Socio> ListaSocios { get; set; } = new();
        // SocioId -> (Estado, FechaFin, PlanNombre)
        public Dictionary<int, (EstadoSuscripcion Estado, DateTime? FechaFin, string? PlanNombre)> EstadoSuscripcionPorSocio { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? Q { get; set; }

        public async Task OnGetAsync()
        {
            var todos = await _repo.ObtenerTodosAsync();
            var query = todos.AsQueryable();
            if (!string.IsNullOrWhiteSpace(Q))
            {
                var q = Q.Trim().ToLowerInvariant();
                query = query.Where(s =>
                    (!string.IsNullOrEmpty(s.Dni) && s.Dni.ToLowerInvariant().Contains(q))
                    || (!string.IsNullOrEmpty(s.Apellido) && s.Apellido.ToLowerInvariant().Contains(q))
                    || (!string.IsNullOrEmpty(s.Nombre) && s.Nombre.ToLowerInvariant().Contains(q))
                );
            }
            ListaSocios = query.ToList();

            // Mapear estado de suscripción actual o última por socio
            var ids = ListaSocios.Select(s => s.Id).ToList();
            if (ids.Count > 0)
            {
                var hoy = DateTime.Now.Date;
                var planes = await _db.SocioPlanes
                    .AsNoTracking()
                    .Include(sp => sp.Plan)
                    .Where(sp => ids.Contains(sp.SocioId))
                    .ToListAsync();

                EstadoSuscripcionPorSocio = planes
                    .GroupBy(sp => sp.SocioId)
                    .ToDictionary(
                        g => g.Key,
                        g => {
                            var activo = g.Where(sp => sp.Estado == EstadoSuscripcion.Activo && sp.FechaFin >= hoy)
                                           .OrderByDescending(sp => sp.FechaFin)
                                           .FirstOrDefault();
                            if (activo != null) return (EstadoSuscripcion.Activo, (DateTime?)activo.FechaFin, activo.Plan?.Nombre);
                            var ultimo = g.OrderByDescending(sp => sp.FechaFin).FirstOrDefault();
                            return ultimo != null ? (ultimo.Estado, (DateTime?)ultimo.FechaFin, ultimo.Plan?.Nombre) : (EstadoSuscripcion.Vencido, (DateTime?)null, (string?)null);
                        }
                    );
            }
        }
    }
}
