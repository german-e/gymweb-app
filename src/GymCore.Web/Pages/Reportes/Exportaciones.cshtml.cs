using GymCore.Web.Reportes;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Mvc;

namespace GymCore.Web.Pages.Reportes;

// Exportaciones (Excel y PDF). Exportar datos de socios y cobros es sensible: permiso propio y auditoría.
[RequierePermiso(Permisos.ReportesExportar)]
public class ExportacionesModel : ReportesPageModel
{
    private readonly Exportador _exportador;
    private readonly IConfiguration _config;
    public ExportacionesModel(ServicioReportes reportes, Exportador exportador, IConfiguration config) : base(reportes)
    {
        _exportador = exportador; _config = config;
    }

    [BindProperty(SupportsGet = true)] public TipoReporte Tipo { get; set; }

    public async Task OnGetAsync() => await CargarPeriodoAsync();

    public async Task<IActionResult> OnGetExcelAsync()
    {
        await CargarPeriodoAsync();
        var tabla = await _exportador.ConstruirAsync(Tipo, Periodo);
        var bytes = _exportador.Excel(tabla, _config["Gimnasio:Nombre"] ?? "GymCore");
        await _exportador.AuditarAsync(tabla, "Excel");
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"{Tipo.ToString().ToLowerInvariant()}-{Periodo.Desde:yyyyMMdd}-{Periodo.Hasta:yyyyMMdd}.xlsx");
    }
}
