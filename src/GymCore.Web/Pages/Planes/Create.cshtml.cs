using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Planes;

public class CreateModel : PlanFormPageModel
{
    public CreateModel(GymDbContext db) : base(db) { }

    public override bool EsEdicion => false;

    // Nombre del plan de origen cuando se llega desde "Duplicar"
    public string? DuplicadoDe { get; private set; }

    public async Task OnGetAsync(int? duplicar)
    {
        if (duplicar is null) return;
        var origen = await Db.Planes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == duplicar);
        if (origen is null) return;
        CargarDesde(origen);
        var nombre = $"{origen.Nombre} (copia)";
        Nombre = nombre.Length > 120 ? nombre[..120] : nombre;
        DuplicadoDe = origen.Nombre;
        DuplicadoDeId = origen.Id;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var plan = new Plan
        {
            Nombre = Nombre.Trim(),
            Descripcion = string.IsNullOrWhiteSpace(Descripcion) ? null : Descripcion.Trim(),
            Tipo = Tipo,
            Precio = Precio,
            Moneda = Moneda.Trim(),
            DuracionDias = DuracionDias,
            MaxDiasPorSemana = MaxDiasPorSemana,
            PermiteAccesoSabado = PermiteAccesoSabado,
            PermiteAccesoDomingo = PermiteAccesoDomingo,
            Activo = true,
            FechaAlta = DateTime.UtcNow
        };
        Db.Planes.Add(plan);
        Db.Auditar(TipoEventoSistema.PlanCreado, EntidadAuditada.Plan, null, plan.Nombre,
            $"{GymCore.Web.Pages.Socios.SocioPresentacion.Dinero(plan.Precio, plan.Moneda)} · {PlanPresentacion.Duracion(plan.DuracionDias)} · {plan.Tipo.Texto()}",
            DuplicadoDeId is { } origen ? $"Duplicado del plan #{origen}" : null, referencia: plan);
        await Db.SaveChangesAsync();
        TempData["Flash"] = $"Se creó el plan {plan.Nombre}.";
        return RedirectToPage("/Planes/Planes");
    }
}
