using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GymCore.Infraestructura.Persistencia;

// EnsureCreated no actualiza una base existente. Este helper la alinea con el modelo de forma
// idempotente y sin migraciones:
//   1. agrega columnas NULLABLES nuevas a tablas existentes (ALTER TABLE ADD COLUMN),
//   2. crea las tablas e índices que falten (a partir del script que genera EF).
// No elimina ni modifica columnas existentes.
public static class EsquemaSqlite
{
    public static async Task ActualizarAsync(GymDbContext db)
    {
        await AgregarColumnasFaltantesAsync(db);
        await CompletarDatosAsync(db);
        await CrearTablasFaltantesAsync(db);
    }

    public static async Task CrearTablasFaltantesAsync(GymDbContext db)
    {
        var script = db.Database.GenerateCreateScript()
            .Replace("CREATE TABLE \"", "CREATE TABLE IF NOT EXISTS \"")
            .Replace("CREATE UNIQUE INDEX \"", "CREATE UNIQUE INDEX IF NOT EXISTS \"")
            .Replace("CREATE INDEX \"", "CREATE INDEX IF NOT EXISTS \"");

        var sentencias = script.Split(";\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.TrimEnd(';').Trim())
            .Where(s => s.Length > 0);

        foreach (var sql in sentencias)
        {
            // best-effort por sentencia: un índice sobre una columna faltante no debe frenar al resto
            try { await db.Database.ExecuteSqlRawAsync(sql); } catch { }
        }
    }

    private static async Task AgregarColumnasFaltantesAsync(GymDbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

        foreach (var entidad in db.Model.GetEntityTypes())
        {
            var tabla = entidad.GetTableName();
            if (tabla is null) continue;
            var objeto = StoreObjectIdentifier.Table(tabla, entidad.GetSchema());

            var existentes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"PRAGMA table_info('{tabla}')";
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) existentes.Add(r.GetString(1));
            }
            if (existentes.Count == 0) continue; // tabla nueva: la crea CrearTablasFaltantesAsync

            foreach (var prop in entidad.GetProperties())
            {
                var columna = prop.GetColumnName(objeto);
                if (columna is null || existentes.Contains(columna) || !prop.IsColumnNullable(objeto)) continue;
                var tipo = prop.GetColumnType(objeto);
                try { await db.Database.ExecuteSqlRawAsync($"ALTER TABLE \"{tabla}\" ADD COLUMN \"{columna}\" {tipo} NULL;"); } catch { }
            }
        }
    }

    private static async Task CompletarDatosAsync(GymDbContext db)
    {
        // Número de socio para altas anteriores a la columna
        try { await db.Database.ExecuteSqlRawAsync("UPDATE \"Socios\" SET \"NumeroSocio\" = \"Id\" WHERE \"NumeroSocio\" IS NULL;"); } catch { }
    }
}
