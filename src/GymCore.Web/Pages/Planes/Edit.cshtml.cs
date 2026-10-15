using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Planes;

public class EditModel : PlanFormPageModel
{
    public EditModel(GymDbContext db) : base(db) { }

    public override bool EsEdicion => true;

    [BindProperty] public int Id { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var p = await Db.Planes.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return RedirectToPage("/Planes/Planes");
        Id = p.Id;
        CargarDesde(p);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var p = await Db.Planes.FirstOrDefaultAsync(x => x.Id == Id);
        if (p is null) return RedirectToPage("/Planes/Planes");
        p.Nombre = Nombre.Trim();
        p.Descripcion = string.IsNullOrWhiteSpace(Descripcion) ? null : Descripcion.Trim();
        p.Tipo = Tipo;
        p.Precio = Precio;
        p.Moneda = Moneda.Trim();
        p.DuracionDias = DuracionDias;
        p.MaxDiasPorSemana = MaxDiasPorSemana;
        p.PermiteAccesoSabado = PermiteAccesoSabado;
        p.PermiteAccesoDomingo = PermiteAccesoDomingo;
        var campos = Db.CamposModificados(p, Auditoria.CamposPlan);
        if (campos.Count > 0)
            Db.Auditar(TipoEventoSistema.PlanEditado, EntidadAuditada.Plan, p.Id, p.Nombre, "Cambió: " + string.Join(", ", campos));
        await Db.SaveChangesAsync();
        TempData["Flash"] = "Cambios guardados.";
        return RedirectToPage("/Planes/Detalle", new { id = p.Id });
    }
}
