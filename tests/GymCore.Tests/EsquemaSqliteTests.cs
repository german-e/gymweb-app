using GymCore.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Tests;

// Compatibilidad con bases existentes: la actualización agrega columnas y tablas sin perder datos.
public class EsquemaSqliteTests
{
    [Fact]
    public async Task Base_anterior_al_sprint_se_actualiza_y_conserva_los_pagos()
    {
        using var b = new BaseDePrueba();
        var (socio, sp) = await b.SocioConPlanAsync();
        await b.PagoAsync(socio.Id, sp.Id, 250);

        // Simula la base previa: sin columnas de anulación ni tabla de auditoría
        using (var db = b.Contexto())
        {
            foreach (var col in new[] { "Anulado", "FechaAnulacion", "MotivoAnulacion", "UsuarioAnulo" })
                await db.Database.ExecuteSqlRawAsync($"ALTER TABLE \"Pagos\" DROP COLUMN \"{col}\";");
            await db.Database.ExecuteSqlRawAsync("DROP TABLE \"EventosSistema\";");
        }

        using (var db = b.Contexto())
            await EsquemaSqlite.ActualizarAsync(db);

        using var lectura = b.Contexto();
        var pago = await lectura.Pagos.SingleAsync();          // el pago previo sigue contando (Anulado = 0)
        Assert.False(pago.Anulado);
        Assert.Equal(250, pago.Importe);
        Assert.Equal(0, await lectura.EventosSistema.CountAsync()); // tabla recreada

        using var cmd = b.Conexion.CreateCommand();
        cmd.CommandText = "SELECT \"notnull\", \"dflt_value\" FROM pragma_table_info('Pagos') WHERE name = 'Anulado'";
        using var r = await cmd.ExecuteReaderAsync();
        Assert.True(await r.ReadAsync());
        Assert.Equal(1L, r.GetInt64(0));
        Assert.Equal("0", r.GetString(1));
    }
}
