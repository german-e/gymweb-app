using GymCore.Web.Seguridad;
using GymCore.Aplicacion.Abstractiones;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GymCore.Web.Pages.Socios;

[RequierePermiso(Permisos.SociosEliminar)]
public class DeleteModel : PageModel
{
    private readonly ISocioRepository _repo;
    public DeleteModel(ISocioRepository repo) => _repo = repo;

    [BindProperty] public int Id { get; set; }
    [BindProperty] public string Dni { get; set; } = string.Empty;
    [BindProperty] public string Apellido { get; set; } = string.Empty;
    [BindProperty] public string Nombre { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        try
        {
            var s = await _repo.ObtenerPorIdAsync(id);
            Id = s.Id; Dni = s.Dni; Apellido = s.Apellido; Nombre = s.Nombre;
            return Page();
        }
        catch
        {
            return RedirectToPage("/Socios/Socios");
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await _repo.EliminarAsync(Id);
        return RedirectToPage("/Socios/Socios");
    }
}
