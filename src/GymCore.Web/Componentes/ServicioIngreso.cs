using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Componentes;

// Reglas de control de ingreso (las mismas que tenía la API /api/asistencias/checkin, sin cambios):
// código = credencial o DNI · suscripción activa vigente · sábados/domingos según plan · duplicado reciente ·
// tope semanal en planes por días (con confirmación de cargo extra).
// Lo usan la API de recepción y el kiosco de autoservicio; cada uno presenta el resultado a su manera.
public class ServicioIngreso
{
    private readonly GymDbContext _db;
    public ServicioIngreso(GymDbContext db) => _db = db;

    public enum Estado { CodigoVacio, NoEncontrado, SinSuscripcion, SabadoNoPermitido, DomingoNoPermitido, Duplicado, CupoExcedido, Registrado }

    public sealed record Resultado(Estado Estado, Socio? Socio = null, SocioPlan? Suscripcion = null, Asistencia? Asistencia = null,
        int UsosSemana = 0, int? MaximoSemana = null)
    {
        public bool Ok => Estado is Estado.Registrado or Estado.Duplicado;
    }

    public async Task<Resultado> RegistrarAsync(string? codigo, bool confirmarExtra)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return new(Estado.CodigoVacio);
        var code = codigo.Trim();

        var socio = await _db.Socios
            .Include(s => s.Suscripciones)
                .ThenInclude(sp => sp.Plan)
            .FirstOrDefaultAsync(s => s.TarjetaCodigo == code || s.Dni == code);
        if (socio is null) return new(Estado.NoEncontrado);

        var ahora = DateTime.Now; // horario local del gym
        var activo = socio.Suscripciones
            .Where(sp => sp.Estado == EstadoSuscripcion.Activo
                       && sp.FechaInicio <= ahora
                       && sp.FechaFin >= ahora)
            .OrderByDescending(sp => sp.FechaFin)
            .FirstOrDefault();
        if (activo is null) return new(Estado.SinSuscripcion, socio);

        var dow = ahora.DayOfWeek;
        if (dow == DayOfWeek.Saturday && !activo.Plan.PermiteAccesoSabado) return new(Estado.SabadoNoPermitido, socio, activo);
        if (dow == DayOfWeek.Sunday && !activo.Plan.PermiteAccesoDomingo) return new(Estado.DomingoNoPermitido, socio, activo);

        // Duplicado reciente (5 minutos)
        var hace5 = DateTime.UtcNow.AddMinutes(-5);   // FechaHoraIngreso se guarda en UTC
        var duplicado = await _db.Asistencias.AnyAsync(a => a.SocioId == socio.Id && a.FechaHoraIngreso >= hace5);
        if (duplicado) return new(Estado.Duplicado, socio, activo);

        // Tope semanal si corresponde
        if (activo.Plan.Tipo == TipoPlan.MensualDiasPorSemana && activo.Plan.MaxDiasPorSemana.HasValue)
        {
            var inicioSemana = ahora.Date.AddDays(-(int)(dow == 0 ? 6 : dow - DayOfWeek.Monday)); // lunes
            var finSemana = inicioSemana.AddDays(7).ToUniversalTime();   // FechaHoraIngreso se guarda en UTC
            inicioSemana = inicioSemana.ToUniversalTime();
            var usos = await _db.Asistencias
                .CountAsync(a => a.SocioId == socio.Id && a.SocioPlanId == activo.Id && a.FechaHoraIngreso >= inicioSemana && a.FechaHoraIngreso < finSemana);
            if (usos >= activo.Plan.MaxDiasPorSemana.Value && !confirmarExtra)
                return new(Estado.CupoExcedido, socio, activo, UsosSemana: usos, MaximoSemana: activo.Plan.MaxDiasPorSemana);
        }

        var asistencia = new Asistencia { SocioId = socio.Id, SocioPlanId = activo.Id, FechaHoraIngreso = DateTime.UtcNow };
        _db.Asistencias.Add(asistencia);
        await _db.SaveChangesAsync();
        return new(Estado.Registrado, socio, activo, asistencia);
    }

    // Respuesta histórica de la API (mismo JSON que antes de extraer el servicio)
    public static object RespuestaApi(Resultado r)
    {
        object? socio = r.Socio is { } s ? new { s.Id, nombre = $"{s.Apellido}, {s.Nombre}" } : null;
        return r.Estado switch
        {
            Estado.NoEncontrado => new { ok = false, mensaje = "Socio no encontrado" },
            Estado.SinSuscripcion => new { ok = false, socio, mensaje = "Suscripción no activa o vencida" },
            Estado.SabadoNoPermitido => new { ok = false, socio, mensaje = "El plan no permite acceso los sábados" },
            Estado.DomingoNoPermitido => new { ok = false, socio, mensaje = "El plan no permite acceso los domingos" },
            Estado.Duplicado => new { ok = true, socio, mensaje = "Ingreso ya registrado recientemente" },
            Estado.CupoExcedido => new
            {
                ok = false, requiresExtra = true, socio, extraPrice = (decimal?)null,
                mensaje = $"Cupo semanal excedido ({r.UsosSemana}/{r.MaximoSemana}). Confirmar para registrar ingreso con cargo extra."
            },
            _ => new { ok = true, socio, mensaje = "Ingreso registrado" }
        };
    }
}
