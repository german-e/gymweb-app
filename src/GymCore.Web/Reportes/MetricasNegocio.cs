namespace GymCore.Web.Reportes;

// Todas las métricas de inteligencia de negocio. Funciones puras sobre HechosNegocio: una sola definición
// por indicador (documentada en Kpi.Definicion), sin acceso a datos y testeables con hechos en memoria.
public static class MetricasNegocio
{
    public const int DiasRiesgoPostVencimiento = 60;   // socios vencidos que todavía se pueden recuperar

    // ---------- Utilidades de calendario ----------
    public static IReadOnlyList<DateOnly> UltimosMeses(DateOnly hoy, int n) =>
        Enumerable.Range(0, n).Select(i => Periodo.InicioMes(hoy).AddMonths(i - (n - 1))).ToList();

    private static DateOnly Cierre(DateOnly mes, DateOnly hoy) => Periodo.FinMes(mes) > hoy ? hoy : Periodo.FinMes(mes);

    private static string MesAnteriorTexto(DateOnly hoy) => Periodo.NombreMesLargo(Periodo.InicioMes(hoy).AddMonths(-1)).ToLower(Periodo.Es);

    // Socios que dejaron de estar activos entre el día anterior a "desde" y "hasta" (baja o sin renovar)
    public static int Perdidos(HechosNegocio h, DateOnly desde, DateOnly hasta) =>
        h.ActivosEn(desde.AddDays(-1)).Except(h.ActivosEn(hasta)).Count();

    public static int Ganados(HechosNegocio h, DateOnly desde, DateOnly hasta) =>
        h.ActivosEn(hasta).Except(h.ActivosEn(desde.AddDays(-1))).Count();

    public static int Registros(HechosNegocio h, DateOnly desde, DateOnly hasta) =>
        h.Socios.Count(s => s.Alta >= desde && s.Alta <= hasta);

    public static decimal Morosidad(HechosNegocio h, DateOnly d)
    {
        var activos = h.ActivosEn(d);
        if (activos.Count == 0) return 0;
        var morosos = h.Socios.Count(s => activos.Contains(s.Id) && h.MorosoEn(s, d));
        return Math.Round(morosos * 100m / activos.Count, 1);
    }

    public static decimal AsistenciaDiaria(HechosNegocio h, DateOnly desde, DateOnly hasta)
    {
        var dias = hasta.DayNumber - desde.DayNumber + 1;
        return dias <= 0 ? 0 : Math.Round((decimal)h.Ingresos(desde, hasta) / dias, 1);
    }

