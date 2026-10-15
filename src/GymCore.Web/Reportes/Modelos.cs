namespace GymCore.Web.Reportes;

// Resultados de las métricas (inmutables). Las vistas solo los presentan; no calculan.

public enum FormatoKpi { Numero, Dinero, Porcentaje, Decimal }

/// <param name="Anterior">valor del período de comparación (null = no hay base)</param>
/// <param name="ArribaEsBueno">define el color de la variación (morosidad: subir es malo)</param>
/// <param name="Tendencia">últimos 12 meses (el último es el mes en curso)</param>
public sealed record Kpi(
    string Clave, string Titulo, string Icono, string Variante,
    decimal Valor, decimal? Anterior, FormatoKpi Formato, bool ArribaEsBueno,
    string Comparacion, IReadOnlyList<decimal> Tendencia, IReadOnlyList<string> TendenciaEtiquetas,
    string Definicion, string? Href = null)
{
    // Porcentajes (morosidad) se comparan en puntos porcentuales; el resto en variación %
    public bool EnPuntos => Formato == FormatoKpi.Porcentaje;

    public decimal? Variacion => Anterior is not { } a ? null
        : EnPuntos ? Valor - a
        : a == 0 ? null
        : Math.Round((Valor - a) * 100 / a, 1);

    public bool? EsBueno => Variacion is not { } v || v == 0 ? null : (v > 0) == ArribaEsBueno;
}

public sealed record MesSocios(DateOnly Mes, int Altas, int Bajas, int ActivosInicio, int ActivosCierre, int Registros)
{
    public int Neto => ActivosCierre - ActivosInicio;   // = Altas − Bajas por construcción
}

public sealed record ReporteSocios(Periodo Periodo, IReadOnlyList<MesSocios> Meses, int ActivosHoy, int SinEntrenador, decimal Retencion)
{
    public int Altas => Meses.Sum(m => m.Altas);
    public int Bajas => Meses.Sum(m => m.Bajas);
    public int Neto => Meses.Count == 0 ? 0 : Meses[^1].ActivosCierre - Meses[0].ActivosInicio;
    public int Registros => Meses.Sum(m => m.Registros);
}

public sealed record MesIngresos(DateOnly Mes, decimal Total, int Pagos, int Activos, bool Completo)
{
    public decimal? PorSocio => Activos > 0 ? Math.Round(Total / Activos, 0) : null;
}

public sealed record ReporteIngresos(
    Periodo Periodo, Kpi Diaria, Kpi Mes, Kpi Anio, Kpi PorSocio,
    IReadOnlyList<MesIngresos> Meses, IReadOnlyList<(DateOnly Dia, decimal Total)> Diario,
    IReadOnlyList<decimal?> AnioActual, IReadOnlyList<decimal?> AnioAnterior,
    IReadOnlyList<MesIngresos> TopMeses, MesIngresos? PeorMes)
{
    public decimal Total => Meses.Sum(m => m.Total);
}

public sealed record FilaPlan(PlanH Plan, int Ventas, int Renovaciones, int SociosActivos, decimal Ingreso, decimal Participacion);

public sealed record ReportePlanes(Periodo Periodo, IReadOnlyList<FilaPlan> Filas, int ActivosConPlan)
{
    public FilaPlan? MasVendido => Filas.Where(f => f.Ventas > 0).OrderByDescending(f => f.Ventas).ThenByDescending(f => f.Ingreso).FirstOrDefault();
    public FilaPlan? MenosVendido => Filas.Where(f => f.Plan.Activo).OrderBy(f => f.Ventas).ThenBy(f => f.Ingreso).FirstOrDefault();
    public decimal Ingreso => Filas.Sum(f => f.Ingreso);
    public int Ventas => Filas.Sum(f => f.Ventas);
}

public sealed record FilaEntrenador(EntrenadorH Entrenador, int Cartera, int Nuevos, decimal Participacion);

public sealed record ReporteEntrenadores(Periodo Periodo, IReadOnlyList<FilaEntrenador> Filas, int SociosActivosSinEntrenador)
{
    public IReadOnlyList<FilaEntrenador> Activos => Filas.Where(f => f.Entrenador.Activo).ToList();
    public FilaEntrenador? MayorCartera => Activos.OrderByDescending(f => f.Cartera).FirstOrDefault();
    public FilaEntrenador? MenorCartera => Activos.OrderBy(f => f.Cartera).FirstOrDefault();
    public decimal Promedio => Activos.Count == 0 ? 0 : Math.Round((decimal)Activos.Sum(f => f.Cartera) / Activos.Count, 1);
    public int TotalCartera => Filas.Sum(f => f.Cartera);
    public int Nuevos => Filas.Sum(f => f.Nuevos);
}

public enum NivelRiesgo { Bajo, Medio, Alto }

public sealed record FactorRiesgo(string Clave, string Texto, int Puntos);

public sealed record SocioRiesgo(
    SocioH Socio, int Puntaje, NivelRiesgo Nivel, int? DiasSinAsistir, bool Vigente, DateOnly? Vence,
    string? Plan, IReadOnlyList<FactorRiesgo> Factores, string Recomendacion);

public sealed record ReporteRiesgo(IReadOnlyList<SocioRiesgo> Socios)
{
    public int Alto => Socios.Count(s => s.Nivel == NivelRiesgo.Alto);
    public int Medio => Socios.Count(s => s.Nivel == NivelRiesgo.Medio);
    public int Bajo => Socios.Count(s => s.Nivel == NivelRiesgo.Bajo);
}

public sealed record AlertaGerencial(string Variante, string Icono, string Titulo, string Detalle, string? Href = null, string? Accion = null);

/// <param name="Tono">success | warning | danger | info (siempre con ícono y texto)</param>
public sealed record Insight(string Tono, string Icono, string Texto, string? Href = null);

public sealed record ResumenEjecutivo(
    IReadOnlyList<Kpi> Kpis, IReadOnlyList<Insight> Insights, IReadOnlyList<AlertaGerencial> Alertas,
    ReporteRiesgo Riesgo, DateOnly Hoy);
