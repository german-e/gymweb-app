using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Pages.Entrenadores;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Socios;

public class DetalleModel : PageModel
{
    private readonly GymDbContext _db;
    public DetalleModel(GymDbContext db) => _db = db;

    public record SuscripcionFila(SocioPlan Suscripcion, decimal Pagado)
    {
        public decimal Saldo => Math.Max(0m, Suscripcion.Plan.Precio - Pagado);
    }
    public record EventoActividad(DateTime FechaUtc, string Icono, string Variante, string Titulo, string? Detalle);

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashError { get; set; }

    public ResumenSocio R { get; private set; } = null!;
    public Socio Socio => R.Socio;

    public List<SuscripcionFila> Suscripciones { get; private set; } = new();
    public List<Pago> Pagos { get; private set; } = new();
    public List<Asistencia> Asistencias { get; private set; } = new();   // historial (últimas 200)
    public Dictionary<string, int> CalendarioAsistencia { get; private set; } = new(); // yyyy-MM-dd local -> cantidad (último año)
    public List<int> AsistenciasPorSemana { get; private set; } = new(); // últimas 12 semanas (lunes a domingo), la última es la actual
    public List<EntrenadorSocio> HistorialEntrenadores { get; private set; } = new();
    public List<Entrenador> EntrenadoresDisponibles { get; private set; } = new();
    public List<MedicionFisica> Mediciones { get; private set; } = new();
    public List<EventoActividad> Actividad { get; private set; } = new();

    // KPIs
    public int DiasComoSocio { get; private set; }
    public int AsistenciasMes { get; private set; }
    public int AsistenciasAnio { get; private set; }
    public double PromedioSemanal { get; private set; }
    public int CuotasPagadas => Pagos.Count;
    public decimal TotalPagado => Pagos.Sum(p => p.Importe);
    public int Renovaciones => Math.Max(0, Suscripciones.Count - 1);

    public async Task<IActionResult> OnGetAsync()
    {
        var resumen = await SociosConsultas.ResumenAsync(_db, new[] { Id });
        if (!resumen.TryGetValue(Id, out var r)) return NotFound();
        R = r;

        var hoy = DateTime.Today;
        DiasComoSocio = Math.Max(0, (hoy - Socio.FechaAlta.ToLocalTime().Date).Days);

        // Membresía
        var subs = await _db.SocioPlanes.AsNoTracking().Include(sp => sp.Plan)
            .Where(sp => sp.SocioId == Id).OrderByDescending(sp => sp.FechaInicio).ToListAsync();

        // Pagos (con su suscripción para el concepto)
        Pagos = await _db.Pagos.AsNoTracking().Include(p => p.SocioPlan).ThenInclude(sp => sp!.Plan)
            .Where(p => p.SocioId == Id).OrderByDescending(p => p.FechaPago).ToListAsync();
        var pagadoPorSub = Pagos.Where(p => p.SocioPlanId != null).GroupBy(p => p.SocioPlanId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Importe));
        Suscripciones = subs.Select(s => new SuscripcionFila(s, pagadoPorSub.GetValueOrDefault(s.Id))).ToList();

        // Asistencia
        var desdeUtc = DateTime.SpecifyKind(hoy.AddDays(-366), DateTimeKind.Local).ToUniversalTime();
        var ingresosAnio = await _db.Asistencias.AsNoTracking()
            .Where(a => a.SocioId == Id && a.FechaHoraIngreso >= desdeUtc)
            .Select(a => a.FechaHoraIngreso).ToListAsync();
        var localesAnio = ingresosAnio.Select(f => f.ToLocalTime()).ToList();
        CalendarioAsistencia = localesAnio.GroupBy(f => f.ToString("yyyy-MM-dd")).ToDictionary(g => g.Key, g => g.Count());
        AsistenciasMes = localesAnio.Count(f => f.Year == hoy.Year && f.Month == hoy.Month);
        AsistenciasAnio = localesAnio.Count(f => f.Year == hoy.Year);
        var lunes = hoy.AddDays(-(((int)hoy.DayOfWeek + 6) % 7));
        AsistenciasPorSemana = Enumerable.Range(0, 12).Select(i => lunes.AddDays(-7 * (11 - i)))
            .Select(ini => localesAnio.Count(f => f.Date >= ini && f.Date < ini.AddDays(7))).ToList();
        PromedioSemanal = Math.Round(AsistenciasPorSemana.Take(11).Average(), 1); // 11 semanas completas

