using GymCore.Dominio.Entidades;
using GymCore.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Pagos;

// Estados visuales de cobranza. Se derivan de los datos existentes (no hay estado persistido en Pago):
//  - Pagado:    pago a cuenta, o pago de un período cuyo total abonado cubre el precio del plan
//  - Parcial:   pago de un período que todavía tiene saldo
//  - Pendiente: suscripción vigente con saldo (Pagos/Pendientes)
//  - Vencido:   membresía vencida sin renovar
//  - Anulado:   pago anulado (Pago.Anulado). No suma en ningún total.
public enum EstadoCobro { Pagado, Parcial, Pendiente, Vencido, Anulado }

public static class PagoPresentacion
{
    public static string Texto(this EstadoCobro e) => e switch
    {
        EstadoCobro.Pagado => "Pagado",
        EstadoCobro.Parcial => "Pago parcial",
        EstadoCobro.Pendiente => "Pendiente",
        EstadoCobro.Vencido => "Vencido",
        EstadoCobro.Anulado => "Anulado",
        _ => "—"
    };

    // Variantes de status-pill / timeline-dot (components.css)
    public static string Variante(this EstadoCobro e) => e switch
    {
        EstadoCobro.Pagado => "success",
        EstadoCobro.Parcial => "info",
        EstadoCobro.Pendiente => "warning",
        EstadoCobro.Vencido => "danger",
        EstadoCobro.Anulado => "void",
        _ => "muted"
    };

    public static string Numero(this Pago p) => $"R-{p.Id:D6}";

    public static string Concepto(this Pago p) => p.SocioPlan?.Plan is { } plan ? $"Cuota {plan.Nombre}" : "Pago a cuenta";

    public static EstadoCobro Estado(this Pago p, IReadOnlyDictionary<int, decimal> pagadoPorPeriodo)
    {
        if (p.Anulado) return EstadoCobro.Anulado;
        if (p.SocioPlanId is not { } spId || p.SocioPlan?.Plan is not { } plan) return EstadoCobro.Pagado;
        return pagadoPorPeriodo.GetValueOrDefault(spId) >= plan.Precio ? EstadoCobro.Pagado : EstadoCobro.Parcial;
    }

    // Motivos sugeridos en el modal de anulación ("Otro" exige detalle)
    public static readonly string[] MotivosAnulacion = { "Cobro duplicado", "Error de importe", "Pago registrado por error", "Otro" };

    // Total abonado por suscripción (sin anulados: filtro global). Suma en memoria: SQLite no agrega decimal del lado del servidor.
    public static async Task<Dictionary<int, decimal>> PagadoPorPeriodoAsync(GymDbContext db, IEnumerable<Pago> pagos)
    {
        var ids = pagos.Where(p => p.SocioPlanId != null).Select(p => p.SocioPlanId!.Value).Distinct().ToList();
        if (ids.Count == 0) return new();
        return (await db.Pagos.AsNoTracking()
                .Where(p => p.SocioPlanId != null && ids.Contains(p.SocioPlanId.Value))
                .Select(p => new { Id = p.SocioPlanId!.Value, p.Importe })
                .ToListAsync())
            .GroupBy(p => p.Id)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Importe));
    }
}
