using ClosedXML.Excel;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
using GymCore.Web.Pages.Socios;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Reportes;

public enum TipoReporte { Socios, Ingresos, Pagos, Planes, Entrenadores, Riesgo }
public enum TipoColumna { Texto, Numero, Dinero, Porcentaje, Fecha }

public sealed record Columna(string Nombre, TipoColumna Tipo = TipoColumna.Texto, bool Total = false);

// Tabla exportable: la misma definición alimenta el Excel y la vista para PDF (nunca muestran datos distintos)
public sealed record TablaReporte(TipoReporte Tipo, string Titulo, string Subtitulo, IReadOnlyList<Columna> Columnas, IReadOnlyList<object?[]> Filas)
{
    public decimal? TotalDe(int col) => Columnas[col].Total ? Filas.Sum(f => f[col] is IConvertible c ? Convert.ToDecimal(c) : 0m) : null;
}

public sealed class Exportador
{
    private readonly ServicioReportes _reportes;
    private readonly GymDbContext _db;
    private readonly IUsuarioActual _usuario;
    public Exportador(ServicioReportes reportes, GymDbContext db, IUsuarioActual usuario) { _reportes = reportes; _db = db; _usuario = usuario; }

    public static readonly (TipoReporte Tipo, string Titulo, string Icono, string Descripcion, bool UsaPeriodo)[] Catalogo =
    {
        (TipoReporte.Socios, "Socios", "bi-people", "Padrón completo con estado, plan, vencimiento, última asistencia, saldo y riesgo.", false),
        (TipoReporte.Ingresos, "Ingresos", "bi-cash-stack", "Recaudación por mes, pagos, socios activos e ingreso por socio.", true),
        (TipoReporte.Pagos, "Pagos", "bi-receipt", "Detalle de cada cobro del período, con anulaciones y responsable.", true),
        (TipoReporte.Planes, "Planes", "bi-box-seam", "Ventas, renovaciones, socios activos, participación e ingreso por plan.", true),
        (TipoReporte.Entrenadores, "Entrenadores", "bi-person-badge", "Cartera, participación y nuevas asignaciones por entrenador.", true),
        (TipoReporte.Riesgo, "Riesgo de abandono", "bi-exclamation-octagon", "Socios evaluados con puntaje, factores y recomendación.", false),
    };