    // ---------- Resumen ejecutivo: 8 KPIs con comparación y tendencia de 12 meses ----------
    public static IReadOnlyList<Kpi> KpisEjecutivos(HechosNegocio h)
    {
        var hoy = h.Hoy;
        var (mI, mF) = Periodo.MesActualHastaHoy(hoy);
        var (aI, aF) = Periodo.MismoTramoMesAnterior(hoy);
        var (yI, yF) = Periodo.AnioHastaHoy(hoy);
        var (yaI, yaF) = Periodo.MismoTramoAnioAnterior(hoy);
        var cierreAnterior = mI.AddDays(-1);
        var meses = UltimosMeses(hoy, 12);
        var etiquetas = meses.Select(m => Periodo.NombreMes(m, true)).ToList();
        var tramo = $"vs. mismo período de {MesAnteriorTexto(hoy)}";
        var cierre = $"vs. cierre de {MesAnteriorTexto(hoy)}";
        List<decimal> Serie(Func<DateOnly, DateOnly, decimal> f) => meses.Select(m => f(m, Cierre(m, hoy))).ToList();

        return new List<Kpi>
        {
            new("activos", "Socios activos", "bi-people-fill", "primary",
                h.ActivosEn(hoy).Count, h.ActivosEn(cierreAnterior).Count, FormatoKpi.Numero, true, cierre,
                Serie((_, f) => h.ActivosEn(f).Count), etiquetas,
                "Socios sin baja con una suscripción que cubre el día. Tendencia: activos al cierre de cada mes.", "/Reportes/Socios"),
            new("nuevos", "Socios nuevos del mes", "bi-person-plus", "success",
                Registros(h, mI, mF), Registros(h, aI, aF), FormatoKpi.Numero, true, tramo,
                Serie((i, f) => Registros(h, i, f)), etiquetas,
                "Socios registrados (fecha de alta) en el mes hasta hoy, contra los mismos días del mes anterior.", "/Reportes/Socios"),
            new("bajas", "Bajas del mes", "bi-person-dash", "danger",
                Perdidos(h, mI, mF), Perdidos(h, aI, aF), FormatoKpi.Numero, false, tramo,
                Serie((i, f) => Perdidos(h, i, f)), etiquetas,
                "Socios que estaban activos al empezar el mes y hoy no lo están: dados de baja o con la membresía vencida sin renovar.", "/Reportes/Riesgo"),
            new("recaudacion-mes", "Recaudación del mes", "bi-cash-stack", "success",
                h.Recaudado(mI, mF), h.Recaudado(aI, aF), FormatoKpi.Dinero, true, tramo,
                Serie((i, f) => h.Recaudado(i, f)), etiquetas,
                "Pagos cobrados en el mes hasta hoy (sin anulados), contra los mismos días del mes anterior.", "/Reportes/Ingresos"),
            new("recaudacion-anio", "Recaudación anual", "bi-graph-up-arrow", "primary",
                h.Recaudado(yI, yF), h.Recaudado(yaI, yaF), FormatoKpi.Dinero, true, $"vs. mismo período de {hoy.Year - 1}",
                Serie((i, f) => h.Recaudado(i, f)), etiquetas,
                $"Pagos cobrados desde el 1/1/{hoy.Year} hasta hoy, contra el mismo tramo del año anterior. Tendencia: recaudación mensual.", "/Reportes/Ingresos"),
            new("morosidad", "Morosidad", "bi-exclamation-diamond", "warning",
                Morosidad(h, hoy), Morosidad(h, cierreAnterior), FormatoKpi.Porcentaje, false, cierre,
                Serie((_, f) => Morosidad(h, f)), etiquetas,
                "Porcentaje de socios activos con saldo pendiente en su suscripción vigente. La variación se expresa en puntos porcentuales.", "/Pagos/Pendientes"),
            new("renovaciones", "Renovaciones", "bi-arrow-repeat", "info",
                h.Renovaciones(mI, mF), h.Renovaciones(aI, aF), FormatoKpi.Numero, true, tramo,
                Serie((i, f) => h.Renovaciones(i, f)), etiquetas,
                "Suscripciones iniciadas en el mes por socios que ya habían tenido otra.", "/Reportes/Planes"),
            new("asistencia", "Asistencia promedio", "bi-door-open", "primary",
                AsistenciaDiaria(h, mI, mF), AsistenciaDiaria(h, aI, aF), FormatoKpi.Decimal, true, tramo,
                Serie((i, f) => AsistenciaDiaria(h, i, f)), etiquetas,
                "Ingresos registrados por día en el mes hasta hoy (promedio diario).", "/Reportes/Riesgo"),
        };
    }

    // ---------- Socios ----------
    public static ReporteSocios Socios(HechosNegocio h, Periodo p)
    {
        var meses = p.Meses().Select(m =>
        {
            var (ini, fin) = p.Tramo(m);
            var a = h.ActivosEn(ini.AddDays(-1));
            var b = h.ActivosEn(fin);
            return new MesSocios(m, b.Except(a).Count(), a.Except(b).Count(), a.Count, b.Count, Registros(h, ini, fin));
        }).ToList();

        var activosHoy = h.ActivosEn(h.Hoy);
        var conEntrenador = h.Asignaciones.Where(x => x.Activa).Select(x => x.SocioId).ToHashSet();
        // Retención: de los activos al inicio del período, cuántos siguen activos al final
        var inicio = h.ActivosEn(p.Desde.AddDays(-1));
        var retencion = inicio.Count == 0 ? 0 : Math.Round(inicio.Intersect(h.ActivosEn(p.Hasta)).Count() * 100m / inicio.Count, 1);
        return new ReporteSocios(p, meses, activosHoy.Count, activosHoy.Count(id => !conEntrenador.Contains(id)), retencion);
    }

