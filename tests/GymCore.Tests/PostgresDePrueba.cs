using GymCore.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GymCore.Tests;

// PostgreSQL para las pruebas (mismo motor que producción).
//
// Servidor: el de desarrollo (docker-compose.dev.yml) o el que indique la variable GYMCORE_PRUEBAS_POSTGRES
// (el CI la define). Al iniciar la corrida se crea una base "plantilla" con todas las migraciones aplicadas;
// cada prueba recibe su propia base copiada de esa plantilla (CREATE DATABASE ... TEMPLATE), que es rápido
// y deja cada prueba aislada de las demás. Las bases se borran al terminar cada prueba.
public static class PostgresDePrueba
{
    public const string Prefijo = "gymcore_test_";
    private const string Plantilla = Prefijo + "plantilla";
    private const string PorDefecto = "Host=localhost;Port=5432;Username=gymcore;Password=gymcore_dev;Database=postgres";

    private static readonly object Candado = new();
    private static readonly Lazy<string> Servidor = new(Iniciar, LazyThreadSafetyMode.ExecutionAndPublication);

    // Base nueva con el esquema al día. Devuelve su cadena de conexión.
    public static string NuevaBase() => CrearBase(conPlantilla: true);

    // Base nueva y vacía, sin tablas (para probar las migraciones desde cero).
    public static string BaseVacia() => CrearBase(conPlantilla: false);

    public static void Borrar(string cadena)
    {
        var nombre = new NpgsqlConnectionStringBuilder(cadena).Database;
        try
        {
            using (var c = new NpgsqlConnection(cadena)) NpgsqlConnection.ClearPool(c);
            Ejecutar(Servidor.Value, $"DROP DATABASE IF EXISTS \"{nombre}\" WITH (FORCE)");
        }
        catch { /* limpieza best-effort: lo que quede se borra al iniciar la próxima corrida */ }
    }

    private static string CrearBase(bool conPlantilla)
    {
        var servidor = Servidor.Value;
        var nombre = $"{Prefijo}{Guid.NewGuid():N}";
        lock (Candado)   // CREATE DATABASE ... TEMPLATE no admite copias simultáneas de la misma plantilla
            Ejecutar(servidor, conPlantilla ? $"CREATE DATABASE \"{nombre}\" TEMPLATE \"{Plantilla}\"" : $"CREATE DATABASE \"{nombre}\"");
        return Cadena(servidor, nombre);
    }

    private static string Iniciar()
    {
        var servidor = Environment.GetEnvironmentVariable("GYMCORE_PRUEBAS_POSTGRES") is { Length: > 0 } v ? v : PorDefecto;
        try
        {
            // Bases que hayan quedado de corridas anteriores (y la plantilla vieja)
            foreach (var vieja in Consultar(servidor, $"SELECT datname FROM pg_database WHERE datname LIKE '{Prefijo}%'"))
                Ejecutar(servidor, $"DROP DATABASE IF EXISTS \"{vieja}\" WITH (FORCE)");
        }
        catch (NpgsqlException ex)
        {
            throw new InvalidOperationException(
                "No se pudo conectar a PostgreSQL para las pruebas. Levantá la base de desarrollo con " +
                "'docker compose -f docker-compose.dev.yml up -d' o definí GYMCORE_PRUEBAS_POSTGRES.", ex);
        }

        Ejecutar(servidor, $"CREATE DATABASE \"{Plantilla}\"");
        var cadena = Cadena(servidor, Plantilla);
        using (var db = new GymDbContext(new DbContextOptionsBuilder<GymDbContext>().UseNpgsql(cadena).Options))
            db.Database.Migrate();

        // Una plantilla no puede tener conexiones abiertas mientras se copia
        using (var c = new NpgsqlConnection(cadena)) NpgsqlConnection.ClearPool(c);
        return servidor;
    }

    // Misma zona horaria de sesión que la aplicación (Program.cs)
    private static string Cadena(string servidor, string baseDeDatos) =>
        new NpgsqlConnectionStringBuilder(servidor) { Database = baseDeDatos, Timezone = "America/Argentina/Buenos_Aires" }.ConnectionString;

    private static void Ejecutar(string cadena, string sql)
    {
        using var c = new NpgsqlConnection(cadena);
        c.Open();
        using var cmd = new NpgsqlCommand(sql, c);
        cmd.ExecuteNonQuery();
    }

    private static List<string> Consultar(string cadena, string sql)
    {
        using var c = new NpgsqlConnection(cadena);
        c.Open();
        using var cmd = new NpgsqlCommand(sql, c);
        using var r = cmd.ExecuteReader();
        var filas = new List<string>();
        while (r.Read()) filas.Add(r.GetString(0));
        return filas;
    }
}
