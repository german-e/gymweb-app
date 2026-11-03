using GymCore.Dominio.Entidades;
using GymCore.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GymCore.Tests;

// Migraciones de EF Core sobre PostgreSQL: el modelo y las migraciones coinciden, una base vacía se crea
// desde cero, arrancar dos veces no repite nada, y las fechas se guardan y leen sin corrimientos.
public sealed class MigracionesTests : IDisposable
{
    private readonly string _cadena = PostgresDePrueba.BaseVacia();

    private GymDbContext Contexto() => new(new DbContextOptionsBuilder<GymDbContext>().UseNpgsql(_cadena).Options);

    public void Dispose() => PostgresDePrueba.Borrar(_cadena);

    [Fact]
    public void El_proyecto_tiene_migraciones_y_el_modelo_no_tiene_cambios_sin_migrar()
    {
        using var db = Contexto();
        Assert.NotEmpty(db.Database.GetMigrations());
        // Si falla: se cambió una entidad o su configuración sin generar la migración (ver docs/migraciones.md)
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Una_base_vacia_se_crea_aplicando_todas_las_migraciones()
    {
        using (var db = Contexto())
            await InicializadorBaseDatos.MigrarAsync(db, NullLogger.Instance);

        using var lectura = Contexto();
        Assert.Empty(await lectura.Database.GetPendingMigrationsAsync());
        Assert.Equal(lectura.Database.GetMigrations(), await lectura.Database.GetAppliedMigrationsAsync());
        Assert.Equal(0, await lectura.Socios.CountAsync());
    }

    [Fact]
    public async Task Arrancar_dos_veces_no_vuelve_a_aplicar_nada()
    {
        using (var db = Contexto()) await InicializadorBaseDatos.MigrarAsync(db, NullLogger.Instance);
        using (var db = Contexto()) await InicializadorBaseDatos.MigrarAsync(db, NullLogger.Instance);

        using var lectura = Contexto();
        Assert.Equal(lectura.Database.GetMigrations().Count(), (await lectura.Database.GetAppliedMigrationsAsync()).Count());
    }

    [Fact]
    public async Task Las_fechas_locales_y_utc_se_guardan_y_leen_sin_corrimiento()
    {
        using (var db = Contexto()) await InicializadorBaseDatos.MigrarAsync(db, NullLogger.Instance);

        var local = new DateTime(2026, 11, 3, 21, 30, 0, DateTimeKind.Local);
        var utc = new DateTime(2026, 11, 4, 0, 30, 0, DateTimeKind.Utc);
        using (var db = Contexto())
        {
            db.Socios.Add(new Socio { Nombre = "Ana", Apellido = "Local", Dni = "1", FechaAlta = local });
            db.Socios.Add(new Socio { Nombre = "Beto", Apellido = "Utc", Dni = "2", FechaAlta = utc });
            await db.SaveChangesAsync();   // con Npgsql, sin el conversor esto falla (Kind=Local en timestamptz)
        }

        using var lectura = Contexto();
        var a = await lectura.Socios.SingleAsync(s => s.Dni == "1");
        var b = await lectura.Socios.SingleAsync(s => s.Dni == "2");
        // Misma "hora de reloj" que se guardó, igual que con SQLite
        Assert.Equal(new DateTime(2026, 11, 3, 21, 30, 0), a.FechaAlta);
        Assert.Equal(new DateTime(2026, 11, 4, 0, 30, 0), b.FechaAlta);
        // Los filtros por fecha con parámetros Local también funcionan
        Assert.Equal(1, await lectura.Socios.CountAsync(s => s.FechaAlta < new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Local)));
    }
}