    // ---------- Ingresos ----------
    public static ReporteIngresos Ingresos(HechosNegocio h, Periodo p)
    {
        var hoy = h.Hoy;
        List<MesIngresos> Mensual(Periodo per) => per.Meses().Select(m =>
        {
            var (ini, fin) = per.Tramo(m);
            return new MesIngresos(m, h.Recaudado(ini, fin), h.Pagos.Count(x => x.Fecha >= ini && x.Fecha <= fin),
                h.ActivosEn(fin).Count, Completo: ini == m && fin == Periodo.FinMes(m));
        }).ToList();

        var meses = Mensual(p);
        static decimal? Arpu(List<MesIngresos> ms)
        {
            var valores = ms.Where(m => m.PorSocio is not null).Select(m => m.PorSocio!.Value).ToList();
            return valores.Count == 0 ? null : Math.Round(valores.Average(), 0);
        }
        var arpu = Arpu(meses);
        var arpuAnterior = Arpu(Mensual(p.Anterior()));

        var kpis = KpisEjecutivos(h);
        var porSocio = new Kpi("por-socio", "Ingreso promedio por socio", "bi-person-check", "info",
            arpu ?? 0, arpuAnterior, FormatoKpi.Dinero, true, "vs. período anterior equivalente",
            meses.Select(m => m.PorSocio ?? 0).ToList(), meses.Select(m => Periodo.NombreMes(m.Mes, true)).ToList(),
            "Promedio mensual de recaudación dividida por los socios activos al cierre de cada mes del período.");

        var diario = Enumerable.Range(0, 30).Select(i => p.Hasta.AddDays(i - 29))
            .Select(d => (d, h.Recaudado(d, d))).ToList();
        var diaria = new Kpi("diaria", "Recaudación de hoy", "bi-calendar-check", "primary",
            h.Recaudado(hoy, hoy), h.Recaudado(hoy.AddDays(-1), hoy.AddDays(-1)), FormatoKpi.Dinero, true, "vs. ayer",
            Enumerable.Range(0, 30).Select(i => h.Recaudado(hoy.AddDays(i - 29), hoy.AddDays(i - 29))).ToList(),
            Enumerable.Range(0, 30).Select(i => hoy.AddDays(i - 29).ToString("dd/MM")).ToList(),
            "Pagos cobrados hoy (sin anulados). Tendencia: últimos 30 días.");

        var anioActual = Enumerable.Range(1, 12).Select(mes =>
        {
            var m = new DateOnly(hoy.Year, mes, 1);
            return m > hoy ? (decimal?)null : h.Recaudado(m, Cierre(m, hoy));
        }).ToList();
        var anioAnterior = Enumerable.Range(1, 12).Select(mes =>
        {
            var m = new DateOnly(hoy.Year - 1, mes, 1);
            return m < Periodo.InicioMes(hoy).AddMonths(-(FuenteHechosDb.MesesPagos - 1)) ? (decimal?)null : h.Recaudado(m, Periodo.FinMes(m));
        }).ToList();

        var top = meses.Where(m => m.Total > 0).OrderByDescending(m => m.Total).Take(3).ToList();
        var peor = meses.Where(m => m.Completo).OrderBy(m => m.Total).FirstOrDefault();
        return new ReporteIngresos(p, diaria,
            kpis.Single(k => k.Clave == "recaudacion-mes"), kpis.Single(k => k.Clave == "recaudacion-anio"), porSocio,
            meses, diario, anioActual, anioAnterior, top, peor);
    }

