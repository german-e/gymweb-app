using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Socios;

// Seguimiento de evolución física: a quién le toca medir. Un entrenador ve solo sus socios asignados.
[RequierePermiso(Permisos.EvolucionVer, Permisos.EvolucionRegistrar)]
public class EvolucionModel : PageModel
{
    private readonly GymDbContext _db;
    private readonly IUsuarioActual _usuario;
    public EvolucionModel(GymDbContext db, IUsuarioActual usuario) { _db = db; _usuario = usuario; }

    public const int DiasSugeridos = 30;   // frecuencia sugerida entre mediciones

    public record Fila(ResumenSocio R, int Mediciones, DateTime? Ultima, decimal? Peso, decimal? Graso, decimal? VariacionPeso)
    {
        public int? Dias => Ultima is { } u ? (DateTime.Today - u.ToLocalTime().Date).Days : null;
        public bool Pendiente => Dias is null or >= DiasSugeridos;
    }

    public List<Fila> Filas { get; private set; } = new();
    public bool SoloAsignados { get; private set; }

    public async Task OnGetAsync()
    {
        SoloAsignados = !_usuario.Puede(Permisos.SociosVer);
        List<int>? ids = SoloAsignados ? await SociosConsultas.AsignadosAsync(_db, _usuario.EntrenadorId) : null;
        var resumen = await SociosConsultas.ResumenAsync(_db, ids);
        var activos = resumen.Values.Where(r => !r.Socio.DadoDeBaja).ToList();
        var socioIds = activos.Select(r => r.Socio.Id).ToList();
        var mediciones = await _db.MedicionesFisicas.AsNoTracking()
            .Where(m => socioIds.Contains(m.SocioId))
            .OrderBy(m => m.Fecha)
            .Select(m => new { m.SocioId, m.Fecha, m.PesoKg, m.PorcentajeGraso })
            .ToListAsync();
        var porSocio = mediciones.GroupBy(m => m.SocioId).ToDictionary(g => g.Key, g => g.ToList());
        Filas = activos.Select(r =>
        {
            var ms = porSocio.GetValueOrDefault(r.Socio.Id);
            var ult = ms?.LastOrDefault();
            var conPeso = ms?.Where(m => m.PesoKg is not null).ToList();
            decimal? variacion = conPeso is { Count: >= 2 } ? conPeso[^1].PesoKg - conPeso[0].PesoKg : null;
            return new Fila(r, ms?.Count ?? 0, ult?.Fecha, ult?.PesoKg, ult?.PorcentajeGraso, variacion);
        })
        .OrderByDescending(f => f.Pendiente).ThenByDescending(f => f.Dias ?? int.MaxValue).ThenBy(f => f.R.Socio.Apellido)
        .ToList();
    }
}
