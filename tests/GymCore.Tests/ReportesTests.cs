using GymCore.Web.Reportes;

namespace GymCore.Tests;

// Motor de métricas de inteligencia de negocio: funciones puras sobre hechos armados en memoria.
public class ReportesTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 15);
    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    // Constructor de escenarios
    private sealed class Escenario
    {
        public List<SocioH> Socios { get; } = new();
        public List<SuscripcionH> Subs { get; } = new();
        public List<PagoH> Pagos { get; } = new();
        public List<AsistenciaH> Asistencias { get; } = new();
        public List<AsignacionH> Asignaciones { get; } = new();
        public List<PlanH> Planes { get; } = new() { new(1, "Mensual", 1000, true), new(2, "Premium", 2000, true), new(3, "Sin ventas", 500, true) };
        public List<EntrenadorH> Entrenadores { get; } = new() { new(1, "Juan Pérez", true), new(2, "Ana Gómez", true) };
        private int _sub;

        public SocioH Socio(int id, DateOnly alta, DateOnly? baja = null, string nombre = "Socio")
        {
            var s = new SocioH(id, nombre, $"N{id}", $"{30000000 + id}", "1155550000", alta, baja);
            Socios.Add(s);
            return s;
        }

        public SuscripcionH Sub(int socio, int plan, DateOnly inicio, int dias = 30)
        {
            var precio = Planes.First(p => p.Id == plan).Precio;
            var s = new SuscripcionH(++_sub, socio, plan, inicio, inicio.AddDays(dias), precio);
            Subs.Add(s);
            return s;
        }

        public void Pago(SuscripcionH s, DateOnly fecha, decimal? importe = null) => Pagos.Add(new(s.SocioId, s.Id, fecha, importe ?? s.Precio));
        public void Asiste(int socio, params DateOnly[] dias) => Asistencias.AddRange(dias.Select(d => new AsistenciaH(socio, d)));

        public HechosNegocio Hechos() => new()
        {
            Hoy = Hoy, Socios = Socios, Suscripciones = Subs, Pagos = Pagos, Asistencias = Asistencias,
            UltimaAsistencia = Asistencias.GroupBy(a => a.SocioId).ToDictionary(g => g.Key, g => g.Max(a => a.Fecha)),
            Asignaciones = Asignaciones, Planes = Planes, Entrenadores = Entrenadores
        };
    }

    // ---------- Períodos ----------
    [Fact]
    public void Presets_de_periodo_y_periodo_anterior()
    {
        var p = Periodo.Crear("6m", Hoy);
        Assert.Equal((D(2026, 5, 1), Hoy), (p.Desde, p.Hasta));
        Assert.Equal(6, p.Meses().Count());
        var ant = p.Anterior();
        Assert.Equal((D(2025, 11, 1), D(2026, 4, 30)), (ant.Desde, ant.Hasta));
        Assert.Equal(D(2025, 11, 1), Periodo.Crear("12m", Hoy).Desde);
        Assert.Equal("6m", Periodo.Crear("cualquiera", Hoy).Clave);   // valor inválido → 6 meses
    }

    [Fact]
    public void Periodo_personalizado_se_acota_y_ordena()
    {
        var p = Periodo.Crear("custom", Hoy, D(2026, 12, 31), D(2026, 8, 10));   // invertido y en el futuro
        Assert.Equal((D(2026, 8, 10), Hoy), (p.Desde, p.Hasta));
        var largo = Periodo.Crear("custom", Hoy, D(2015, 1, 1), Hoy);
        Assert.Equal(Periodo.MesesMaximos, largo.Meses().Count());
    }

    [Fact]
    public void El_mismo_tramo_del_mes_anterior_respeta_meses_cortos()
    {
        Assert.Equal((D(2026, 9, 1), D(2026, 9, 15)), Periodo.MismoTramoMesAnterior(Hoy));
        Assert.Equal((D(2026, 2, 1), D(2026, 2, 28)), Periodo.MismoTramoMesAnterior(D(2026, 3, 31)));
        Assert.Equal((D(2025, 1, 1), D(2025, 10, 15)), Periodo.MismoTramoAnioAnterior(Hoy));
    }

    // ---------- Actividad histórica ----------
    [Fact]
    public void Un_socio_esta_activo_solo_mientras_una_suscripcion_lo_cubre_y_no_tiene_baja()
    {
        var e = new Escenario();
        var s = e.Socio(1, D(2026, 1, 1), baja: D(2026, 9, 20));
        e.Sub(1, 1, D(2026, 8, 1), 60);   // cubre hasta el 30/9, pero se dio de baja el 20/9
        var h = e.Hechos();
        Assert.False(h.EstaActivo(s, D(2026, 7, 31)));
        Assert.True(h.EstaActivo(s, D(2026, 9, 19)));
        Assert.False(h.EstaActivo(s, D(2026, 9, 20)));
    }

    [Fact]
    public void Altas_bajas_y_neto_cierran_por_construccion()
    {
        var e = new Escenario();
        for (var i = 1; i <= 40; i++)
        {
            e.Socio(i, D(2026, 1, 1).AddDays(i * 5), baja: i % 9 == 0 ? D(2026, 9, 1) : null);
            var inicio = D(2026, 1, 1).AddDays(i * 5);
            for (var k = 0; k < (i % 4) + 1; k++) e.Sub(i, 1 + i % 2, inicio.AddDays(k * 31));
        }
        var r = MetricasNegocio.Socios(e.Hechos(), Periodo.Crear("12m", Hoy));
        Assert.All(r.Meses, m => Assert.Equal(m.ActivosCierre - m.ActivosInicio, m.Altas - m.Bajas));
        Assert.Equal(r.Meses[^1].ActivosCierre - r.Meses[0].ActivosInicio, r.Altas - r.Bajas);
        Assert.Equal(e.Hechos().ActivosEn(Hoy).Count, r.ActivosHoy);
    }

    // ---------- KPIs ----------
    [Fact]
    public void Variacion_porcentual_puntos_y_sentido_bueno_o_malo()
    {
        Kpi K(decimal v, decimal? a, FormatoKpi f, bool arriba) => new("k", "K", "", "", v, a, f, arriba, "", Array.Empty<decimal>(), Array.Empty<string>(), "");
        Assert.Equal(25m, K(125, 100, FormatoKpi.Dinero, true).Variacion);
        Assert.True(K(125, 100, FormatoKpi.Dinero, true).EsBueno);
        Assert.Null(K(10, 0, FormatoKpi.Numero, true).Variacion);           // sin base no hay %
        var mor = K(12.5m, 10, FormatoKpi.Porcentaje, false);
        Assert.Equal(2.5m, mor.Variacion);                                   // puntos porcentuales
        Assert.False(mor.EsBueno);                                           // más morosidad es malo
        Assert.Equal("▲ 2,5 pp", Presentacion.Variacion(mor));
        Assert.Equal("delta-bad", Presentacion.ClaseVariacion(mor));
        // Más de +300%: se muestra el multiplicador (una base chica hace ilegible el porcentaje)
        Assert.Equal("▲ 23,9×", Presentacion.Variacion(K(2390, 100, FormatoKpi.Dinero, true)));
        Assert.Equal("▼ 40%", Presentacion.Variacion(K(60, 100, FormatoKpi.Numero, true)));
    }

    [Fact]
    public void Kpis_ejecutivos_con_datos_reales_del_escenario()
    {
        var e = new Escenario();
        e.Socio(1, D(2026, 9, 5)); var s1 = e.Sub(1, 1, D(2026, 9, 5), 60); e.Pago(s1, D(2026, 9, 5));
        e.Socio(2, D(2026, 10, 2)); var s2 = e.Sub(2, 2, D(2026, 10, 2)); e.Pago(s2, D(2026, 10, 3), 500);   // saldo: moroso
        e.Socio(3, D(2026, 10, 10)); var s3 = e.Sub(3, 1, D(2026, 10, 10)); e.Pago(s3, D(2026, 10, 10));
        e.Asiste(1, D(2026, 10, 1), D(2026, 10, 2), D(2026, 9, 3));
        var k = MetricasNegocio.KpisEjecutivos(e.Hechos()).ToDictionary(x => x.Clave);

        Assert.Equal(8, k.Count);
        Assert.Equal(3, k["activos"].Valor);
        Assert.Equal(1, k["activos"].Anterior);                // al 30/9 solo el socio 1
        Assert.Equal(2, k["nuevos"].Valor);                    // altas del 1 al 15/10
        Assert.Equal(1, k["nuevos"].Anterior);                 // altas del 1 al 15/9
        Assert.Equal(1500, k["recaudacion-mes"].Valor);
        Assert.Equal(1000, k["recaudacion-mes"].Anterior);
        Assert.Equal(50, k["recaudacion-mes"].Variacion);
        Assert.Equal(33.3m, k["morosidad"].Valor);             // 1 de 3 activos con saldo
        Assert.Equal(12, k["activos"].Tendencia.Count);
        Assert.Equal(k["activos"].Valor, k["activos"].Tendencia[^1]);
    }

    // ---------- Ingresos ----------
    [Fact]
    public void Ingresos_mejores_meses_y_peor_mes_completo()
    {
        var e = new Escenario();
        e.Socio(1, D(2026, 1, 1));
        foreach (var (mes, monto) in new[] { (5, 3000m), (6, 1000m), (7, 5000m), (8, 2000m), (9, 4000m), (10, 500m) })
        {
            var s = e.Sub(1, 1, D(2026, mes, 1));
            e.Pago(s, D(2026, mes, 3), monto);
        }
        var r = MetricasNegocio.Ingresos(e.Hechos(), Periodo.Crear("6m", Hoy));
        Assert.Equal(15500, r.Total);
        Assert.Equal(new[] { 7, 9, 5 }, r.TopMeses.Select(m => m.Mes.Month));
        Assert.Equal(6, r.PeorMes!.Mes.Month);   // octubre (500) está en curso: no cuenta como "peor mes"
        Assert.Equal(500, r.AnioActual[9]);
        Assert.Null(r.AnioActual[10]);           // noviembre todavía no ocurrió
    }

    // ---------- Planes y entrenadores ----------
    [Fact]
    public void Ranking_de_planes_por_ventas_con_ingreso_y_socios()
    {
        var e = new Escenario();
        for (var i = 1; i <= 3; i++) { e.Socio(i, D(2026, 9, 1)); e.Pago(e.Sub(i, 2, D(2026, 10, 1)), D(2026, 10, 1)); }
        e.Socio(4, D(2026, 9, 1)); e.Sub(4, 1, D(2026, 9, 20));
        var r = MetricasNegocio.Planes(e.Hechos(), Periodo.Crear("3m", Hoy));
        Assert.Equal("Premium", r.MasVendido!.Plan.Nombre);
        Assert.Equal(3, r.MasVendido.Ventas);
        Assert.Equal(6000, r.MasVendido.Ingreso);
        Assert.Equal(75, r.MasVendido.Participacion);
        Assert.Equal("Sin ventas", r.MenosVendido!.Plan.Nombre);
    }

    [Fact]
    public void Cartera_de_entrenadores_excluye_bajas_y_suma_100()
    {
        var e = new Escenario();
        for (var i = 1; i <= 4; i++) { e.Socio(i, D(2026, 1, 1), baja: i == 4 ? D(2026, 9, 1) : null); e.Sub(i, 1, D(2026, 10, 1)); }
        e.Asignaciones.AddRange(new AsignacionH[] { new(1, 1, D(2026, 10, 2), null, true), new(1, 2, D(2026, 3, 1), null, true), new(2, 3, D(2026, 10, 5), null, true), new(2, 4, D(2026, 1, 1), null, true) });
        var r = MetricasNegocio.Entrenadores(e.Hechos(), Periodo.Crear("3m", Hoy));
        Assert.Equal("Juan Pérez", r.MayorCartera!.Entrenador.NombreCompleto);
        Assert.Equal(2, r.MayorCartera.Cartera);
        Assert.Equal(1, r.MenorCartera!.Cartera);   // el socio dado de baja no cuenta
        Assert.Equal(100, r.Filas.Sum(f => f.Participacion), 1);
        Assert.Equal(1.5m, r.Promedio);
    }

    // ---------- Riesgo de abandono ----------
    [Fact]
    public void Clasificacion_de_riesgo_por_factores()
    {
        var e = new Escenario();
        // Alto: vencido hace 10 días y sin venir hace 30
        e.Socio(1, D(2026, 1, 1)); e.Pago(e.Sub(1, 1, D(2026, 8, 6)), D(2026, 8, 6)); e.Asiste(1, D(2026, 9, 15));
        // Bajo: al día, viene seguido y renovado
        e.Socio(2, D(2026, 1, 1)); var s2 = e.Sub(2, 1, D(2026, 10, 1)); e.Pago(s2, D(2026, 10, 1)); e.Asiste(2, D(2026, 10, 14), D(2026, 10, 12));
        // Medio: nunca vino (alta hace 10 días)
        e.Socio(3, D(2026, 10, 5)); e.Pago(e.Sub(3, 1, D(2026, 10, 5)), D(2026, 10, 5));
        // Fuera de alcance: vencido hace más de 60 días
        e.Socio(4, D(2025, 1, 1)); e.Sub(4, 1, D(2026, 6, 1));
        // Frecuencia en baja: 8 ingresos en las 4 semanas previas, 2 en las últimas
        e.Socio(5, D(2026, 1, 1)); e.Pago(e.Sub(5, 1, D(2026, 10, 1)), D(2026, 10, 1));
        e.Asiste(5, Enumerable.Range(0, 8).Select(i => D(2026, 8, 25).AddDays(i * 3)).Concat(new[] { D(2026, 10, 1), D(2026, 10, 13) }).ToArray());

        var r = MetricasNegocio.Riesgo(e.Hechos()).Socios.ToDictionary(x => x.Socio.Id);
        Assert.Equal(NivelRiesgo.Alto, r[1].Nivel);
        Assert.Equal(30, r[1].DiasSinAsistir);
        Assert.Contains(r[1].Factores, f => f.Clave == "vencida");
        Assert.StartsWith("Contactar para renovar", r[1].Recomendacion);
        Assert.Equal(NivelRiesgo.Bajo, r[2].Nivel);
        Assert.Equal(NivelRiesgo.Medio, r[3].Nivel);
        Assert.Contains(r[3].Factores, f => f.Clave == "sin-asistencia");
        Assert.False(r.ContainsKey(4));
        Assert.Contains(r[5].Factores, f => f.Clave == "frecuencia" && f.Puntos == 20);
    }

    // ---------- Alertas e insights ----------
    [Fact]
    public void Alertas_gerenciales_por_umbral_e_insights_con_numeros_reales()
    {
        var e = new Escenario();
        e.Socio(1, D(2026, 1, 1)); e.Pago(e.Sub(1, 2, D(2026, 9, 2)), D(2026, 9, 2), 10000);
        e.Socio(2, D(2026, 1, 1)); e.Pago(e.Sub(2, 2, D(2026, 10, 2)), D(2026, 10, 2), 2000);
        e.Asignaciones.Add(new(1, 2, D(2026, 10, 2), null, true));
        var h = e.Hechos();
        var kpis = MetricasNegocio.KpisEjecutivos(h);
        var planes = MetricasNegocio.Planes(h, Periodo.Crear("3m", Hoy));
        var ent = MetricasNegocio.Entrenadores(h, Periodo.Crear("3m", Hoy));
        var riesgo = MetricasNegocio.Riesgo(h);

        var alertas = MetricasNegocio.Alertas(h, kpis, planes, ent, riesgo).Select(a => a.Titulo).ToList();
        Assert.Contains("Caída de ingresos", alertas);              // 2.000 vs 10.000 = −80%
        Assert.Contains("Entrenadores sin socios", alertas);        // Ana Gómez
        Assert.Contains("Planes sin contrataciones", alertas);      // Mensual y "Sin ventas"

        var insights = MetricasNegocio.Insights(h, kpis, planes, ent, riesgo).Select(i => i.Texto).ToList();
        Assert.Contains(insights, t => t.Contains("cayó un 80%"));
        Assert.Contains(insights, t => t.Contains("El plan Premium lidera las ventas"));
        Assert.Contains(insights, t => t.StartsWith("Juan Pérez tiene la mayor cartera"));
    }
}
