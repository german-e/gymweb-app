using System.ComponentModel.DataAnnotations;
using GymCore.Aplicacion.Abstractiones;
using GymCore.Dominio.Entidades;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GymCore.Web.Pages.Socios;

public class CreateModel : PageModel
{
    private readonly ISocioRepository _repo;
    public CreateModel(ISocioRepository repo) => _repo = repo;

    [BindProperty, Required, StringLength(20)] public string Dni { get; set; } = string.Empty;
    [BindProperty, Required, StringLength(80)] public string Apellido { get; set; } = string.Empty;
    [BindProperty, Required, StringLength(80)] public string Nombre { get; set; } = string.Empty;

    public void OnGet() {}

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var socio = new Socio
        {
            Dni = Dni.Trim(),
            Apellido = Apellido.Trim(),
            Nombre = Nombre.Trim(),
            FechaAlta = DateTime.UtcNow
        };
        await _repo.AgregarAsync(socio);
        return RedirectToPage("/Socios/Socios");
    }
}
