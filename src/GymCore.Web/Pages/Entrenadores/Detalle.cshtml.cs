using GymCore.Web.Seguridad;
using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Entrenadores;

[RequierePermiso(Permisos.EntrenadoresVer)]
public class DetalleModel : PageModel
{
    private readonly GymDbContext _db;
    private readonly IWebHostEnvironment _env;
    public DetalleModel(GymDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public record SocioAsignado(
        int AsignacionId, int SocioId, string Nombre, string Dni, string? NumeroSocio,
        string Membresia, string MembresiaVariante, string? PlanNombre,
        DateTime? UltimaAsistencia, string? Objetivo, DateTime FechaAsignacion);

    public Entrenador Entrenador { get; private set; } = null!;
    public List<SocioAsignado> Socios { get; private set; } = new();
    public List<EntrenadorEvento> Historial { get; private set; } = new();

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [TempData] public string? Flash { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var e = await _db.Entrenadores.AsNoTracking().FirstOrDefaultAsync(x => x.Id == Id);
        if (e is null) return NotFound();
        Entrenador = e;

        var asignaciones = await _db.EntrenadorSocios.AsNoTracking()
            .Include(a => a.Socio)
            .Where(a => a.EntrenadorId == Id && a.Activa)
            .OrderBy(a => a.Socio.Apellido).ThenBy(a => a.Socio.Nombre)
            .ToListAsync();

        var socioIds = asignaciones.Select(a => a.SocioId).ToList();
        var membresias = await EstadoMembresiasAsync(socioIds);
        var ultimas = await _db.Asistencias.AsNoTracking()
            .Where(a => socioIds.Contains(a.SocioId))
            .GroupBy(a => a.SocioId)
            .Select(g => new { g.Key, Ultima = g.Max(a => a.FechaHoraIngreso) })
            .ToDictionaryAsync(x => x.Key, x => x.Ultima);

        Socios = asignaciones.Select(a =>
        {
            var m = membresias.GetValueOrDefault(a.SocioId, ("Sin plan", "muted", null));
            return new SocioAsignado(a.Id, a.SocioId, $"{a.Socio.Apellido}, {a.Socio.Nombre}", a.Socio.Dni, a.Socio.TarjetaCodigo,
                m.Texto, m.Variante, m.Plan,
                ultimas.TryGetValue(a.SocioId, out var u) ? u : null, a.Objetivo, a.FechaAsignacion);
        }).ToList();

        Historial = await _db.EntrenadorEventos.AsNoTracking()
            .Where(x => x.EntrenadorId == Id)
            .OrderByDescending(x => x.Fecha).ThenByDescending(x => x.Id)
            .Take(200)
            .ToListAsync();

        return Page();
    }

    // Búsqueda para el modal "Asignar socio" (nombre, DNI o número de socio)
    [RequierePermiso(Permisos.EntrenadoresEditar)]
    public async Task<IActionResult> OnGetBuscarSociosAsync(string? q)
    {
        q = q?.Trim();
        if (string.IsNullOrEmpty(q) || q.Length < 2) return new JsonResult(Array.Empty<object>());

        // Comparación sin acentos ni mayúsculas ("perez" encuentra "Pérez"). Volumen acotado de socios → en memoria.
        var qn = Normalizar(q);
        var esNumero = int.TryParse(q, out var numero);
        var socios = (await _db.Socios.AsNoTracking()
                .Select(s => new { s.Id, s.Nombre, s.Apellido, s.Dni, s.TarjetaCodigo })
                .ToListAsync())
            .Where(s => Normalizar($"{s.Nombre} {s.Apellido} {s.Apellido} {s.Nombre}").Contains(qn)
                     || s.Dni.Contains(q) || s.TarjetaCodigo == q || (esNumero && s.Id == numero))
            .OrderBy(s => s.Apellido).ThenBy(s => s.Nombre)
            .Take(25)
            .ToList();

        var ids = socios.Select(s => s.Id).ToList();
        var asignados = await _db.EntrenadorSocios
            .Where(a => a.EntrenadorId == Id && a.Activa && ids.Contains(a.SocioId))
            .Select(a => a.SocioId).ToListAsync();
        var membresias = await EstadoMembresiasAsync(ids);

        return new JsonResult(socios.Select(s =>
        {
            var m = membresias.GetValueOrDefault(s.Id, ("Sin plan", "muted", null));
            return new
            {
                id = s.Id,
                nombre = $"{s.Apellido}, {s.Nombre}",
                iniciales = $"{s.Nombre.FirstOrDefault()}{s.Apellido.FirstOrDefault()}".ToUpperInvariant(),
                dni = s.Dni,
                numero = s.TarjetaCodigo ?? s.Id.ToString(),
                membresia = m.Texto,
                variante = m.Variante,
                asignado = asignados.Contains(s.Id)
            };
        }));
    }

    [RequierePermiso(Permisos.EntrenadoresEditar)]
    public async Task<IActionResult> OnPostAsignarAsync(int[] socioIds, string? objetivo)
    {
        var e = await _db.Entrenadores.FirstOrDefaultAsync(x => x.Id == Id);
        if (e is null) return NotFound();
        if (!e.Activo)
        {
            Flash = "No se pueden asignar socios a un entrenador inactivo.";
            return RedirectToPage(null, null, new { id = Id }, "tab-socios");
        }

        socioIds = socioIds.Distinct().ToArray();
        var socios = await _db.Socios.Where(s => socioIds.Contains(s.Id)).ToListAsync();
        var existentes = await _db.EntrenadorSocios.Where(a => a.EntrenadorId == Id && socioIds.Contains(a.SocioId)).ToListAsync();
        objetivo = string.IsNullOrWhiteSpace(objetivo) ? null : objetivo.Trim();

        var nuevos = new List<string>();
        var nuevosIds = new List<int>();
        foreach (var s in socios)
        {
            var a = existentes.FirstOrDefault(x => x.SocioId == s.Id);
            if (a is { Activa: true }) continue;
            if (a is null)
            {
                _db.EntrenadorSocios.Add(new EntrenadorSocio { EntrenadorId = Id, SocioId = s.Id, Objetivo = objetivo });
            }
            else
            {
                // Reasignación: se reutiliza el registro (índice único entrenador+socio)
                a.Activa = true;
                a.FechaFin = null;
                a.FechaAsignacion = DateTime.UtcNow;
                a.Objetivo = objetivo ?? a.Objetivo;
            }
            nuevos.Add($"{s.Apellido}, {s.Nombre}");
            nuevosIds.Add(s.Id);
        }

        if (nuevos.Count > 0)
        {
            _db.RegistrarEvento(e, TipoEventoEntrenador.AsignacionSocio,
                nuevos.Count == 1 ? "Socio asignado" : $"{nuevos.Count} socios asignados",
                string.Join("\n", nuevos) + (objetivo is null ? "" : $"\nObjetivo: {objetivo}"),
                nuevosIds.Count == 1 ? nuevosIds[0] : null);
            await _db.SaveChangesAsync();
            Flash = nuevos.Count == 1 ? $"Socio asignado: {nuevos[0]}." : $"Se asignaron {nuevos.Count} socios.";
        }
        else
        {
            Flash = "Los socios seleccionados ya estaban asignados.";
        }
        return RedirectToPage(null, null, new { id = Id }, "tab-socios");
    }

    [RequierePermiso(Permisos.EntrenadoresEditar)]
    public async Task<IActionResult> OnPostDesasignarAsync(int asignacionId)
    {
        var a = await _db.EntrenadorSocios.Include(x => x.Socio).Include(x => x.Entrenador)
            .FirstOrDefaultAsync(x => x.Id == asignacionId && x.EntrenadorId == Id);
        if (a is null) return NotFound();

        a.Activa = false;
        a.FechaFin = DateTime.UtcNow;
        _db.RegistrarEvento(a.Entrenador, TipoEventoEntrenador.DesasignacionSocio, "Socio desasignado", $"{a.Socio.Apellido}, {a.Socio.Nombre}", a.SocioId);
        await _db.SaveChangesAsync();
        Flash = $"Se quitó la asignación de {a.Socio.Apellido}, {a.Socio.Nombre}.";
        return RedirectToPage(null, null, new { id = Id }, "tab-socios");
    }

    [RequierePermiso(Permisos.EntrenadoresEditar)]
    public async Task<IActionResult> OnPostObservacionAsync(string? texto)
    {
        var e = await _db.Entrenadores.FirstOrDefaultAsync(x => x.Id == Id);
        if (e is null) return NotFound();
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim();
            _db.RegistrarEvento(e, TipoEventoEntrenador.Observacion, "Observación", t.Length > 2000 ? t[..2000] : t);
            await _db.SaveChangesAsync();
            Flash = "Observación registrada.";
        }
        return RedirectToPage(null, null, new { id = Id }, "tab-historial");
    }