    public async Task<TablaReporte> ConstruirAsync(TipoReporte tipo, Periodo p)
    {
        var h = await _reportes.HechosAsync();
        var sub = Catalogo.First(c => c.Tipo == tipo).UsaPeriodo ? $"Período: {p.Desde:dd/MM/yyyy} al {p.Hasta:dd/MM/yyyy}" : $"Situación al {h.Hoy:dd/MM/yyyy}";
        switch (tipo)
        {
            case TipoReporte.Socios:
            {
                var riesgo = (await _reportes.RiesgoAsync()).Socios.ToDictionary(x => x.Socio.Id);
                var filas = h.Socios.OrderBy(s => s.Apellido).ThenBy(s => s.Nombre).Select(s =>
                {
                    var vig = h.VigenteEn(s.Id, h.Hoy);
                    var ultima = h.SubsPorSocio[s.Id].MaxBy(x => x.Fin);
                    var estado = s.Baja is { } b && b <= h.Hoy ? "Baja" : vig is not null ? "Activo" : ultima is not null ? "Vencido" : "Sin plan";
                    var plan = h.Planes.FirstOrDefault(x => x.Id == (vig ?? ultima)?.PlanId)?.Nombre;
                    var saldo = vig is null ? 0 : Math.Max(0, vig.Precio - h.PagadoHasta(vig.Id, h.Hoy));
                    return new object?[]
                    {
                        s.Apellido, s.Nombre, s.Dni, s.Telefono, s.Alta.ToDateTime(TimeOnly.MinValue), estado, plan,
                        (vig ?? ultima)?.Fin.ToDateTime(TimeOnly.MinValue),
                        h.UltimaAsistencia.TryGetValue(s.Id, out var u) ? u.ToDateTime(TimeOnly.MinValue) : null,
                        saldo, riesgo.TryGetValue(s.Id, out var rr) ? rr.Nivel.Estilo().Texto : "—"
                    };
                }).ToList();
                return new(tipo, "Padrón de socios", sub, new Columna[]
                {
                    new("Apellido"), new("Nombre"), new("DNI"), new("Teléfono"), new("Alta", TipoColumna.Fecha), new("Estado"), new("Plan"),
                    new("Vence", TipoColumna.Fecha), new("Última asistencia", TipoColumna.Fecha), new("Saldo", TipoColumna.Dinero, true), new("Riesgo")
                }, filas);
            }
            case TipoReporte.Ingresos:
            {
                var r = await _reportes.IngresosAsync(p);
                return new(tipo, "Ingresos por mes", sub, new Columna[]
                {
                    new("Mes"), new("Recaudación", TipoColumna.Dinero, true), new("Pagos", TipoColumna.Numero, true),
                    new("Socios activos al cierre", TipoColumna.Numero), new("Ingreso por socio", TipoColumna.Dinero)
                }, r.Meses.Select(m => new object?[] { $"{Periodo.NombreMesLargo(m.Mes)} {m.Mes.Year}{(m.Completo ? "" : " (parcial)")}", m.Total, m.Pagos, m.Activos, m.PorSocio }).ToList());
            }
            case TipoReporte.Pagos:
            {
                var desde = Periodo.InicioUtc(p.Desde);
                var hasta = Periodo.InicioUtc(p.Hasta.AddDays(1));
                var pagos = await _db.Pagos.IgnoreQueryFilters().AsNoTracking()
                    .Where(x => x.FechaPago >= desde && x.FechaPago < hasta)
                    .OrderBy(x => x.FechaPago)
                    .Select(x => new
                    {
                        x.Id, x.FechaPago, x.Socio.Apellido, x.Socio.Nombre, x.Socio.Dni, Plan = x.SocioPlan != null ? x.SocioPlan.Plan.Nombre : null,
                        x.Medio, x.Importe, x.Anulado, x.UsuarioCajero, x.MotivoAnulacion, x.ReferenciaExterna
                    })
                    .ToListAsync();
                return new(tipo, "Detalle de pagos", sub + " · los anulados no suman en el total", new Columna[]
                {
                    new("Fecha", TipoColumna.Fecha), new("Recibo"), new("Socio"), new("DNI"), new("Concepto"), new("Medio"), new("Referencia"),
                    new("Importe", TipoColumna.Dinero, true), new("Estado"), new("Registró"), new("Motivo de anulación")
                }, pagos.Select(x => new object?[]
                {
                    x.FechaPago.ToLocalTime(), $"R-{x.Id:D6}", $"{x.Apellido}, {x.Nombre}", x.Dni, x.Plan is null ? "Pago a cuenta" : $"Cuota {x.Plan}",
                    x.Medio.Texto(), x.ReferenciaExterna, x.Anulado ? 0m : x.Importe, x.Anulado ? $"Anulado ({MetricasNegocio.Dinero(x.Importe)})" : "Pagado",
                    x.UsuarioCajero, x.MotivoAnulacion
                }).ToList());
            }
            case TipoReporte.Planes:
            {
                var r = await _reportes.PlanesAsync(p);
                return new(tipo, "Rendimiento de planes", sub, new Columna[]
                {
                    new("Plan"), new("Estado"), new("Precio", TipoColumna.Dinero), new("Ventas", TipoColumna.Numero, true), new("Renovaciones", TipoColumna.Numero, true),
                    new("Socios activos", TipoColumna.Numero, true), new("Participación", TipoColumna.Porcentaje), new("Ingreso", TipoColumna.Dinero, true)
                }, r.Filas.Select(f => new object?[] { f.Plan.Nombre, f.Plan.Activo ? "Activo" : "Inactivo", f.Plan.Precio, f.Ventas, f.Renovaciones, f.SociosActivos, f.Participacion, f.Ingreso }).ToList());
            }
            case TipoReporte.Entrenadores:
            {
                var r = await _reportes.EntrenadoresAsync(p);
                return new(tipo, "Análisis de entrenadores", sub, new Columna[]
                {
                    new("Entrenador"), new("Estado"), new("Cartera", TipoColumna.Numero, true), new("Participación", TipoColumna.Porcentaje), new("Nuevos en el período", TipoColumna.Numero, true)
                }, r.Filas.Select(f => new object?[] { f.Entrenador.NombreCompleto, f.Entrenador.Activo ? "Activo" : "Inactivo", f.Cartera, f.Participacion, f.Nuevos }).ToList());
            }
            default:
            {
                var r = await _reportes.RiesgoAsync();
                return new(tipo, "Riesgo de abandono", sub, new Columna[]
                {
                    new("Socio"), new("DNI"), new("Teléfono"), new("Plan"), new("Días sin asistir", TipoColumna.Numero), new("Membresía"),
                    new("Puntaje", TipoColumna.Numero), new("Nivel"), new("Factores"), new("Recomendación")
                }, r.Socios.Select(s => new object?[]
                {
                    $"{s.Socio.Apellido}, {s.Socio.Nombre}", s.Socio.Dni, s.Socio.Telefono, s.Plan, s.DiasSinAsistir,
                    s.Vigente ? $"Vence {s.Vence:dd/MM/yyyy}" : "Vencida", s.Puntaje, s.Nivel.Estilo().Texto,
                    string.Join(" · ", s.Factores.Select(f => f.Texto)), s.Recomendacion
                }).ToList());
            }
        }
    }

