using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Entrenadores;

public class EditModel : EntrenadorFormPageModel
{
    public EditModel(GymDbContext db, IWebHostEnvironment env) : base(db, env) { }

    public override bool EsEdicion => true;

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var e = await Db.Entrenadores.AsNoTracking().FirstOrDefaultAsync(x => x.Id == Id);
        if (e is null) return NotFound();
        Input = EntrenadorInput.Desde(e);
        FotoActualUrl = e.FotoUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var e = await Db.Entrenadores.FirstOrDefaultAsync(x => x.Id == Id);
        if (e is null) return NotFound();
        FotoActualUrl = e.FotoUrl;

        await ValidarDniUnicoAsync(Id);
        if (!ModelState.IsValid) return Page();

        // Snapshot para el historial
        var horarioAntes = $"{EntrenadorPresentacion.DiasTexto(e.DiasTrabajo)} · {EntrenadorPresentacion.HorarioTexto(e)} · {e.Turno.Texto()}";
        var activoAntes = e.Activo;
        var entrada = Db.Entry(e);

        Input.AplicarA(e);
        var cambioFoto = await ProcesarFotoAsync(e);
        Db.ChangeTracker.DetectChanges(); // necesario para que IsModified refleje los cambios

        var horarioDespues = $"{EntrenadorPresentacion.DiasTexto(e.DiasTrabajo)} · {EntrenadorPresentacion.HorarioTexto(e)} · {e.Turno.Texto()}";
        var camposHorario = new[] { nameof(Entrenador.DiasTrabajo), nameof(Entrenador.HorarioDesde), nameof(Entrenador.HorarioHasta), nameof(Entrenador.Turno) };
        var camposEstado = new[] { nameof(Entrenador.Activo), nameof(Entrenador.FechaBaja) };
        var otrosCambios = entrada.Properties
            .Where(p => p.IsModified && !camposHorario.Contains(p.Metadata.Name) && !camposEstado.Contains(p.Metadata.Name))
            .Select(p => Etiqueta(p.Metadata.Name))
            .ToList();
        if (cambioFoto && !otrosCambios.Contains("Foto")) otrosCambios.Add("Foto");

        if (horarioAntes != horarioDespues)
            Db.RegistrarEvento(e, TipoEventoEntrenador.CambioHorario, "Cambio de horario", $"{horarioAntes}\n→ {horarioDespues}");
        if (activoAntes != e.Activo)
            Db.RegistrarEvento(e, e.Activo ? TipoEventoEntrenador.Activacion : TipoEventoEntrenador.Desactivacion,
                e.Activo ? "Entrenador reactivado" : "Entrenador desactivado");
        if (otrosCambios.Count > 0)
            Db.RegistrarEvento(e, TipoEventoEntrenador.Modificacion, "Datos actualizados", "Campos: " + string.Join(", ", otrosCambios.Distinct()));

        await Db.SaveChangesAsync();
        TempData["Flash"] = "Cambios guardados.";
        return RedirectToPage("Detalle", new { id = e.Id });
    }

    private static string Etiqueta(string campo) => campo switch
    {
        nameof(Entrenador.FotoUrl) => "Foto",
        nameof(Entrenador.FechaNacimiento) => "Fecha de nacimiento",
        nameof(Entrenador.InstitucionFormadora) => "Institución formadora",
        nameof(Entrenador.AniosExperiencia) => "Años de experiencia",
        nameof(Entrenador.DescripcionProfesional) => "Descripción profesional",
        nameof(Entrenador.FechaAlta) => "Fecha de alta",
        nameof(Entrenador.Direccion) => "Dirección",
        nameof(Entrenador.Telefono) => "Teléfono",
        _ => campo
    };
}