    // ---------- Planes ----------
    public static ReportePlanes Planes(HechosNegocio h, Periodo p)
    {
        var activosHoy = h.ActivosEn(h.Hoy);
        var planDeSub = h.Suscripciones.ToDictionary(s => s.Id, s => s.PlanId);
        var activosPorPlan = activosHoy.Select(id => h.VigenteEn(id, h.Hoy)!.PlanId).GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        var ingresoPorPlan = h.Pagos.Where(x => p.Contiene(x.Fecha) && x.SuscripcionId is { } sid && planDeSub.ContainsKey(sid))
            .GroupBy(x => planDeSub[x.SuscripcionId!.Value]).ToDictionary(g => g.Key, g => g.Sum(x => x.Importe));

        var filas = h.Planes.Select(plan =>
        {
            var ventas = h.Suscripciones.Where(s => s.PlanId == plan.Id && p.Contiene(s.Inicio)).ToList();
            var renov = ventas.Count(s => h.SubsPorSocio[s.SocioId].Any(o => o.Id != s.Id && o.Inicio < s.Inicio));
            var socios = activosPorPlan.GetValueOrDefault(plan.Id);
            return new FilaPlan(plan, ventas.Count, renov, socios, ingresoPorPlan.GetValueOrDefault(plan.Id),
                activosHoy.Count == 0 ? 0 : Math.Round(socios * 100m / activosHoy.Count, 1));
        })
        .OrderByDescending(f => f.Ventas).ThenByDescending(f => f.Ingreso).ThenBy(f => f.Plan.Nombre)
        .ToList();
        return new ReportePlanes(p, filas, activosHoy.Count);
    }

    // ---------- Entrenadores ----------
    public static ReporteEntrenadores Entrenadores(HechosNegocio h, Periodo p)
    {
        var bajas = h.Socios.Where(s => s.Baja is { } b && b <= h.Hoy).Select(s => s.Id).ToHashSet();
        var vigentes = h.Asignaciones.Where(a => a.Activa && !bajas.Contains(a.SocioId)).ToList();
        var total = vigentes.Count;
        var filas = h.Entrenadores.Select(e =>
        {
            var cartera = vigentes.Count(a => a.EntrenadorId == e.Id);
            var nuevos = h.Asignaciones.Count(a => a.EntrenadorId == e.Id && p.Contiene(a.Desde));
            return new FilaEntrenador(e, cartera, nuevos, total == 0 ? 0 : Math.Round(cartera * 100m / total, 1));
        })
        .OrderByDescending(f => f.Cartera).ThenByDescending(f => f.Nuevos).ThenBy(f => f.Entrenador.NombreCompleto)
        .ToList();
        var asignados = vigentes.Select(a => a.SocioId).ToHashSet();
        return new ReporteEntrenadores(p, filas, h.ActivosEn(h.Hoy).Count(id => !asignados.Contains(id)));
    }

