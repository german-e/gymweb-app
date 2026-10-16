using GymCore.Dominio.Entidades;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
using GymCore.Web.Marca;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Estado = GymCore.Web.Componentes.ServicioIngreso.Estado;

namespace GymCore.Web.Pages.Kiosk;

// Kiosco de autoservicio: el socio escanea su credencial (o tipea su DNI) y ve el resultado a pantalla completa.
// Corre en un equipo con sesión de un usuario con permiso de control de ingreso. Mismas reglas que la recepción;
// la única diferencia es que el kiosco no autoriza cargos extra (eso queda para el personal).
[RequierePermiso(Permisos.AsistenciasRegistrar)]
public class CheckinModel : PageModel
{
    private readonly GymDbContext _db;
    private readonly ServicioIngreso _ingreso;
    private readonly ServicioBranding _branding;
    public CheckinModel(GymDbContext db, ServicioIngreso ingreso, ServicioBranding branding) { _db = db; _ingreso = ingreso; _branding = branding; }

    public Branding Marca { get; private set; } = null!;
    public record AvisoKiosco(string Tipo, string Titulo, string? Texto, string? Imagen, string? Fecha);
    public List<AvisoKiosco> Avisos { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Marca = await _branding.ActualAsync();
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var avisos = await _db.AvisosRecepcion.AsNoTracking().Where(a => a.Activo).OrderBy(a => a.Orden).ThenByDescending(a => a.Id).ToListAsync();
        Avisos = avisos.Where(a => a.VigenteEn(hoy))
            .Select(a => new AvisoKiosco(Configuracion.RecepcionModel.Texto(a.Tipo), a.Titulo, a.Texto,
                a.ImagenUrl is null ? null : "/" + a.ImagenUrl,
                a.FechaEvento?.ToString("dddd d 'de' MMMM · HH:mm", new System.Globalization.CultureInfo("es-AR"))))
            .ToList();
    }

    public sealed record Respuesta(string Estado, string Titulo, string Mensaje, SocioKiosco? Socio, string Hora, string? Nota);
    public sealed record SocioKiosco(string Nombre, string Iniciales, string? Foto, string? Plan, string? Vence, string? Entrenador, int Numero);

    public async Task<IActionResult> OnPostIngresoAsync(string? codigo)
    {
        var r = await _ingreso.RegistrarAsync(codigo, confirmarExtra: false);
        var hora = DateTime.Now.ToString("HH:mm");
        var socio = r.Socio is { } s ? await SocioAsync(s, r.Suscripcion) : null;
        var ultima = r.Socio?.Suscripciones.OrderByDescending(sp => sp.FechaFin).FirstOrDefault();
        var plan = r.Suscripcion?.Plan;

        Respuesta R(string estado, string titulo, string mensaje, string? nota = null) => new(estado, titulo, mensaje, socio, hora, nota);
        var respuesta = r.Estado switch
        {
            Estado.Registrado => R("autorizado", "ACCESO AUTORIZADO", $"¡Bienvenido/a, {r.Socio!.Nombre}!"),
            Estado.Duplicado => R("autorizado", "ACCESO AUTORIZADO", $"¡Bienvenido/a, {r.Socio!.Nombre}!", "Tu ingreso ya estaba registrado hace unos minutos."),
            Estado.SinSuscripcion => R("restringido", "ACCESO RESTRINGIDO",
                ultima is null ? "No tenés una membresía activa." : $"Tu membresía venció el {ultima.FechaFin:dd/MM/yyyy}.", "Diríjase a recepción"),
            Estado.SabadoNoPermitido => R("restringido", "ACCESO RESTRINGIDO", $"Tu plan {plan?.Nombre} no incluye los sábados.", "Diríjase a recepción"),
            Estado.DomingoNoPermitido => R("restringido", "ACCESO RESTRINGIDO", $"Tu plan {plan?.Nombre} no incluye los domingos.", "Diríjase a recepción"),
            Estado.CupoExcedido => R("restringido", "ACCESO RESTRINGIDO", $"Ya usaste tus {r.MaximoSemana} ingresos de esta semana.", "Diríjase a recepción"),
            _ => new Respuesta("denegado", "ACCESO DENEGADO", "Código no válido", null, hora, "Probá de nuevo o acercate a recepción.")
        };
        return new JsonResult(respuesta);
    }

    private async Task<SocioKiosco> SocioAsync(Socio s, SocioPlan? vigente)
    {
        var referencia = vigente ?? s.Suscripciones.OrderByDescending(sp => sp.FechaFin).FirstOrDefault();
        var entrenador = await _db.EntrenadorSocios.AsNoTracking()
            .Where(a => a.SocioId == s.Id && a.Activa)
            .Select(a => a.Entrenador.Nombre + " " + a.Entrenador.Apellido)
            .FirstOrDefaultAsync();
        return new SocioKiosco($"{s.Nombre} {s.Apellido}", s.Iniciales, s.FotoUrl is null ? null : "/" + s.FotoUrl,
            referencia?.Plan.Nombre, referencia?.FechaFin.ToString("dd/MM/yyyy"), entrenador, s.NumeroVisible);
    }
}
