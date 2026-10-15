using System.ComponentModel.DataAnnotations;
using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
using GymCore.Web.Pages.Administracion.Usuarios;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Administracion.Roles;

// Alta y edición de roles. Los permisos nuevos impactan en la próxima request de cada usuario del rol.
[RequierePermiso(Permisos.UsuariosAdministrar)]
public class EditarModel : PageModel
{
    private readonly GymDbContext _db;
    public EditarModel(GymDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    public bool EsEdicion => Id.HasValue;

    [BindProperty, Required(ErrorMessage = "Ingresá el nombre del rol."), StringLength(60), Display(Name = "Nombre")]
    public string Nombre { get; set; } = "";

    [BindProperty, StringLength(300), Display(Name = "Descripción")]
    public string? Descripcion { get; set; }

    [BindProperty] public List<string> Seleccionados { get; set; } = new();

    public Rol? Existente { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!EsEdicion) return Page();
        Existente = await _db.Roles.AsNoTracking().Include(r => r.Permisos).Include(r => r.Usuarios).FirstOrDefaultAsync(r => r.Id == Id);
        if (Existente is null) return NotFound();
        Nombre = Existente.Nombre;
        Descripcion = Existente.Descripcion;
        Seleccionados = Existente.Permisos.Select(p => p.Permiso).ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var permisos = Seleccionados.Where(Permisos.Codigos.Contains).Distinct().ToList();   // solo códigos del catálogo
        var nombre = Nombre.Trim();
        Rol? rol = null;
        if (EsEdicion)
        {
            rol = await _db.Roles.Include(r => r.Permisos).Include(r => r.Usuarios).FirstOrDefaultAsync(r => r.Id == Id);
            if (rol is null) return NotFound();
            Existente = rol;
            if (rol.EsSistema) nombre = rol.Nombre;   // los roles iniciales no se renombran
        }
        if (await _db.Roles.AnyAsync(r => r.Nombre == nombre && r.Id != Id)) ModelState.AddModelError(nameof(Nombre), "Ya existe un rol con ese nombre.");

        if (rol is { AccesoTotal: false } && rol.Tiene(Permisos.UsuariosAdministrar) && !permisos.Contains(Permisos.UsuariosAdministrar)
            && !await UsuarioAcciones.QuedaAdministradorAsync(_db, rolEditadoId: rol.Id, rolEditadoAdministra: false))
            ModelState.AddModelError(nameof(Seleccionados), "Sin “Administrar usuarios y roles” en este rol no quedaría nadie que pueda administrar usuarios.");
        if (!ModelState.IsValid) return Page();

        if (rol is null)
        {
            rol = new Rol { Nombre = nombre, Descripcion = Descripcion?.Trim() };
            rol.AsignarPermisos(permisos);
            _db.Roles.Add(rol);
            _db.Auditar(TipoEventoSistema.RolCreado, EntidadAuditada.Rol, null, nombre, $"{permisos.Count} permisos", string.Join(", ", permisos), referencia: rol);
            await _db.SaveChangesAsync();
            TempData["Flash"] = $"Rol {nombre} creado. Ya podés asignarlo a usuarios.";
            return RedirectToPage("Index");
        }

        var antes = rol.Permisos.Select(p => p.Permiso).ToHashSet();
        var agregados = rol.AccesoTotal ? new List<string>() : permisos.Except(antes).ToList();
        var quitados = rol.AccesoTotal ? new List<string>() : antes.Except(permisos).ToList();
        rol.Nombre = nombre;
        rol.Descripcion = Descripcion?.Trim();
        if (!rol.AccesoTotal) rol.AsignarPermisos(permisos);
        if (agregados.Count + quitados.Count > 0)
            _db.Auditar(TipoEventoSistema.RolPermisosCambiados, EntidadAuditada.Rol, rol.Id, rol.Nombre,
                $"+{agregados.Count} / −{quitados.Count} permisos · afecta a {rol.Usuarios.Count} usuario(s)",
                string.Join("\n", agregados.Select(p => "+ " + (Permisos.Buscar(p)?.Nombre ?? p)).Concat(quitados.Select(p => "− " + (Permisos.Buscar(p)?.Nombre ?? p)))));
        await _db.SaveChangesAsync();
        TempData["Flash"] = agregados.Count + quitados.Count > 0
            ? $"Permisos de {rol.Nombre} actualizados. Se aplican de inmediato a sus {rol.Usuarios.Count} usuario(s)."
            : "Cambios guardados.";
        return RedirectToPage("Index");
    }
}
