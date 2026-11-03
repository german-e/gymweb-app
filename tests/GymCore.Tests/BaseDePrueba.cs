using GymCore.Aplicacion.Abstractiones;
using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Tests;

// Base PostgreSQL propia de cada prueba, con el esquema al día (copiada de la plantilla migrada).
// Mismo motor que producción. Se borra al terminar la prueba.
public sealed class BaseDePrueba : IDisposable
{
    private readonly string _cadena = PostgresDePrueba.NuevaBase();

    private sealed record Operador(string Nombre, int? UsuarioId = 7) : IOperadorAuditoria;

    public GymDbContext Contexto(string? operador = "Cajero Test") =>
        new(new DbContextOptionsBuilder<GymDbContext>().UseNpgsql(_cadena).Options,
            operador is null ? null : new Operador(operador));

    public string Cadena => _cadena;

    // Socio con una suscripción vigente al plan del precio indicado
    public async Task<(Socio Socio, SocioPlan Suscripcion)> SocioConPlanAsync(decimal precio = 1000m)
    {
        using var db = Contexto();
        var plan = new Plan { Nombre = $"Plan {precio}", Tipo = TipoPlan.MensualIlimitado, Precio = precio, DuracionDias = 30 };
        var socio = new Socio { Nombre = "Ana", Apellido = "Prueba", Dni = Guid.NewGuid().ToString("N")[..8] };
        var sp = new SocioPlan { Socio = socio, Plan = plan, FechaInicio = DateTime.Today, FechaFin = DateTime.Today.AddDays(30), Estado = EstadoSuscripcion.Activo };
        db.SocioPlanes.Add(sp);
        await db.SaveChangesAsync();
        return (socio, sp);
    }

    public async Task<Pago> PagoAsync(int socioId, int? socioPlanId, decimal importe)
    {
        using var db = Contexto();
        var p = new Pago { SocioId = socioId, SocioPlanId = socioPlanId, Importe = importe, Medio = MedioPago.Efectivo, FechaPago = DateTime.UtcNow };
        db.Pagos.Add(p);
        await db.SaveChangesAsync();
        return p;
    }

    public async Task AnularAsync(int pagoId)
    {
        using var db = Contexto();
        var p = await db.Pagos.IgnoreQueryFilters().FirstAsync(x => x.Id == pagoId);
        p.Anular("Cobro duplicado", "Cajero Test", DateTime.UtcNow);
        await db.SaveChangesAsync();
    }

    public void Dispose() => PostgresDePrueba.Borrar(_cadena);
}
