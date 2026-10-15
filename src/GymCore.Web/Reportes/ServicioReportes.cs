namespace GymCore.Web.Reportes;

// Punto de entrada de las páginas de inteligencia de negocio. Obtiene los hechos una vez por request y
// delega en MetricasNegocio; memoriza lo que varias pantallas comparten (KPIs, riesgo).
public sealed class ServicioReportes
{
    private readonly IFuenteHechos _fuente;
    public ServicioReportes(IFuenteHechos fuente) => _fuente = fuente;

    private IReadOnlyList<Kpi>? _kpis;
    private ReporteRiesgo? _riesgo;

    public Task<HechosNegocio> HechosAsync() => _fuente.ObtenerAsync();

    public async Task<ResumenEjecutivo> ResumenAsync()
    {
        var h = await _fuente.ObtenerAsync();
        var kpis = _kpis ??= MetricasNegocio.KpisEjecutivos(h);
        var riesgo = _riesgo ??= MetricasNegocio.Riesgo(h);
        var tresMeses = Periodo.Crear("3m", h.Hoy);
        var planes = MetricasNegocio.Planes(h, tresMeses);
        var entrenadores = MetricasNegocio.Entrenadores(h, tresMeses);
        return new ResumenEjecutivo(kpis,
            MetricasNegocio.Insights(h, kpis, planes, entrenadores, riesgo),
            MetricasNegocio.Alertas(h, kpis, planes, entrenadores, riesgo),
            riesgo, h.Hoy);
    }

    public async Task<ReporteSocios> SociosAsync(Periodo p) => MetricasNegocio.Socios(await _fuente.ObtenerAsync(), p);
    public async Task<ReporteIngresos> IngresosAsync(Periodo p) => MetricasNegocio.Ingresos(await _fuente.ObtenerAsync(), p);
    public async Task<ReportePlanes> PlanesAsync(Periodo p) => MetricasNegocio.Planes(await _fuente.ObtenerAsync(), p);
    public async Task<ReporteEntrenadores> EntrenadoresAsync(Periodo p) => MetricasNegocio.Entrenadores(await _fuente.ObtenerAsync(), p);
    public async Task<ReporteRiesgo> RiesgoAsync() => _riesgo ??= MetricasNegocio.Riesgo(await _fuente.ObtenerAsync());

    public async Task<Periodo> PeriodoAsync(string? clave, DateOnly? desde, DateOnly? hasta) =>
        Periodo.Crear(clave, (await _fuente.ObtenerAsync()).Hoy, desde, hasta);
}