    // ---------- Riesgo de abandono ----------
    // Puntaje 0-100 por factores observables. Alto >= 50 · Medio >= 25 · Bajo < 25.
    public static ReporteRiesgo Riesgo(HechosNegocio h)
    {
        var hoy = h.Hoy;
        var lista = new List<SocioRiesgo>();
        foreach (var s in h.Socios.Where(s => s.Baja is null || s.Baja > hoy))
        {
            var subs = h.SubsPorSocio[s.Id].ToList();
            if (subs.Count == 0) continue;
            var vigente = h.VigenteEn(s.Id, hoy);
            var ultimaSub = subs.MaxBy(x => x.Fin)!;
            if (vigente is null && hoy.DayNumber - ultimaSub.Fin.DayNumber > DiasRiesgoPostVencimiento) continue;   // ya se fue: fuera del alcance de retención

            var factores = new List<FactorRiesgo>();
            int? dias = h.UltimaAsistencia.TryGetValue(s.Id, out var u) ? hoy.DayNumber - u.DayNumber : null;
            var desdeAlta = hoy.DayNumber - (subs.Min(x => x.Inicio) > s.Alta ? subs.Min(x => x.Inicio) : s.Alta).DayNumber;
            if (dias is null && desdeAlta >= 7) factores.Add(new("sin-asistencia", "Nunca registró un ingreso", 35));
            else if (dias >= 21) factores.Add(new("inasistencia", $"No asiste hace {dias} días", 40));
            else if (dias >= 14) factores.Add(new("inasistencia", $"No asiste hace {dias} días", 25));
            else if (dias >= 8) factores.Add(new("inasistencia", $"No asiste hace {dias} días", 10));

            if (vigente is null)
                factores.Add(new("vencida", $"Membresía vencida hace {hoy.DayNumber - ultimaSub.Fin.DayNumber} días sin renovar", 30));
            else
            {
                var saldo = vigente.Precio - h.PagadoHasta(vigente.Id, hoy);
                if (saldo > 0) factores.Add(new("saldo", $"Saldo pendiente de {Dinero(saldo)}", 20));
                var renovo = subs.Any(x => x.Inicio > vigente.Fin);
                if (!renovo && vigente.Fin.DayNumber - hoy.DayNumber <= 7)
                    factores.Add(new("vence", $"Vence el {vigente.Fin:dd/MM} sin renovación", 10));
            }

            // Disminución de frecuencia: últimas 4 semanas contra las 4 anteriores
            var recientes = h.AsistenciasPorSocio[s.Id].Count(a => a.Fecha > hoy.AddDays(-28));
            var previas = h.AsistenciasPorSocio[s.Id].Count(a => a.Fecha <= hoy.AddDays(-28) && a.Fecha > hoy.AddDays(-56));
            if (previas >= 4 && recientes <= previas * 0.5)
                factores.Add(new("frecuencia", $"Frecuencia en baja: {previas} → {recientes} ingresos (4 semanas)", 20));
            else if (previas >= 4 && recientes <= previas * 0.75)
                factores.Add(new("frecuencia", $"Frecuencia en baja: {previas} → {recientes} ingresos (4 semanas)", 10));

            var puntaje = Math.Min(100, factores.Sum(f => f.Puntos));
            var nivel = puntaje >= 50 ? NivelRiesgo.Alto : puntaje >= 25 ? NivelRiesgo.Medio : NivelRiesgo.Bajo;
            var plan = h.Planes.FirstOrDefault(x => x.Id == (vigente ?? ultimaSub).PlanId)?.Nombre;
            lista.Add(new SocioRiesgo(s, puntaje, nivel, dias, vigente is not null, (vigente ?? ultimaSub).Fin, plan,
                factores.OrderByDescending(f => f.Puntos).ToList(), Recomendar(factores, plan, vigente, h, dias)));
        }
        return new ReporteRiesgo(lista.OrderByDescending(x => x.Puntaje).ThenByDescending(x => x.DiasSinAsistir ?? int.MaxValue).ToList());
    }

    // La recomendación sigue la acción más útil, no el mayor puntaje: con la membresía vencida lo primero es renovar
    private static readonly string[] PrioridadAccion = { "vencida", "sin-asistencia", "inasistencia", "saldo", "frecuencia", "vence" };

    private static string Recomendar(List<FactorRiesgo> f, string? plan, SuscripcionH? vigente, HechosNegocio h, int? dias)
    {
        var principal = PrioridadAccion.FirstOrDefault(c => f.Any(x => x.Clave == c));
        return principal switch
        {
            "vencida" => $"Contactar para renovar{(plan is null ? "" : $" el plan {plan}")}: todavía es recuperable.",
            "sin-asistencia" => "Llamar para darle la bienvenida y agendar su primer entrenamiento.",
            "inasistencia" => $"Contactar: no viene hace {dias} días. Consultá si necesita otro horario o rutina.",
            "saldo" => "Gestionar el cobro del saldo pendiente.",
            "frecuencia" => "Pedirle a su entrenador una revisión de rutina y objetivos.",
            "vence" => $"Ofrecer la renovación antes del {vigente!.Fin:dd/MM}.",
            _ => "Sin acciones: socio comprometido."
        };
    }