    [RequierePermiso(Permisos.EntrenadoresEditar)]
    public async Task<IActionResult> OnPostToggleEstadoAsync()
    {
        Flash = await EntrenadorAcciones.AlternarEstadoAsync(_db, Id);
        return RedirectToPage(new { id = Id });
    }

    [RequierePermiso(Permisos.EntrenadoresEditar)]
    public async Task<IActionResult> OnPostEliminarAsync()
    {
        TempData["Flash"] = await EntrenadorAcciones.EliminarAsync(_db, _env, Id);
        return RedirectToPage("Index");
    }

    private static string Normalizar(string s) =>
        new string(s.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray()).ToLowerInvariant();

    // SocioId -> (texto, variante de color, plan): mismo criterio que el módulo de Socios
    private async Task<Dictionary<int, (string Texto, string Variante, string? Plan)>> EstadoMembresiasAsync(List<int> socioIds)
    {
        if (socioIds.Count == 0) return new();
        var resumen = await global::GymCore.Web.Pages.Socios.SociosConsultas.ResumenAsync(_db, socioIds);
        return resumen.ToDictionary(kv => kv.Key, kv => (global::GymCore.Web.Pages.Socios.SocioPresentacion.Texto(kv.Value.Estado), global::GymCore.Web.Pages.Socios.SocioPresentacion.Variante(kv.Value.Estado), kv.Value.PlanNombre));
    }
}
