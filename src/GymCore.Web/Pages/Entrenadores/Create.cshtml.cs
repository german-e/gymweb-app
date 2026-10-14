using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Mvc;

namespace GymCore.Web.Pages.Entrenadores;

public class CreateModel : EntrenadorFormPageModel
{
    public CreateModel(GymDbContext db, IWebHostEnvironment env) : base(db, env) { }

    public override bool EsEdicion => false;

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        await ValidarDniUnicoAsync();
        if (!ModelState.IsValid) return Page();

        var e = new Entrenador();
        Input.AplicarA(e);
        await ProcesarFotoAsync(e);
        Db.Entrenadores.Add(e);
        Db.RegistrarEvento(e, TipoEventoEntrenador.Alta, "Alta del entrenador",
            string.Join(" · ", new[] { e.Especialidad, EntrenadorPresentacion.DiasTexto(e.DiasTrabajo), EntrenadorPresentacion.HorarioTexto(e) }
                .Where(s => !string.IsNullOrEmpty(s))));
        await Db.SaveChangesAsync();

        TempData["Flash"] = $"Alta registrada: {e.NombreCompleto}.";
        return RedirectToPage("Detalle", new { id = e.Id });
    }
}