    public byte[] Excel(TablaReporte t, string gimnasio)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(t.Titulo.Length > 31 ? t.Titulo[..31] : t.Titulo);
        ws.Cell(1, 1).Value = $"{gimnasio} · {t.Titulo}";
        ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        ws.Cell(2, 1).Value = $"{t.Subtitulo} · Generado por {_usuario.Nombre} el {DateTime.Now:dd/MM/yyyy HH:mm}";
        ws.Cell(2, 1).Style.Font.SetFontColor(XLColor.FromHtml("#64748b"));

        const int fila0 = 4;
        for (var c = 0; c < t.Columnas.Count; c++)
        {
            var celda = ws.Cell(fila0, c + 1);
            celda.Value = t.Columnas[c].Nombre;
            celda.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#eef4ff")).Font.SetFontColor(XLColor.FromHtml("#1d4ed8"));
        }
        for (var f = 0; f < t.Filas.Count; f++)
            for (var c = 0; c < t.Columnas.Count; c++)
                Escribir(ws.Cell(fila0 + 1 + f, c + 1), t.Filas[f][c], t.Columnas[c].Tipo);

        var filaTotal = fila0 + t.Filas.Count + 1;
        if (t.Columnas.Any(c => c.Total) && t.Filas.Count > 0)
        {
            ws.Cell(filaTotal, 1).Value = "Total";
            for (var c = 0; c < t.Columnas.Count; c++)
                if (t.TotalDe(c) is { } tot) Escribir(ws.Cell(filaTotal, c + 1), tot, t.Columnas[c].Tipo);
            ws.Row(filaTotal).Style.Font.SetBold().Border.SetTopBorder(XLBorderStyleValues.Thin);
        }
        if (t.Filas.Count > 0) ws.Range(fila0, 1, fila0 + t.Filas.Count, t.Columnas.Count).SetAutoFilter();
        ws.SheetView.FreezeRows(fila0);
        ws.Columns().AdjustToContents(fila0, Math.Max(fila0, filaTotal), 8, 60);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void Escribir(IXLCell celda, object? v, TipoColumna tipo)
    {
        switch (v)
        {
            case null: return;
            case DateTime d: celda.Value = d; celda.Style.DateFormat.Format = d.TimeOfDay == TimeSpan.Zero ? "dd/mm/yyyy" : "dd/mm/yyyy hh:mm"; return;
            case decimal or int or double or long:
                var n = Convert.ToDouble(v);
                if (tipo == TipoColumna.Porcentaje) { celda.Value = n / 100; celda.Style.NumberFormat.Format = "0.0%"; }
                else { celda.Value = n; celda.Style.NumberFormat.Format = tipo == TipoColumna.Dinero ? "\"$\" #,##0" : "#,##0"; }
                return;
            default: celda.Value = v.ToString(); return;
        }
    }

    // Texto para la vista de impresión / PDF
    public static string Formatear(object? v, TipoColumna tipo) => v switch
    {
        null => "—",
        DateTime d => d.TimeOfDay == TimeSpan.Zero ? d.ToString("dd/MM/yyyy") : d.ToString("dd/MM/yyyy HH:mm"),
        decimal or int or double or long when tipo == TipoColumna.Dinero => MetricasNegocio.Dinero(Convert.ToDecimal(v)),
        decimal or int or double or long when tipo == TipoColumna.Porcentaje => Convert.ToDecimal(v).ToString("0.#", Periodo.Es) + "%",
        decimal or int or double or long => Convert.ToDecimal(v).ToString("N0", Periodo.Es),
        _ => v.ToString() ?? ""
    };

    // Toda exportación queda en la auditoría (quién, qué reporte, formato y alcance)
    public async Task AuditarAsync(TablaReporte t, string formato)
    {
        _db.Auditar(TipoEventoSistema.ReporteExportado, EntidadAuditada.Reporte, null, t.Titulo, $"{formato} · {t.Filas.Count} filas · {t.Subtitulo}");
        await _db.SaveChangesAsync();
    }
}
