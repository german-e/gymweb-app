using GymCore.Web.Reportes;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Mvc;

namespace GymCore.Web.Pages.Reportes;

// Versión para PDF: misma tabla que el Excel, con diseño de impresión (A4 horizontal) y diálogo de impresión automático.
[RequierePermiso(Permisos.ReportesExportar)]
public class ImprimirModel : ReportesPageModel
{
    private readonly Exportador _exportador;
    public ImprimirModel(ServicioReportes reportes, Exportador exportador) : base(reportes) => _exportador = exportador;

    [BindProperty(SupportsGet = true)] public TipoReporte Tipo { get; set; }
    public TablaReporte Tabla { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        await CargarPeriodoAsync();
        Tabla = await _exportador.ConstruirAsync(Tipo, Periodo);
        await _exportador.AuditarAsync(Tabla, "PDF");
    }
}
