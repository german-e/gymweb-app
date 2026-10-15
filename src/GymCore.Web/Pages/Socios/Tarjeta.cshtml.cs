using GymCore.Web.Seguridad;
using GymCore.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GymCore.Web.Pages.Socios;

[RequierePermiso(Permisos.CredencialesEmitir)]
public class TarjetaModel : PageModel
{
    private readonly GymDbContext _db;
    public TarjetaModel(GymDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    // ?imprimir=true abre el diálogo de impresión al cargar
    [BindProperty(SupportsGet = true)]
    public bool Imprimir { get; set; }

    public ResumenSocio R { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync()
    {
        var resumen = await SociosConsultas.ResumenAsync(_db, new[] { Id });
        if (!resumen.TryGetValue(Id, out var r)) return NotFound();
        R = r;
        return Page();
    }
}