        Asistencias = await _db.Asistencias.AsNoTracking().Include(a => a.SocioPlan).ThenInclude(sp => sp!.Plan)
            .Where(a => a.SocioId == Id).OrderByDescending(a => a.FechaHoraIngreso).Take(200).ToListAsync();

        // Entrenador
        HistorialEntrenadores = await _db.EntrenadorSocios.AsNoTracking().Include(a => a.Entrenador)
            .Where(a => a.SocioId == Id).OrderByDescending(a => a.Activa).ThenByDescending(a => a.FechaAsignacion).ToListAsync();
        EntrenadoresDisponibles = await _db.Entrenadores.AsNoTracking().Where(e => e.Activo)
            .OrderBy(e => e.Apellido).ThenBy(e => e.Nombre).ToListAsync();

        // Evolución física
        Mediciones = await _db.MedicionesFisicas.AsNoTracking().Where(m => m.SocioId == Id)
            .OrderBy(m => m.Fecha).ToListAsync();

        Actividad = ArmarActividad();
        return Page();
    }

    private List<EventoActividad> ArmarActividad()
    {
        var ev = new List<EventoActividad>
        {
            new(Socio.FechaAlta, "bi-person-plus", "success", "Alta como socio", $"N° {SocioPresentacion.Numero(Socio.NumeroVisible)}")
        };
        if (Socio.FechaBaja is { } baja)
            ev.Add(new(baja, "bi-person-x", "danger", "Baja del socio", null));

        foreach (var s in Suscripciones)
        {
            var inicioUtc = DateTime.SpecifyKind(s.Suscripcion.FechaInicio, DateTimeKind.Local).ToUniversalTime();
            ev.Add(new(inicioUtc, "bi-card-checklist", "primary", $"Suscripción {s.Suscripcion.Plan.Nombre}",
                $"{s.Suscripcion.FechaInicio:dd/MM/yyyy} → {s.Suscripcion.FechaFin:dd/MM/yyyy}"));
        }
        foreach (var p in Pagos)
            ev.Add(new(p.FechaPago, "bi-cash-coin", "success", $"Pago {SocioPresentacion.Dinero(p.Importe, p.Moneda)}",
                $"{p.Medio.Texto()}{(p.SocioPlan?.Plan is { } pl ? $" · {pl.Nombre}" : "")}"));
        foreach (var a in HistorialEntrenadores)
        {
            ev.Add(new(a.FechaAsignacion, "bi-person-badge", "info", $"Entrenador asignado: {a.Entrenador.NombreCompleto}", a.Objetivo is null ? null : $"Objetivo: {a.Objetivo}"));
            if (a.FechaFin is { } fin)
                ev.Add(new(fin, "bi-person-dash", "warning", $"Fin de asignación: {a.Entrenador.NombreCompleto}", null));
        }
        foreach (var m in Mediciones)
            ev.Add(new(m.Fecha, "bi-activity", "muted", "Medición física",
                string.Join(" · ", new[] { m.PesoKg is { } p ? $"{p:0.#} kg" : null, m.Imc is { } i ? $"IMC {i:0.0}" : null, m.PorcentajeGraso is { } g ? $"{g:0.#}% graso" : null }.Where(x => x != null))));

        return ev.OrderByDescending(e => e.FechaUtc).Take(150).ToList();
    }

    // ---------- Acciones ----------

    public async Task<IActionResult> OnPostToggleBajaAsync()
    {
        Flash = await SocioAcciones.AlternarBajaAsync(_db, Id);
        return RedirectToPage(new { id = Id });
    }

    // Emite un código nuevo para la credencial. El anterior deja de funcionar; el DNI sigue habilitado en el check-in.
    public async Task<IActionResult> OnPostRegenerarCredencialAsync()
    {
        var s = await _db.Socios.FirstOrDefaultAsync(x => x.Id == Id);
        if (s is null) return NotFound();

        string codigo;
        do { codigo = RandomNumberGenerator.GetInt32(100_000_000, 999_999_999).ToString() + RandomNumberGenerator.GetInt32(0, 10); }
        while (await _db.Socios.AnyAsync(x => x.TarjetaCodigo == codigo));

        s.TarjetaCodigo = codigo;
        await _db.SaveChangesAsync();
        Flash = "Se emitió una nueva credencial. La anterior ya no es válida.";
        return RedirectToPage(null, null, new { id = Id }, "tab-credencial");
    }

    public async Task<IActionResult> OnPostAsignarEntrenadorAsync(int entrenadorId, string? objetivo, bool reemplazar = true)
    {
        var socio = await _db.Socios.FirstOrDefaultAsync(s => s.Id == Id);
        var entrenador = await _db.Entrenadores.FirstOrDefaultAsync(e => e.Id == entrenadorId && e.Activo);
        if (socio is null) return NotFound();
        if (entrenador is null)
        {
            FlashError = "El entrenador no existe o está inactivo.";
            return RedirectToPage(null, null, new { id = Id }, "tab-entrenador");
        }
        objetivo = string.IsNullOrWhiteSpace(objetivo) ? null : objetivo.Trim()[..Math.Min(objetivo.Trim().Length, 200)];
        var nombreSocio = $"{socio.Apellido}, {socio.Nombre}";

        var asignaciones = await _db.EntrenadorSocios.Include(a => a.Entrenador).Where(a => a.SocioId == Id).ToListAsync();
        if (reemplazar)
        {
            foreach (var a in asignaciones.Where(a => a.Activa && a.EntrenadorId != entrenadorId))
            {
                a.Activa = false;
                a.FechaFin = DateTime.UtcNow;
                _db.RegistrarEvento(a.Entrenador, TipoEventoEntrenador.DesasignacionSocio, "Socio desasignado", $"{nombreSocio} (cambio de entrenador)");
            }
        }

        var existente = asignaciones.FirstOrDefault(a => a.EntrenadorId == entrenadorId);
        if (existente is { Activa: true })
        {
            existente.Objetivo = objetivo ?? existente.Objetivo;
        }
        else
        {
            if (existente is null)
                _db.EntrenadorSocios.Add(new EntrenadorSocio { EntrenadorId = entrenadorId, SocioId = Id, Objetivo = objetivo });
            else
            {
                existente.Activa = true;
                existente.FechaFin = null;
                existente.FechaAsignacion = DateTime.UtcNow;
                existente.Objetivo = objetivo ?? existente.Objetivo;
            }
            _db.RegistrarEvento(entrenador, TipoEventoEntrenador.AsignacionSocio, "Socio asignado",
                nombreSocio + (objetivo is null ? "" : $"\nObjetivo: {objetivo}"));
        }

        await _db.SaveChangesAsync();
        Flash = $"Entrenador asignado: {entrenador.NombreCompleto}.";
        return RedirectToPage(null, null, new { id = Id }, "tab-entrenador");
    }

    public async Task<IActionResult> OnPostQuitarEntrenadorAsync(int asignacionId)
    {
        var a = await _db.EntrenadorSocios.Include(x => x.Entrenador).Include(x => x.Socio)
            .FirstOrDefaultAsync(x => x.Id == asignacionId && x.SocioId == Id && x.Activa);
        if (a is null) return NotFound();
        a.Activa = false;
        a.FechaFin = DateTime.UtcNow;
        _db.RegistrarEvento(a.Entrenador, TipoEventoEntrenador.DesasignacionSocio, "Socio desasignado", $"{a.Socio.Apellido}, {a.Socio.Nombre}");
        await _db.SaveChangesAsync();
        Flash = $"Se quitó a {a.Entrenador.NombreCompleto} como entrenador.";
        return RedirectToPage(null, null, new { id = Id }, "tab-entrenador");
    }

    // Los <input type="number"> envían punto decimal; se parsean con cultura invariante
    // para no depender del idioma del servidor (acepta también coma).
    public class MedicionInput
    {
        [DataType(DataType.Date)] public DateTime Fecha { get; set; } = DateTime.Today;
        public string? PesoKg { get; set; }
        public string? AlturaCm { get; set; }
        public string? MasaMuscularKg { get; set; }
        public string? PorcentajeGraso { get; set; }
        [StringLength(1000)] public string? Observaciones { get; set; }
        public int? EntrenadorId { get; set; }

        public static decimal? Numero(string? v) =>
            decimal.TryParse(v?.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;

        public static bool EnRango(decimal? v, decimal min, decimal max) => v is null || (v >= min && v <= max);
    }

    public async Task<IActionResult> OnPostAgregarMedicionAsync(MedicionInput medicion)
    {
        if (!await _db.Socios.AnyAsync(s => s.Id == Id)) return NotFound();
        var peso = MedicionInput.Numero(medicion.PesoKg);
        var alturaIn = MedicionInput.Numero(medicion.AlturaCm);
        var masa = MedicionInput.Numero(medicion.MasaMuscularKg);
        var graso = MedicionInput.Numero(medicion.PorcentajeGraso);
        var valido = ModelState.IsValid && medicion.Fecha.Date <= DateTime.Today
            && MedicionInput.EnRango(peso, 20, 350) && MedicionInput.EnRango(alturaIn, 80, 250)
            && MedicionInput.EnRango(masa, 5, 150) && MedicionInput.EnRango(graso, 2, 70)
            && (peso ?? masa ?? graso) is not null;
        if (!valido)
        {
            FlashError = "Revisá los valores de la medición (peso 20–350 kg, altura 80–250 cm, % graso 2–70).";
            return RedirectToPage(null, null, new { id = Id }, "tab-evolucion");
        }

        // Si no se informa altura, se reutiliza la última conocida para calcular el IMC
        var altura = alturaIn ?? await _db.MedicionesFisicas.Where(m => m.SocioId == Id && m.AlturaCm != null)
            .OrderByDescending(m => m.Fecha).Select(m => m.AlturaCm).FirstOrDefaultAsync();

        var fecha = medicion.Fecha.Date == DateTime.Today ? DateTime.UtcNow : DateTime.SpecifyKind(medicion.Fecha.Date.AddHours(12), DateTimeKind.Local).ToUniversalTime();
        _db.MedicionesFisicas.Add(new MedicionFisica
        {
            SocioId = Id,
            Fecha = fecha,
            PesoKg = peso,
            AlturaCm = altura,
            MasaMuscularKg = masa,
            PorcentajeGraso = graso,
            Observaciones = string.IsNullOrWhiteSpace(medicion.Observaciones) ? null : medicion.Observaciones.Trim(),
            EntrenadorId = medicion.EntrenadorId
        });
        await _db.SaveChangesAsync();
        Flash = "Medición registrada.";
        return RedirectToPage(null, null, new { id = Id }, "tab-evolucion");
    }

    public async Task<IActionResult> OnPostEliminarMedicionAsync(int medicionId)
    {
        var m = await _db.MedicionesFisicas.FirstOrDefaultAsync(x => x.Id == medicionId && x.SocioId == Id);
        if (m is not null)
        {
            _db.MedicionesFisicas.Remove(m);
            await _db.SaveChangesAsync();
            Flash = "Medición eliminada.";
        }
        return RedirectToPage(null, null, new { id = Id }, "tab-evolucion");
    }
}
