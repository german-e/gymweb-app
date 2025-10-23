using System.ComponentModel.DataAnnotations;
using GymCore.Aplicacion.Abstractiones;
using GymCore.Dominio.Entidades;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GymCore.Web.Pages.Socios;

public class EditModel : PageModel
{
    private readonly ISocioRepository _repo;
    public EditModel(ISocioRepository repo) => _repo = repo;

    [BindProperty] public int Id { get; set; }
    [BindProperty, Required, StringLength(20)] public string Dni { get; set; } = string.Empty;
    [BindProperty, Required, StringLength(80)] public string Apellido { get; set; } = string.Empty;
    [BindProperty, Required, StringLength(80)] public string Nombre { get; set; } = string.Empty;

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
        if (!ModelState.IsValid) return Page();
        var s = new Socio { Id = Id, Dni = Dni.Trim(), Apellido = Apellido.Trim(), Nombre = Nombre.Trim() };
        await _repo.ActualizarAsync(s);
        return RedirectToPage("/Socios/Socios");
    }
}
