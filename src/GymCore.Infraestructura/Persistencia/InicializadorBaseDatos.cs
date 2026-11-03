using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace GymCore.Infraestructura.Persistencia;

// Deja la base al día con las migraciones de EF Core al arrancar la aplicación.
//
// Tres casos:
//   1. Base nueva (o vacía): se aplican todas las migraciones.
//   2. Base "heredada", creada antes con EnsureCreated (tiene tablas pero no historial de migraciones):
//      se completa el esquema con EsquemaSqlite, se registra la migración inicial como aplicada
//      y se aplican las posteriores. No se pierden datos.
//   3. Proyecto todavía sin migraciones generadas: se usa EnsureCreated como antes, con una advertencia.
//
// Si una migración falla, la excepción se propaga y la aplicación NO arranca: es preferible a arrancar
// con un esquema a medio actualizar.
public static class InicializadorBaseDatos
{
    public static async Task MigrarAsync(GymDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var migraciones = db.Database.GetMigrations().ToList();

        if (migraciones.Count == 0)
        {
            logger.LogWarning("El proyecto no tiene migraciones: se crea el esquema con EnsureCreated. " +
                              "Generá la migración inicial (ver docs/migraciones.md).");
            await db.Database.EnsureCreatedAsync(ct);
            await EsquemaSqlite.ActualizarAsync(db);
            return;
        }

        if (await EsBaseHeredadaAsync(db, ct))
        {
            var inicial = migraciones[0];
            logger.LogWarning("Base creada sin migraciones: se completa el esquema y se registra {Migracion} como aplicada.", inicial);

            await EsquemaSqlite.ActualizarAsync(db);
            var historial = db.GetService<IHistoryRepository>();
            await db.Database.ExecuteSqlRawAsync(historial.GetCreateIfNotExistsScript(), ct);
            await db.Database.ExecuteSqlRawAsync(historial.GetInsertScript(new HistoryRow(inicial, ProductInfo.GetVersion())), ct);
        }

        var pendientes = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pendientes.Count > 0)
            logger.LogInformation("Aplicando {Cantidad} migraciones: {Migraciones}", pendientes.Count, string.Join(", ", pendientes));

        await db.Database.MigrateAsync(ct);
    }

    // Tiene tablas pero no tiene la tabla de historial de migraciones
    public static async Task<bool> EsBaseHeredadaAsync(GymDbContext db, CancellationToken ct = default)
    {
        var creador = db.GetService<IRelationalDatabaseCreator>();
        if (!await creador.ExistsAsync(ct) || !await creador.HasTablesAsync(ct)) return false;
        return !await db.GetService<IHistoryRepository>().ExistsAsync(ct);
    }
}
