using GymCore.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Socios;

public class EditModel : SocioFormPageModel
{
    public EditModel(GymDbContext db, IWebHostEnvironment env) : base(db, env) { }

    public override bool EsEdicion => true;
    public override int? IdSocio => Id;

    // Se mantiene la ruta original (?id=) para no romper enlaces existentes
    [BindProperty(SupportsGet = true)] public int Id { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var s = await Db.Socios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == Id);
        if (s is null) return RedirectToPage("/Socios/Socios");
        Input = SocioInput.Desde(s);
        await CargarContextoAsync(s.FotoUrl);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Se actualiza la entidad cargada (antes se reemplazaba por una nueva y se perdían TarjetaCodigo y FechaAlta)
        var s = await Db.Socios.FirstOrDefaultAsync(x => x.Id == Id);
        if (s is null) return RedirectToPage("/Socios/Socios");

        await ValidarUnicidadAsync(Id);
        if (!ModelState.IsValid)
        {
            await CargarContextoAsync(s.FotoUrl);
            return Page();
        }

        Input.AplicarA(s);
        await ProcesarFotoAsync(s);
        await Db.SaveChangesAsync();
        TempData["Flash"] = "Cambios guardados.";
        return RedirectToPage("Detalle", new { id = s.Id });
    }

    private async Task CargarContextoAsync(string? foto)
    {
        FotoActualUrl = foto;
        var r = (await SociosConsultas.ResumenAsync(Db, new[] { Id })).GetValueOrDefault(Id);
        PlanActual = r?.PlanNombre;
        EntrenadorActual = r?.Entrenador?.NombreCompleto;
    }
}
