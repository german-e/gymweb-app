using System.Text.Json;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GymCore.Web.Reportes;

// Formatos y textos compartidos por las vistas de reportes y las exportaciones.
public static class Presentacion
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Configuración de gráfico serializada para data-config (Razor la escapa como atributo)
    public static string Config(object cfg) => JsonSerializer.Serialize(cfg, Json);

    public static string Valor(decimal v, FormatoKpi f) => f switch
    {
        FormatoKpi.Dinero => MetricasNegocio.Dinero(v),
        FormatoKpi.Porcentaje => v.ToString("0.#", Periodo.Es) + "%",
        FormatoKpi.Decimal => v.ToString("0.#", Periodo.Es),
        _ => v.ToString("N0", Periodo.Es)
    };

    public static string FormatoJs(FormatoKpi f) => f switch
    {
        FormatoKpi.Dinero => "dinero", FormatoKpi.Porcentaje => "porcentaje", FormatoKpi.Decimal => "decimal", _ => "numero"
    };

    // "▲ 12,3%" · "▼ 2,1 pp" · "▲ 23,9×" (más de +300%: el multiplicador se lee mejor) · "= sin cambios" · null sin base
    public static string? Variacion(Kpi k)
    {
        if (k.Variacion is not { } v) return null;
        if (v == 0) return "= sin cambios";
        if (!k.EnPuntos && v > 300 && k.Anterior is > 0 and var a) return $"▲ {(k.Valor / a).ToString("0.#", Periodo.Es)}×";
        return $"{(v > 0 ? "▲" : "▼")} {Math.Abs(v).ToString("0.#", Periodo.Es)}{(k.EnPuntos ? " pp" : "%")}";
    }

    // Clase de color de la variación: según si el movimiento es bueno o malo para el negocio
    public static string ClaseVariacion(Kpi k) => k.EsBueno switch { true => "delta-good", false => "delta-bad", _ => "delta-neutral" };

    public static (string Texto, string Icono, string Variante) Estilo(this NivelRiesgo n) => n switch
    {
        NivelRiesgo.Alto => ("Alto", "bi-exclamation-octagon-fill", "danger"),
        NivelRiesgo.Medio => ("Medio", "bi-exclamation-triangle-fill", "warning"),
        _ => ("Bajo", "bi-check-circle-fill", "success")
    };

    public static string Mes(DateOnly m) => Periodo.NombreMes(m, true);
}

// Base de las páginas de Inteligencia de negocio: permiso de lectura y período (?p=3m|6m|12m|custom&desde&hasta)
[RequierePermiso(Permisos.ReportesVer)]
public abstract class ReportesPageModel : PageModel
{
    protected readonly ServicioReportes Reportes;
    protected ReportesPageModel(ServicioReportes reportes) => Reportes = reportes;

    [BindProperty(SupportsGet = true, Name = "p")] public string? ClavePeriodo { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? Desde { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? Hasta { get; set; }

    public Periodo Periodo { get; private set; } = null!;

    protected async Task CargarPeriodoAsync() => Periodo = await Reportes.PeriodoAsync(ClavePeriodo, Desde, Hasta);
}

// Ranking en barras horizontales (HTML accesible, valor al final de cada barra, una sola serie = un color)
public sealed record BarraH(string Etiqueta, decimal Valor, string Texto, string? Detalle = null, string? Href = null);
public sealed record BarrasHVm(IReadOnlyList<BarraH> Barras, string Descripcion, bool Numerada = false);

// Contenedor de gráfico: título accesible, alto, tipo y configuración (el contenido lo dibuja charts.js)
public sealed record VizVm(string Tipo, object Config, string Descripcion, int Alto = 240, bool Cuadrado = false);
