using System.Globalization;

namespace GymCore.Web.Reportes;

// Rango de análisis en fechas locales del gimnasio (ambos extremos incluidos).
public sealed record Periodo(DateOnly Desde, DateOnly Hasta, string Clave)
{
    public static readonly CultureInfo Es = new("es-AR");
    public const int MesesMaximos = 36;

    public static readonly (string Clave, string Texto)[] Presets =
    {
        ("3m", "Últimos 3 meses"), ("6m", "Últimos 6 meses"), ("12m", "Últimos 12 meses")
    };

    // "3m" | "6m" | "12m" (meses calendario completos, el actual hasta hoy) | "custom" con desde/hasta
    public static Periodo Crear(string? clave, DateOnly hoy, DateOnly? desde = null, DateOnly? hasta = null)
    {
        if (clave == "custom" && desde is { } d && hasta is { } h)
        {
            if (d > h) (d, h) = (h, d);
            if (h > hoy) h = hoy;
            if (d > h) d = h;
            var minimo = InicioMes(h).AddMonths(-(MesesMaximos - 1));
            return new Periodo(d < minimo ? minimo : d, h, "custom");
        }
        var meses = clave switch { "3m" => 3, "12m" => 12, _ => 6 };
        return new Periodo(InicioMes(hoy).AddMonths(-(meses - 1)), hoy, $"{meses}m");
    }

    public static DateOnly InicioMes(DateOnly d) => new(d.Year, d.Month, 1);
    public static DateOnly FinMes(DateOnly d) => InicioMes(d).AddMonths(1).AddDays(-1);

    public int Dias => Hasta.DayNumber - Desde.DayNumber + 1;

    // Período de igual cantidad de meses inmediatamente anterior (para comparar)
    public Periodo Anterior()
    {
        var meses = Meses().Count();
        var desde = InicioMes(Desde).AddMonths(-meses);
        return new Periodo(desde, InicioMes(Desde).AddDays(-1), Clave);
    }
    public bool Contiene(DateOnly d) => d >= Desde && d <= Hasta;

    // Primer día de cada mes tocado por el período
    public IEnumerable<DateOnly> Meses()
    {
        for (var m = InicioMes(Desde); m <= Hasta; m = m.AddMonths(1)) yield return m;
    }

    // Tramo del mes m que cae dentro del período (para series mensuales en rangos personalizados)
    public (DateOnly Desde, DateOnly Hasta) Tramo(DateOnly mes)
    {
        var ini = mes < Desde ? Desde : mes;
        var fin = FinMes(mes) > Hasta ? Hasta : FinMes(mes);
        return (ini, fin);
    }

    public string Texto => Clave == "custom"
        ? $"{Desde.ToString("dd/MM/yyyy", Es)} al {Hasta.ToString("dd/MM/yyyy", Es)}"
        : Presets.First(p => p.Clave == Clave).Texto;

    public static string NombreMes(DateOnly m, bool conAnio = false)
    {
        var t = m.ToString(conAnio ? "MMM yy" : "MMM", Es).Replace(".", "");
        return char.ToUpper(t[0]) + t[1..];
    }

    public static string NombreMesLargo(DateOnly m)
    {
        var t = m.ToString("MMMM", Es);
        return char.ToUpper(t[0]) + t[1..];
    }

    // Comparaciones estándar (una sola definición para Panel, sidebar y reportes)
    //  - Flujos (recaudación, altas…): mismo tramo transcurrido del período anterior.
    //  - Stocks (activos, morosidad): valor al cierre del mes anterior.
    public static (DateOnly Desde, DateOnly Hasta) MesActualHastaHoy(DateOnly hoy) => (InicioMes(hoy), hoy);

    public static (DateOnly Desde, DateOnly Hasta) MismoTramoMesAnterior(DateOnly hoy)
    {
        var ini = InicioMes(hoy).AddMonths(-1);
        var fin = ini.AddDays(hoy.Day - 1);
        return (ini, fin > FinMes(ini) ? FinMes(ini) : fin);   // 31/03 → 28/02
    }

    public static (DateOnly Desde, DateOnly Hasta) AnioHastaHoy(DateOnly hoy) => (new DateOnly(hoy.Year, 1, 1), hoy);

    public static (DateOnly Desde, DateOnly Hasta) MismoTramoAnioAnterior(DateOnly hoy)
    {
        var fin = hoy.Month == 2 && hoy.Day == 29 ? new DateOnly(hoy.Year - 1, 2, 28) : hoy.AddYears(-1);
        return (new DateOnly(hoy.Year - 1, 1, 1), fin);
    }

    // DateTime UTC de la base → fecha local del gimnasio
    public static DateOnly Local(DateTime utc) => DateOnly.FromDateTime(DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime());

    // Fecha local → inicio del día en UTC (para filtrar en la base)
    public static DateTime InicioUtc(DateOnly d) => d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();
}
