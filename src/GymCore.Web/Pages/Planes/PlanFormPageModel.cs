using GymCore.Web.Seguridad;
using System.ComponentModel.DataAnnotations;
using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GymCore.Web.Pages.Planes;

// Base compartida por Alta y Edición: mismo formulario (_PlanForm) y mismas validaciones.
[RequierePermiso(Permisos.PlanesEditar)]
public abstract class PlanFormPageModel : PageModel
{
    protected readonly GymDbContext Db;
    protected PlanFormPageModel(GymDbContext db) => Db = db;

    [BindProperty, Required(ErrorMessage = "Ingresá el nombre del plan."), StringLength(120, ErrorMessage = "Máximo 120 caracteres.")]
    [Display(Name = "Nombre del plan")]
    public string Nombre { get; set; } = string.Empty;

    [BindProperty, Display(Name = "Descripción")]
    public string? Descripcion { get; set; }

    [BindProperty, Required, Display(Name = "Tipo de plan")]
    public TipoPlan Tipo { get; set; } = TipoPlan.MensualIlimitado;

    [BindProperty, Range(0, double.MaxValue, ErrorMessage = "El precio no puede ser negativo.")]
    [Display(Name = "Precio")]
    public decimal Precio { get; set; }

    [BindProperty, Required(ErrorMessage = "Indicá la moneda."), StringLength(10, ErrorMessage = "Máximo 10 caracteres.")]
    [Display(Name = "Moneda")]
    public string Moneda { get; set; } = "ARS";

    [BindProperty, Range(1, 365, ErrorMessage = "Entre 1 y 365 días.")]
    [Display(Name = "Duración (días)")]
    public int DuracionDias { get; set; } = 30;

    [BindProperty, Display(Name = "Días por semana")]
    public int? MaxDiasPorSemana { get; set; }

    [BindProperty, Display(Name = "Acceso los sábados")]
    public bool PermiteAccesoSabado { get; set; } = true;

    [BindProperty, Display(Name = "Acceso los domingos")]
    public bool PermiteAccesoDomingo { get; set; } = true;

    // Plan de origen cuando el alta viene de "Duplicar" (queda en la auditoría)
    [BindProperty] public int? DuplicadoDeId { get; set; }

    public IEnumerable<SelectListItem> Tipos => Enum.GetValues<TipoPlan>()
        .Select(t => new SelectListItem(t.Texto(), ((int)t).ToString()));

    // Usados por el parcial del formulario
    public abstract bool EsEdicion { get; }
    public int? PlanId => RouteData.Values["id"] is { } id ? Convert.ToInt32(id) : null;

    protected void CargarDesde(Plan p)
    {
        Nombre = p.Nombre; Descripcion = p.Descripcion; Tipo = p.Tipo; Precio = p.Precio; Moneda = p.Moneda;
        DuracionDias = p.DuracionDias; MaxDiasPorSemana = p.MaxDiasPorSemana;
        PermiteAccesoSabado = p.PermiteAccesoSabado; PermiteAccesoDomingo = p.PermiteAccesoDomingo;
    }
}