    // ---------- Alertas gerenciales ----------
    public static IReadOnlyList<AlertaGerencial> Alertas(HechosNegocio h, IReadOnlyList<Kpi> kpis, ReportePlanes planes90, ReporteEntrenadores entrenadores, ReporteRiesgo riesgo)
    {
        var k = kpis.ToDictionary(x => x.Clave);
        var alertas = new List<AlertaGerencial>();

        if (k["recaudacion-mes"] is { Variacion: <= -15 } rec)
            alertas.Add(new("danger", "bi-graph-down-arrow", "Caída de ingresos",
                $"La recaudación del mes va {Math.Abs(rec.Variacion!.Value):0.#}% por debajo del mismo período del mes anterior.", "/Reportes/Ingresos", "Ver ingresos"));
        if (k["morosidad"] is { Variacion: >= 3 } mor && mor.Valor >= 10)
            alertas.Add(new("warning", "bi-exclamation-diamond", "Aumento de morosidad",
                $"La morosidad subió {mor.Variacion!.Value:0.#} puntos: hoy {mor.Valor:0.#}% de los socios activos tiene saldo.", "/Pagos/Pendientes", "Ver deudas"));
        if (k["asistencia"] is { Variacion: <= -20 } asi)
            alertas.Add(new("warning", "bi-door-closed", "Disminución de asistencia",
                $"El promedio diario de ingresos cayó {Math.Abs(asi.Variacion!.Value):0.#}% respecto del mes anterior.", "/Reportes/Riesgo", "Ver riesgo"));

        var hoy = h.Hoy;
        var netoActual = Ganados(h, Periodo.InicioMes(hoy), hoy) - Perdidos(h, Periodo.InicioMes(hoy), hoy);
        var mesAnt = Periodo.InicioMes(hoy).AddMonths(-1);
        var netoAnterior = Ganados(h, mesAnt, Periodo.FinMes(mesAnt)) - Perdidos(h, mesAnt, Periodo.FinMes(mesAnt));
        if (netoActual < 0 && netoAnterior < 0)
            alertas.Add(new("danger", "bi-people", "Dos meses perdiendo socios",
                $"El crecimiento neto fue negativo el mes pasado ({netoAnterior}) y lo es en el actual ({netoActual}).", "/Reportes/Socios", "Ver evolución"));

        if (riesgo.Alto > 0)
            alertas.Add(new("warning", "bi-exclamation-octagon", "Socios con riesgo alto de abandono",
                $"{riesgo.Alto} {(riesgo.Alto == 1 ? "socio tiene" : "socios tienen")} riesgo alto. Contactarlos esta semana mejora la retención.", "/Reportes/Riesgo", "Ver listado"));

        var sinSocios = entrenadores.Activos.Where(f => f.Cartera == 0).Select(f => f.Entrenador.NombreCompleto).ToList();
        if (sinSocios.Count > 0)
            alertas.Add(new("info", "bi-person-badge", "Entrenadores sin socios",
                $"{string.Join(", ", sinSocios.Take(3))}{(sinSocios.Count > 3 ? $" y {sinSocios.Count - 3} más" : "")} {(sinSocios.Count == 1 ? "no tiene" : "no tienen")} socios asignados.", "/Reportes/Entrenadores", "Ver entrenadores"));

        var sinVentas = planes90.Filas.Where(f => f.Plan.Activo && f.Ventas == 0).Select(f => f.Plan.Nombre).ToList();
        if (sinVentas.Count > 0)
            alertas.Add(new("info", "bi-box-seam", "Planes sin contrataciones",
                $"{string.Join(", ", sinVentas.Take(3))}{(sinVentas.Count > 3 ? $" y {sinVentas.Count - 3} más" : "")}: sin altas en los últimos 3 meses.", "/Reportes/Planes", "Ver planes"));
        return alertas;
    }

