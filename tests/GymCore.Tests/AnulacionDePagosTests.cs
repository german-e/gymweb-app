using GymCore.Dominio.Entidades;
using GymCore.Web.Componentes;
using GymCore.Web.Pages.Socios;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Tests;

// Historia 1: anulación lógica. El pago nunca se borra y deja de impactar en saldos e indicadores.
public class AnulacionDePagosTests
{
    [Fact]
    public void Anular_completa_fecha_motivo_y_responsable()
    {
        var p = new Pago { Importe = 500 };
        var cuando = new DateTime(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc);

        p.Anular("  Error de importe  ", "Germán", cuando);

        Assert.True(p.Anulado);
        Assert.Equal(cuando, p.FechaAnulacion);
        Assert.Equal("Error de importe", p.MotivoAnulacion);
        Assert.Equal("Germán", p.UsuarioAnulo);
        Assert.Equal(500, p.Importe); // el importe original se conserva
    }

    [Fact]
    public void Anular_dos_veces_no_esta_permitido()
    {
        var p = new Pago();
        p.Anular("Cobro duplicado", "Germán", DateTime.UtcNow);
        Assert.Throws<InvalidOperationException>(() => p.Anular("Otro motivo", "Otro", DateTime.UtcNow));
        Assert.Equal("Cobro duplicado", p.MotivoAnulacion);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Anular_exige_motivo(string motivo)
    {
        var p = new Pago();
        Assert.Throws<ArgumentException>(() => p.Anular(motivo, "Germán", DateTime.UtcNow));
        Assert.False(p.Anulado);
    }

    [Fact]
    public void Anular_recorta_motivos_demasiado_largos()
    {
        var p = new Pago();
        p.Anular(new string('x', 1000), "Germán", DateTime.UtcNow);
        Assert.Equal(Pago.MotivoMaxLength, p.MotivoAnulacion!.Length);
    }

    [Fact]
    public async Task Pago_anulado_no_se_borra_y_queda_fuera_de_las_consultas_por_defecto()
    {
        using var b = new BaseDePrueba();
        var (socio, sp) = await b.SocioConPlanAsync();
        var vigente = await b.PagoAsync(socio.Id, sp.Id, 300);
        var anulado = await b.PagoAsync(socio.Id, sp.Id, 700);
        await b.AnularAsync(anulado.Id);

        using var db = b.Contexto();
        Assert.Equal(new[] { vigente.Id }, await db.Pagos.Select(p => p.Id).ToListAsync());
        var todos = await db.Pagos.IgnoreQueryFilters().OrderBy(p => p.Id).ToListAsync();
        Assert.Equal(2, todos.Count);
        Assert.True(todos[1].Anulado);
        Assert.Equal("Cajero Test", todos[1].UsuarioAnulo);
    }

    [Fact]
    public async Task Anular_el_pago_vuelve_a_dejar_saldo_pendiente()
    {
        using var b = new BaseDePrueba();
        var (socio, sp) = await b.SocioConPlanAsync(precio: 1000);
        var pago = await b.PagoAsync(socio.Id, sp.Id, 1000);

        using (var db = b.Contexto())
            Assert.False((await SociosConsultas.ResumenAsync(db, new[] { socio.Id }))[socio.Id].Moroso);

        await b.AnularAsync(pago.Id);

        using (var db = b.Contexto())
        {
            var r = (await SociosConsultas.ResumenAsync(db, new[] { socio.Id }))[socio.Id];
            Assert.True(r.Moroso);
            Assert.Equal(1000, r.Saldo);
        }
    }

    [Fact]
    public async Task La_recaudacion_no_suma_pagos_anulados()
    {
        using var b = new BaseDePrueba();
        var (socio, sp) = await b.SocioConPlanAsync();
        await b.PagoAsync(socio.Id, sp.Id, 400);
        var anulado = await b.PagoAsync(socio.Id, sp.Id, 600);
        await b.AnularAsync(anulado.Id);

        using var db = b.Contexto();
        var c = await IndicadoresGimnasio.CobranzaAsync(db);
        Assert.Equal(400, c.Hoy);
        Assert.Equal(1, c.PagosHoy);
        Assert.Equal(400, c.Mes);
        Assert.Equal(400, (await IndicadoresGimnasio.RecaudacionMensualAsync(db, 1)).Single().Total);
    }
}
