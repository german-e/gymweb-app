using GymCore.Dominio.Enums;

namespace GymCore.Web.Pages.Socios;

public static class SocioPresentacion
{
    public static string Numero(int n) => n.ToString("D5");

    public static string Texto(this EstadoMembresia e) => e switch
    {
        EstadoMembresia.Vigente => "Activa",
        EstadoMembresia.PorVencer => "Por vencer",
        EstadoMembresia.Pausada => "Pausada",
        EstadoMembresia.Vencida => "Vencida",
        EstadoMembresia.SinPlan => "Sin plan",
        EstadoMembresia.Baja => "Baja",
        _ => "—"
    };

    // Variantes de status-pill / module-icon (components.css)
    public static string Variante(this EstadoMembresia e) => e switch
    {
        EstadoMembresia.Vigente => "success",
        EstadoMembresia.PorVencer => "warning",
        EstadoMembresia.Pausada => "info",
        EstadoMembresia.Vencida => "danger",
        _ => "muted"
    };

    // Clave para filtros del listado
    public static string Clave(this EstadoMembresia e) => e switch
    {
        EstadoMembresia.Vigente or EstadoMembresia.PorVencer => "activo",
        EstadoMembresia.Baja => "baja",
        _ => "inactivo"
    };

    public static string Texto(this MedioPago m) => m switch
    {
        MedioPago.Efectivo => "Efectivo",
        MedioPago.TarjetaDebito => "Tarjeta de débito",
        MedioPago.TarjetaCredito => "Tarjeta de crédito",
        MedioPago.Transferencia => "Transferencia",
        MedioPago.MercadoPago => "Mercado Pago",
        _ => m.ToString()
    };

    public static string Icono(this MedioPago m) => m switch
    {
        MedioPago.Efectivo => "bi-cash",
        MedioPago.TarjetaDebito or MedioPago.TarjetaCredito => "bi-credit-card",
        MedioPago.Transferencia => "bi-bank",
        MedioPago.MercadoPago => "bi-phone",
        _ => "bi-wallet2"
    };

    public static string Dinero(decimal importe, string moneda = "ARS") =>
        $"$ {importe.ToString("N0", new System.Globalization.CultureInfo("es-AR"))}" + (moneda == "ARS" ? "" : $" {moneda}");

    public static string Relativo(DateTime? utc)
    {
        if (utc is null) return "Sin registros";
        var local = utc.Value.ToLocalTime();
        var dias = (DateTime.Today - local.Date).Days;
        return dias switch
        {
            0 => $"Hoy, {local:HH:mm}",
            1 => $"Ayer, {local:HH:mm}",
            < 7 => $"Hace {dias} días",
            _ => local.ToString("dd/MM/yyyy")
        };
    }

    // "vence en 3 días", "venció hace 2 días"
    public static string Vencimiento(DateTime? fechaFin)
    {
        if (fechaFin is null) return "—";
        var dias = (fechaFin.Value.Date - DateTime.Today).Days;
        return dias switch
        {
            0 => "Vence hoy",
            1 => "Vence mañana",
            > 1 => $"En {dias} días",
            -1 => "Venció ayer",
            _ => $"Venció hace {-dias} días"
        };
    }

    // Normaliza un teléfono argentino para wa.me (solo dígitos; agrega 549 si parece local)
    public static string? WhatsApp(string? tel)
    {
        if (string.IsNullOrWhiteSpace(tel)) return null;
        var d = new string(tel.Where(char.IsDigit).ToArray());
        if (d.Length < 8) return null;
        if (d.StartsWith("54")) return d;
        if (d.StartsWith("0")) d = d[1..];
        return "549" + d;
    }
}

public static class CredencialFactory
{
    // Activa = tarjeta azul de marca con indicador verde; por vencer = naranja; vencida = roja; resto = gris.
    public static GymCore.Web.Componentes.CredencialVm Desde(ResumenSocio r, bool mini = false, string? id = null)
    {
        var s = r.Socio;
        var variante = r.Estado switch
        {
            EstadoMembresia.Vigente => "",
            EstadoMembresia.PorVencer => "warning",
            EstadoMembresia.Vencida => "danger",
            _ => "muted"
        };
        var estado = r.Estado == EstadoMembresia.Vigente ? "Activo" : r.Estado.Texto();
        return new(s.NombreCompleto, s.FotoUrl, s.Iniciales, SocioPresentacion.Numero(s.NumeroVisible), s.Dni,
            r.PlanNombre, r.Vencimiento, estado, variante, s.CodigoCredencial, mini, id);
    }
}