    // ---------- Insights: "Lo que está ocurriendo en tu gimnasio" ----------
    public static IReadOnlyList<Insight> Insights(HechosNegocio h, IReadOnlyList<Kpi> kpis, ReportePlanes planes90, ReporteEntrenadores entrenadores, ReporteRiesgo riesgo)
    {
        var k = kpis.ToDictionary(x => x.Clave);
        var mesAnt = MesAnteriorTexto(h.Hoy);
        var lista = new List<Insight>();

        var rec = k["recaudacion-mes"];
        if (rec.Variacion is { } vr)
            lista.Add(vr >= 0
                ? new("success", "bi-graph-up-arrow", $"La recaudación aumentó un {vr:0.#}% respecto del mismo período de {mesAnt} ({Dinero(rec.Valor)}).", "/Reportes/Ingresos")
                : new("danger", "bi-graph-down-arrow", $"La recaudación cayó un {Math.Abs(vr):0.#}% respecto del mismo período de {mesAnt} ({Dinero(rec.Valor)}).", "/Reportes/Ingresos"));
        else if (rec.Valor > 0)
            lista.Add(new("info", "bi-cash-stack", $"Este mes se recaudaron {Dinero(rec.Valor)}.", "/Reportes/Ingresos"));

        var activos = k["activos"];
        var neto = (int)(activos.Valor - (activos.Anterior ?? activos.Valor));
        if (neto != 0)
            lista.Add(neto > 0
                ? new("success", "bi-people-fill", $"El gimnasio creció {neto} {(neto == 1 ? "socio activo" : "socios activos")} en lo que va del mes (hoy son {activos.Valor:0}).", "/Reportes/Socios")
                : new("danger", "bi-people", $"En lo que va del mes hay {Math.Abs(neto)} {(neto == -1 ? "socio activo" : "socios activos")} menos (hoy son {activos.Valor:0}).", "/Reportes/Socios"));

        if (planes90.MasVendido is { } top)
            lista.Add(new("success", "bi-trophy", $"El plan {top.Plan.Nombre} lidera las ventas de los últimos 3 meses con {top.Ventas} {(top.Ventas == 1 ? "contratación" : "contrataciones")}.", "/Reportes/Planes"));

        if (riesgo.Alto + riesgo.Medio > 0)
            lista.Add(new(riesgo.Alto > 0 ? "warning" : "info", "bi-exclamation-octagon",
                $"Hay {riesgo.Alto + riesgo.Medio} {(riesgo.Alto + riesgo.Medio == 1 ? "socio" : "socios")} con riesgo de abandono ({riesgo.Alto} alto, {riesgo.Medio} medio).", "/Reportes/Riesgo"));
        else if (activos.Valor > 0)
            lista.Add(new("success", "bi-shield-check", "Ningún socio muestra señales de abandono en este momento.", "/Reportes/Riesgo"));

        if (entrenadores.MayorCartera is { Cartera: > 0 } ent)
            lista.Add(new("info", "bi-person-badge", $"{ent.Entrenador.NombreCompleto} tiene la mayor cartera: {ent.Cartera} {(ent.Cartera == 1 ? "socio asignado" : "socios asignados")} ({ent.Participacion:0.#}% del total).", "/Reportes/Entrenadores"));

        var mor = k["morosidad"];
        if (mor.Valor > 0)
            lista.Add(new(mor.Valor >= 15 ? "warning" : "info", "bi-exclamation-diamond", $"La morosidad está en {mor.Valor:0.#}% de los socios activos.", "/Pagos/Pendientes"));

        var asi = k["asistencia"];
        if (asi.Variacion is { } va && asi.Anterior > 0)
            lista.Add(new(va >= 0 ? "success" : "warning", "bi-door-open",
                $"La asistencia promedio es de {asi.Valor:0.#} ingresos por día ({(va >= 0 ? "+" : "−")}{Math.Abs(va):0.#}% vs. {mesAnt}).", "/Reportes/Riesgo"));
        return lista;
    }

    public static string Dinero(decimal v) => "$ " + v.ToString("N0", Periodo.Es);
}
