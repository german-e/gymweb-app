using GymCore.Dominio.Entidades;
using GymCore.Infraestructura.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GymCore.Tests;

// Migraciones de EF Core: el modelo y las migraciones coinciden, una base nueva se crea desde cero
// y una base heredada (creada con EnsureCreated) se adopta sin perder datos.
public sealed class MigracionesTests : IDisposable
{
    private readonly string _ruta = Path.Combine(Path.GetTempPath(), $"gymcore-migr-{Guid.NewGuid():N}.db");

    private GymDbContext Contexto() =>
        new(new DbContextOptionsBuilder<GymDbContext>().UseSqlite($"Data Source={_ruta}").Options);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { File.Delete(_ruta); } catch { }
    }

    [Fact]
    public void El_proyecto_tiene_migraciones_y_el_modelo_no_tiene_cambios_sin_migrar()
    {
        using var db = Contexto();
        Assert.NotEmpty(db.Database.GetMigrations());
        // Si falla: se cambió una entidad o su configuración sin generar la migración (ver docs/migraciones.md)
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Una_base_nueva_se_crea_aplicando_todas_las_migraciones()
    {
        using (var db = Contexto())
            await InicializadorBaseDatos.MigrarAsync(db, NullLogger.Instance);

        using var lectura = Contexto();
        Assert.Empty(await lectura.Database.GetPendingMigrationsAsync());
        Assert.Equal(lectura.Database.GetMigrations(), await lectura.Database.GetAppliedMigrationsAsync());
        Assert.Equal(0, await lectura.Socios.CountAsync());
    }

    [Fact]
    public async Task Una_base_creada_con_EnsureCreated_se_adopta_sin_perder_datos()
    {
        // Base "vieja": como la creaba la aplicación antes de las migraciones
        using (var db = Contexto())
        {
            await db.Database.EnsureCreatedAsync();
            db.Socios.Add(new Socio { Nombre = "Ana", Apellido = "Heredada", Dni = "30111222" });
            await db.SaveChangesAsync();
        }
        using (var db = Contexto())
            Assert.True(await InicializadorBaseDatos.EsBaseHeredadaAsync(db));

        using (var db = Contexto())
            await InicializadorBaseDatos.MigrarAsync(db, NullLogger.Instance);

        using var lectura = Contexto();
        Assert.False(await InicializadorBaseDatos.EsBaseHeredadaAsync(lectura));
        Assert.Empty(await lectura.Database.GetPendingMigrationsAsync());
        var socio = await lectura.Socios.SingleAsync();
        Assert.Equal("Heredada", socio.Apellido);
        Assert.Equal("30111222", socio.TarjetaCodigo);   // credencial completada con el DNI, como antes
    }

    [Fact]
    public async Task Arrancar_dos_veces_no_vuelve_a_aplicar_nada()
    {
        using (var db = Contexto()) await InicializadorBaseDatos.MigrarAsync(db, NullLogger.Instance);
        using (var db = Contexto()) await InicializadorBaseDatos.MigrarAsync(db, NullLogger.Instance);

        using var lectura = Contexto();
        Assert.Equal(lectura.Database.GetMigrations().Count(), (await lectura.Database.GetAppliedMigrationsAsync()).Count());
    }
}
