using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GymCore.Infraestructura.Persistencia;

// Deja la base al día con las migraciones de EF Core al arrancar la aplicación.
// Si una migración falla, la excepción se propaga y la aplicación NO arranca: es preferible a operar
// con un esquema a medio actualizar. Las migraciones de EF Core en PostgreSQL usan un bloqueo propio,
// así que dos instancias arrancando a la vez no aplican la misma migración dos veces.
public static class InicializadorBaseDatos
{
    public static async Task MigrarAsync(GymDbContext db, ILogger logger, CancellationToken ct = default)
    {
        if (!db.Database.GetMigrations().Any())
            throw new InvalidOperationException(
                "El proyecto no tiene migraciones. Generá la migración inicial (ver docs/migraciones.md).");

        var pendientes = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pendientes.Count > 0)
            logger.LogInformation("Aplicando {Cantidad} migraciones: {Migraciones}", pendientes.Count, string.Join(", ", pendientes));

        await db.Database.MigrateAsync(ct);
    }
}
