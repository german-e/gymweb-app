using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
using GymCore.Web.Pages.Entrenadores;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Tests;

// Historia 2: los eventos se completan solos (usuario, Id de entidades nuevas) en el mismo SaveChanges.
public class AuditoriaTests
{
    [Fact]
    public async Task El_evento_toma_el_usuario_del_operador_actual()
    {
        using var b = new BaseDePrueba();
        using (var db = b.Contexto("Germán"))
        {
            db.Auditar(TipoEventoSistema.PlanActivado, EntidadAuditada.Plan, 1, "Plan X");
            await db.SaveChangesAsync();
        }
        using var lectura = b.Contexto();
        Assert.Equal("Germán", (await lectura.EventosSistema.SingleAsync()).Usuario);
    }

    [Fact]
    public async Task Sin_operador_el_responsable_es_Sistema()
    {
        using var b = new BaseDePrueba();
        using (var db = b.Contexto(operador: null))
        {
            db.Auditar(TipoEventoSistema.PlanActivado, EntidadAuditada.Plan, 1, "Plan X");
            await db.SaveChangesAsync();
        }
        using var lectura = b.Contexto();
        Assert.Equal(GymDbContext.UsuarioSistema, (await lectura.EventosSistema.SingleAsync()).Usuario);
    }

    [Fact]
    public async Task Evento_de_entidad_nueva_recibe_su_Id_al_guardar()
    {
        using var b = new BaseDePrueba();
        var plan = new Plan { Nombre = "Nuevo", Tipo = TipoPlan.PaseDiario, Precio = 10, DuracionDias = 1 };
        var socio = new Socio { Nombre = "Leo", Apellido = "Nuevo", Dni = "123" };
        using (var db = b.Contexto())
        {
            db.Planes.Add(plan);
            db.Socios.Add(socio);
            db.Auditar(TipoEventoSistema.PlanCreado, EntidadAuditada.Plan, null, plan.Nombre, referencia: plan);
            db.Auditar(TipoEventoSistema.SocioCreado, EntidadAuditada.Socio, null, "Nuevo, Leo", referencia: socio);
            await db.SaveChangesAsync();
        }

        using var lectura = b.Contexto();
        var eventos = await lectura.EventosSistema.OrderBy(e => e.Id).ToListAsync();
        Assert.Equal(plan.Id, eventos[0].EntidadId);
        Assert.Equal(socio.Id, eventos[1].EntidadId);
        Assert.Equal(socio.Id, eventos[1].SocioId); // el alta aparece en el historial del socio
    }

    [Fact]
    public async Task Historial_de_entrenador_se_refleja_en_la_auditoria_con_responsable()
    {
        using var b = new BaseDePrueba();
        var e = new Entrenador { Nombre = "Romina", Apellido = "Cepeda", Dni = "999" };
        using (var db = b.Contexto("Germán"))
        {
            db.Entrenadores.Add(e);
            db.RegistrarEvento(e, TipoEventoEntrenador.Alta, "Alta del entrenador");
            await db.SaveChangesAsync();
        }

        using var lectura = b.Contexto();
        var propio = await lectura.EntrenadorEventos.SingleAsync();
        var sistema = await lectura.EventosSistema.SingleAsync();
        Assert.Equal("Germán", propio.Usuario);
        Assert.Equal(TipoEventoSistema.EntrenadorCreado, sistema.Tipo);
        Assert.Equal(e.Id, sistema.EntidadId);
        Assert.Equal("Germán", sistema.Usuario);
    }

    [Fact]
    public async Task Los_campos_modificados_se_detectan_con_etiquetas_legibles()
    {
        using var b = new BaseDePrueba();
        var (_, sp) = await b.SocioConPlanAsync(precio: 1000);
        using var db = b.Contexto();
        var plan = await db.Planes.FirstAsync(p => p.Id == sp.PlanId);
        plan.Precio = 1500;
        plan.Nombre = plan.Nombre; // sin cambio real: no se informa

        Assert.Equal(new[] { "precio" }, db.CamposModificados(plan, Auditoria.CamposPlan));
    }
}
