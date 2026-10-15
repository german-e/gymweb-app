using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Componentes;
using GymCore.Web.Pages.Socios;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Comercial;

// Cobro de cuotas. Una sola implementación para "Registrar pago" y para la cuota incluida en un ticket del POS.
public class ServicioCobranza
{
    private readonly GymDbContext _db;
    private readonly IUsuarioActual _usuario;
    public ServicioCobranza(GymDbContext db, IUsuarioActual usuario) { _db = db; _usuario = usuario; }

    public record SaldoCuota(SocioPlan Suscripcion, decimal Precio, decimal Pagado)
    {
        public decimal Faltante => Math.Max(0m, Precio - Pagado);
    }

    // Suscripción activa y vigente hoy (la de vencimiento más lejano si hubiera varias)
    public static SocioPlan? SuscripcionVigente(Socio socio, DateTime ahora) => socio.Suscripciones
        .Where(sp => sp.Estado == EstadoSuscripcion.Activo && sp.FechaInicio <= ahora && sp.FechaFin >= ahora)
        .OrderByDescending(sp => sp.FechaFin)
        .FirstOrDefault();

    public Task<Socio?> SocioConSuscripcionesAsync(int socioId) => _db.Socios
        .Include(s => s.Suscripciones).ThenInclude(sp => sp.Plan)
        .FirstOrDefaultAsync(s => s.Id == socioId);

    // Saldo de la cuota vigente (null: sin suscripción vigente). Los pagos anulados no cuentan (filtro global).
    public async Task<SaldoCuota?> SaldoAsync(Socio socio)
    {
        var activa = SuscripcionVigente(socio, DateTime.Now);
        if (activa is null) return null;
        var pagado = await _db.Pagos
            .Where(p => p.SocioId == socio.Id && p.SocioPlanId == activa.Id)
            .SumAsync(p => (decimal?)p.Importe) ?? 0m;
        return new SaldoCuota(activa, activa.Plan.Precio, pagado);
    }

    // Agrega el pago y su evento de auditoría al contexto (lo guarda quien llama, en su misma operación)
    public Pago CrearPago(Socio socio, decimal importe, MedioPago medio, string? moneda = null,
        string? referenciaExterna = null, string? observaciones = null, Venta? venta = null)
    {
        var activa = SuscripcionVigente(socio, DateTime.Now);
        var pago = new Pago
        {
            SocioId = socio.Id,
            SocioPlanId = activa?.Id,
            Importe = importe,
            Moneda = string.IsNullOrWhiteSpace(moneda) ? "ARS" : moneda.Trim().ToUpperInvariant(),
            Medio = medio,
            ReferenciaExterna = referenciaExterna,
            Observaciones = observaciones,
            FechaPago = DateTime.UtcNow,
            UsuarioCajero = _usuario.Nombre,
            Venta = venta
        };
        _db.Pagos.Add(pago);
        _db.Auditar(TipoEventoSistema.PagoRegistrado, EntidadAuditada.Pago, null, $"{socio.Apellido}, {socio.Nombre}",
            $"{SocioPresentacion.Dinero(pago.Importe, pago.Moneda)} · {pago.Medio.Texto()} · {(activa is null ? "Pago a cuenta" : $"Cuota {activa.Plan.Nombre}")}{(venta is null ? "" : " · Punto de Venta")}",
            pago.Observaciones, socio.Id, referencia: pago);
        return pago;
    }
}
