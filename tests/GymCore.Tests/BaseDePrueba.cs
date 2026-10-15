using GymCore.Aplicacion.Abstractiones;
using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Tests;

// Base SQLite en memoria (mismo motor que producción). La conexión queda abierta durante el test.
public sealed class BaseDePrueba : IDisposable
{
    private readonly SqliteConnection _conexion = new("Data Source=:memory:");

    public BaseDePrueba()
    {
        _conexion.Open();
        using var db = Contexto();
        db.Database.EnsureCreated();
    }

    private sealed record Operador(string Nombre, int? UsuarioId = 7) : IOperadorAuditoria;

    public GymDbContext Contexto(string? operador = "Cajero Test") =>
        new(new DbContextOptionsBuilder<GymDbContext>().UseSqlite(_conexion).Options,
            operador is null ? null : new Operador(operador));

    public SqliteConnection Conexion => _conexion;

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

    public void Dispose() => _conexion.Dispose();
}
