using GymCore.Web.Reportes;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Mvc;

namespace GymCore.Web.Pages.Reportes;

// Exportaciones (Excel y PDF). Exportar datos de socios y cobros es sensible: permiso propio y auditoría.
[RequierePermiso(Permisos.ReportesExportar)]
public class ExportacionesModel : ReportesPageModel
{
    private readonly Exportador _exportador;
    private readonly GymCore.Web.Marca.ServicioBranding _branding;
    public ExportacionesModel(ServicioReportes reportes, Exportador exportador, GymCore.Web.Marca.ServicioBranding branding) : base(reportes)
    {
        _exportador = exportador; _branding = branding;
    }

    [BindProperty(SupportsGet = true)] public TipoReporte Tipo { get; set; }

    public async Task OnGetAsync() => await CargarPeriodoAsync();

    public async Task<IActionResult> OnGetExcelAsync()
    {
        await CargarPeriodoAsync();
        var tabla = await _exportador.ConstruirAsync(Tipo, Periodo);
        var bytes = _exportador.Excel(tabla, (await _branding.ActualAsync()).Nombre);
        await _exportador.AuditarAsync(tabla, "Excel");
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"{Tipo.ToString().ToLowerInvariant()}-{Periodo.Desde:yyyyMMdd}-{Periodo.Hasta:yyyyMMdd}.xlsx");
    }
}
