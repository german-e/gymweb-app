using GymCore.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GymCore.Web.Pages.Socios
{
    public class SociosModel : PageModel
    {
        private readonly GymDbContext _db;
        public SociosModel(GymDbContext db) => _db = db;

        public List<ResumenSocio> Filas { get; private set; } = new();
        public List<string> Planes { get; private set; } = new();

        // KPIs
        public int Total { get; private set; }
        public int Activos { get; private set; }
        public int Morosos { get; private set; }
        public int NuevosMes { get; private set; }
        public int PorVencer { get; private set; }
        public int Bajas { get; private set; }
        public int ConEntrenador { get; private set; }

        // Búsqueda inicial (deep-link ?q=) y filtro inicial (?f=activo|moroso|porvencer|...)
        [BindProperty(SupportsGet = true)] public string? Q { get; set; }
        [BindProperty(SupportsGet = true)] public string? F { get; set; }
        [TempData] public string? Flash { get; set; }

        public async Task OnGetAsync()
        {
            var resumen = await SociosConsultas.ResumenAsync(_db);
            Filas = resumen.Values
                .OrderBy(r => r.Socio.Apellido).ThenBy(r => r.Socio.Nombre)
                .ToList();

            var activos = Filas.Where(r => !r.Socio.DadoDeBaja).ToList();
            var inicioMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

            Total = activos.Count;
            Activos = activos.Count(r => r.Vigente is not null);
            Morosos = activos.Count(r => r.Moroso);
            PorVencer = activos.Count(r => r.Estado == EstadoMembresia.PorVencer);
            NuevosMes = activos.Count(r => r.Socio.FechaAlta.ToLocalTime() >= inicioMes);
            ConEntrenador = activos.Count(r => r.Entrenador is not null);
            Bajas = Filas.Count - Total;
            Planes = Filas.Select(r => r.PlanNombre).Where(p => p is not null).Distinct().Order().ToList()!;
        }

        public async Task<IActionResult> OnPostToggleBajaAsync(int id)
        {
            Flash = await SocioAcciones.AlternarBajaAsync(_db, id);
            return RedirectToPage();
        }

        // Etiquetas para los filtros rápidos (components.js, data-facet-mode="tokens")
        public static string Etiquetas(ResumenSocio r)
        {
            var t = new List<string> { r.Estado.Clave() };
            if (r.Moroso) t.Add("moroso");
            if (r.Estado == EstadoMembresia.PorVencer) t.Add("porvencer");
            if (!r.Socio.DadoDeBaja) t.Add(r.Entrenador is null ? "sinentrenador" : "conentrenador");
            if (!r.Socio.DadoDeBaja) t.Add("vigentes"); // "Todos" excluye bajas
            return string.Join(' ', t);
        }
    }
}
