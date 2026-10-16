using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Marca;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Configuracion;

[RequierePermiso(Permisos.ConfiguracionAdministrar)]
public class IndexModel : PageModel
{
    private readonly GymDbContext _db;
    private readonly ServicioBranding _branding;
    public IndexModel(GymDbContext db, ServicioBranding branding) { _db = db; _branding = branding; }

    public Branding Marca { get; private set; } = null!;
    public int AvisosVigentes { get; private set; }

    public async Task OnGetAsync()
    {
        Marca = await _branding.ActualAsync();
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        AvisosVigentes = (await _db.AvisosRecepcion.AsNoTracking().Where(a => a.Activo).ToListAsync()).Count(a => a.VigenteEn(hoy));
    }
}
