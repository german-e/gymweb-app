using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Pages.Entrenadores;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Socios;

public class CreateModel : SocioFormPageModel
{
    public CreateModel(GymDbContext db, IWebHostEnvironment env) : base(db, env) { }

    public override bool EsEdicion => false;

    public async Task OnGetAsync()
    {
        await CargarListasAsync();
        Input.NumeroSocio = await SociosConsultas.SiguienteNumeroAsync(Db);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await ValidarUnicidadAsync();
        Plan? plan = null;
        if (Input.PlanId is { } planId && (plan = await Db.Planes.FirstOrDefaultAsync(p => p.Id == planId)) is null)
            ModelState.AddModelError("Input.PlanId", "Plan inválido.");
        Entrenador? entrenador = null;
        if (Input.EntrenadorId is { } entId && (entrenador = await Db.Entrenadores.FirstOrDefaultAsync(e => e.Id == entId && e.Activo)) is null)
            ModelState.AddModelError("Input.EntrenadorId", "Entrenador inválido o inactivo.");

        if (!ModelState.IsValid)
        {
            await CargarListasAsync();
            return Page();
        }

        var socio = new Socio();
        Input.AplicarA(socio);
        // Igual que antes: el código de la credencial es inicialmente el DNI
        socio.TarjetaCodigo = socio.Dni;
        await ProcesarFotoAsync(socio);
        Db.Socios.Add(socio);

        if (plan is not null)
        {
            // Misma regla que Suscripciones/Nueva: inicio = fecha de alta, fin = inicio + duración del plan
            var inicio = Input.FechaAlta.Date;
            Db.SocioPlanes.Add(new SocioPlan
            {
                Socio = socio,
                PlanId = plan.Id,
                FechaInicio = inicio,
                FechaFin = inicio.AddDays(plan.DuracionDias),
                Estado = EstadoSuscripcion.Activo
            });
        }

        if (entrenador is not null)
        {
            Db.EntrenadorSocios.Add(new EntrenadorSocio { Entrenador = entrenador, Socio = socio });
            Db.RegistrarEvento(entrenador, TipoEventoEntrenador.AsignacionSocio, "Socio asignado", $"{socio.Apellido}, {socio.Nombre} (alta de socio)");
        }

        await Db.SaveChangesAsync();
        TempData["Flash"] = $"Alta registrada: {socio.NombreCompleto}. La credencial ya está disponible.";
        return RedirectToPage("Detalle", new { id = socio.Id });
    }
}
