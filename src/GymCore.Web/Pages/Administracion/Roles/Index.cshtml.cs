using GymCore.Dominio.Entidades;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Administracion.Roles;

// Roles y matriz de permisos (solo lectura; la edición está en Editar)
[RequierePermiso(Permisos.UsuariosAdministrar)]
public class IndexModel : PageModel
{
    private readonly GymDbContext _db;
    public IndexModel(GymDbContext db) => _db = db;

    public List<Rol> Roles { get; private set; } = new();
    [Microsoft.AspNetCore.Mvc.TempData] public string? Flash { get; set; }

    public async Task OnGetAsync()
    {
        Roles = await _db.Roles.AsNoTracking().Include(r => r.Permisos).Include(r => r.Usuarios)
            .OrderByDescending(r => r.AccesoTotal).ThenByDescending(r => r.EsSistema).ThenBy(r => r.Nombre).ToListAsync();
    }
}
