using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GymCore.Web.Pages.Planes;

// Ruta histórica (?planId=): los socios del plan ahora viven en la ficha del plan.
[RequierePermiso(Permisos.PlanesVer)]
public class SuscriptosModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int PlanId { get; set; }

    public IActionResult OnGet() => RedirectToPage("/Planes/Detalle", null, new { id = PlanId }, "tab-socios");
}
