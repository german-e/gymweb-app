using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Mvc;

namespace GymCore.Web.Componentes;

// Registro de eventos auditables. El evento se agrega al mismo SaveChanges que la acción:
// el DbContext completa el usuario y, si la entidad es nueva (referencia), su Id.
public static class Auditoria
{
    public static EventoSistema Auditar(this GymDbContext db, TipoEventoSistema tipo, EntidadAuditada entidad,
        int? entidadId, string entidadNombre, string? resumen = null, string? detalle = null,
        int? socioId = null, object? referencia = null)
    {
        var ev = new EventoSistema
        {
            FechaUtc = DateTime.UtcNow,
            Tipo = tipo,
            Entidad = entidad,
            EntidadId = entidadId is > 0 ? entidadId : null,
            EntidadNombre = Recortar(entidadNombre, 200) ?? "",
            Resumen = Recortar(resumen, 500),
            Detalle = Recortar(detalle, 4000),
            SocioId = socioId,
            Referencia = entidadId is > 0 ? null : referencia
        };
        db.EventosSistema.Add(ev);
        return ev;
    }

    // Etiquetas legibles de las propiedades modificadas (solo las listadas). Llamar antes de SaveChanges.
    public static List<string> CamposModificados(this GymDbContext db, object entidad, IReadOnlyDictionary<string, string> etiquetas)
    {
        db.ChangeTracker.DetectChanges();
        return db.Entry(entidad).Properties
            .Where(p => p.IsModified && etiquetas.ContainsKey(p.Metadata.Name))
            .Select(p => etiquetas[p.Metadata.Name])
            .Distinct()
            .ToList();
    }

    private static string? Recortar(string? s, int max) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Length > max ? s[..max] : s;

    public static readonly IReadOnlyDictionary<string, string> CamposSocio = new Dictionary<string, string>
    {
        ["Nombre"] = "nombre", ["Apellido"] = "apellido", ["Dni"] = "DNI", ["NumeroSocio"] = "N° de socio",
        ["FechaNacimiento"] = "fecha de nacimiento", ["Sexo"] = "sexo", ["Telefono"] = "teléfono", ["Email"] = "email",
        ["Direccion"] = "dirección", ["ContactoEmergenciaNombre"] = "contacto de emergencia",
        ["ContactoEmergenciaTelefono"] = "teléfono de emergencia", ["FotoUrl"] = "foto", ["FechaAlta"] = "fecha de alta"
    };

    public static readonly IReadOnlyDictionary<string, string> CamposProducto = new Dictionary<string, string>
    {
        ["Codigo"] = "código", ["CodigoBarras"] = "código de barras", ["Nombre"] = "nombre", ["CategoriaId"] = "categoría",
        ["Marca"] = "marca", ["Descripcion"] = "descripción", ["PrecioCompra"] = "precio de compra",
        ["PrecioVenta"] = "precio de venta", ["StockMinimo"] = "stock mínimo", ["Destacado"] = "acceso rápido"
    };

    public static readonly IReadOnlyDictionary<string, string> CamposPlan = new Dictionary<string, string>
    {
        ["Nombre"] = "nombre", ["Descripcion"] = "descripción", ["Tipo"] = "tipo", ["Precio"] = "precio",
        ["Moneda"] = "moneda", ["DuracionDias"] = "duración", ["MaxDiasPorSemana"] = "días por semana",
        ["PermiteAccesoSabado"] = "acceso sábados", ["PermiteAccesoDomingo"] = "acceso domingos"
    };
}

// Texto, ícono, color y destino de cada tipo de evento (timeline de auditoría, actividad e historiales).
public static class EventoPresentacion
{
    public static (string Texto, string Icono, string Variante) Estilo(this TipoEventoSistema t) => t switch
    {
        TipoEventoSistema.PagoRegistrado => ("Pago registrado", "bi-cash-coin", "success"),
        TipoEventoSistema.PagoAnulado => ("Pago anulado", "bi-x-octagon", "danger"),
        TipoEventoSistema.SocioCreado => ("Alta de socio", "bi-person-plus", "success"),
        TipoEventoSistema.SocioEditado => ("Datos del socio actualizados", "bi-pencil", "primary"),
        TipoEventoSistema.SocioBaja => ("Baja de socio", "bi-person-x", "danger"),
        TipoEventoSistema.SocioReactivado => ("Socio reactivado", "bi-arrow-counterclockwise", "success"),
        TipoEventoSistema.SuscripcionAlta => ("Nueva suscripción", "bi-card-checklist", "primary"),
        TipoEventoSistema.SuscripcionRenovada => ("Membresía renovada", "bi-arrow-repeat", "info"),
        TipoEventoSistema.CredencialRegenerada => ("Credencial reemitida", "bi-person-vcard", "warning"),
        TipoEventoSistema.PlanCreado => ("Plan creado", "bi-box-seam", "success"),
        TipoEventoSistema.PlanEditado => ("Plan modificado", "bi-pencil", "primary"),
        TipoEventoSistema.PlanActivado => ("Plan activado", "bi-toggle-on", "success"),
        TipoEventoSistema.PlanDesactivado => ("Plan desactivado", "bi-toggle-off", "muted"),
        TipoEventoSistema.PlanEliminado => ("Plan eliminado", "bi-trash", "danger"),
        TipoEventoSistema.EntrenadorCreado => ("Alta de entrenador", "bi-person-badge", "success"),
        TipoEventoSistema.EntrenadorEditado => ("Entrenador modificado", "bi-pencil", "primary"),
        TipoEventoSistema.EntrenadorActivado => ("Entrenador activado", "bi-toggle-on", "success"),
        TipoEventoSistema.EntrenadorDesactivado => ("Entrenador desactivado", "bi-toggle-off", "muted"),
        TipoEventoSistema.EntrenadorEliminado => ("Entrenador eliminado", "bi-trash", "danger"),
        TipoEventoSistema.EntrenadorAsignado => ("Entrenador asignado", "bi-person-check", "info"),
        TipoEventoSistema.EntrenadorDesasignado => ("Entrenador desasignado", "bi-person-dash", "warning"),
        TipoEventoSistema.EntrenadorObservacion => ("Observación de entrenador", "bi-chat-left-text", "muted"),
        TipoEventoSistema.SesionIniciada => ("Inicio de sesión", "bi-box-arrow-in-right", "success"),
        TipoEventoSistema.SesionCerrada => ("Cierre de sesión", "bi-box-arrow-right", "muted"),
        TipoEventoSistema.LoginFallido => ("Intento de ingreso fallido", "bi-shield-exclamation", "warning"),
        TipoEventoSistema.UsuarioBloqueado => ("Cuenta bloqueada", "bi-lock-fill", "danger"),
        TipoEventoSistema.AccesoDenegado => ("Acceso denegado", "bi-slash-circle", "danger"),
        TipoEventoSistema.UsuarioCreado => ("Alta de usuario", "bi-person-plus", "success"),
        TipoEventoSistema.UsuarioEditado => ("Usuario modificado", "bi-pencil", "primary"),
        TipoEventoSistema.PasswordCambiada => ("Contraseña cambiada", "bi-key", "info"),
        TipoEventoSistema.PasswordReseteada => ("Contraseña restablecida", "bi-key-fill", "warning"),
        TipoEventoSistema.RolCambiado => ("Cambio de rol", "bi-shield-lock", "warning"),
        TipoEventoSistema.UsuarioActivado => ("Usuario activado", "bi-person-check", "success"),
        TipoEventoSistema.UsuarioDesactivado => ("Usuario desactivado", "bi-person-x", "danger"),
        TipoEventoSistema.RolCreado => ("Rol creado", "bi-shield-plus", "success"),
        TipoEventoSistema.RolPermisosCambiados => ("Permisos de rol modificados", "bi-sliders", "warning"),
        TipoEventoSistema.ReporteExportado => ("Reporte exportado", "bi-file-earmark-arrow-down", "info"),
        TipoEventoSistema.ProductoCreado => ("Producto creado", "bi-box-seam", "success"),
        TipoEventoSistema.ProductoEditado => ("Producto modificado", "bi-pencil", "primary"),
        TipoEventoSistema.ProductoActivado => ("Producto activado", "bi-toggle-on", "success"),
        TipoEventoSistema.ProductoDesactivado => ("Producto desactivado", "bi-toggle-off", "muted"),
        TipoEventoSistema.CategoriaCreada => ("Categoría creada", "bi-tags", "success"),
        TipoEventoSistema.CategoriaEditada => ("Categoría modificada", "bi-tags", "primary"),
        TipoEventoSistema.StockAjustado => ("Movimiento de stock", "bi-arrow-left-right", "warning"),
        TipoEventoSistema.VentaRealizada => ("Venta realizada", "bi-cart-check", "success"),
        TipoEventoSistema.VentaAnulada => ("Venta anulada", "bi-cart-x", "danger"),
        TipoEventoSistema.BrandingModificado => ("Identidad visual modificada", "bi-palette", "primary"),
        TipoEventoSistema.KioscoConfigurado => ("Kiosco configurado", "bi-display", "info"),
        TipoEventoSistema.AvisoCreado => ("Aviso de recepción creado", "bi-megaphone", "success"),
        TipoEventoSistema.AvisoEditado => ("Aviso de recepción modificado", "bi-megaphone", "primary"),
        TipoEventoSistema.AvisoEliminado => ("Aviso de recepción eliminado", "bi-trash", "danger"),
        _ => (t.ToString(), "bi-dot", "muted")
    };

    public static string Texto(this EntidadAuditada e) => e switch
    {
        EntidadAuditada.Socio => "Socio",
        EntidadAuditada.Plan => "Plan",
        EntidadAuditada.Pago => "Pago",
        EntidadAuditada.Entrenador => "Entrenador",
        EntidadAuditada.Usuario => "Usuario",
        EntidadAuditada.Rol => "Rol",
        EntidadAuditada.Reporte => "Reporte",
        EntidadAuditada.Producto => "Producto",
        EntidadAuditada.Venta => "Venta",
        EntidadAuditada.Categoria => "Categoría",
        EntidadAuditada.Configuracion => "Configuración",
        EntidadAuditada.Aviso => "Aviso",
        _ => e.ToString()
    };

    // Enlace a la entidad afectada (null si fue eliminada o no se conoce el Id)
    public static string? Href(this EventoSistema ev, IUrlHelper url)
    {
        if (ev.EntidadId is not { } id || ev.Tipo is TipoEventoSistema.PlanEliminado or TipoEventoSistema.EntrenadorEliminado) return null;
        return ev.Entidad switch
        {
            EntidadAuditada.Socio => url.Page("/Socios/Detalle", new { id }),
            EntidadAuditada.Plan => url.Page("/Planes/Detalle", new { id }),
            EntidadAuditada.Pago => url.Page("/Pagos/Recibo", new { id }),
            EntidadAuditada.Entrenador => url.Page("/Entrenadores/Detalle", new { id }),
            EntidadAuditada.Usuario => url.Page("/Administracion/Usuarios/Detalle", new { id }),
            EntidadAuditada.Rol => url.Page("/Administracion/Roles/Editar", new { id }),
            EntidadAuditada.Producto => url.Page("/Ventas/Productos/Detalle", new { id }),
            EntidadAuditada.Venta => url.Page("/Ventas/Ticket", new { id }),
            EntidadAuditada.Categoria => url.Page("/Ventas/Categorias"),
            EntidadAuditada.Configuracion => url.Page("/Configuracion/Branding"),
            EntidadAuditada.Aviso => ev.Tipo == TipoEventoSistema.AvisoEliminado ? null : url.Page("/Configuracion/Recepcion"),
            _ => null
        };
    }

    // Equivalencia con el historial propio de entrenadores
    public static TipoEventoSistema DesdeEntrenador(TipoEventoEntrenador t) => t switch
    {
        TipoEventoEntrenador.Alta => TipoEventoSistema.EntrenadorCreado,
        TipoEventoEntrenador.AsignacionSocio => TipoEventoSistema.EntrenadorAsignado,
        TipoEventoEntrenador.DesasignacionSocio => TipoEventoSistema.EntrenadorDesasignado,
        TipoEventoEntrenador.Activacion => TipoEventoSistema.EntrenadorActivado,
        TipoEventoEntrenador.Desactivacion => TipoEventoSistema.EntrenadorDesactivado,
        TipoEventoEntrenador.Observacion => TipoEventoSistema.EntrenadorObservacion,
        _ => TipoEventoSistema.EntrenadorEditado   // Modificacion, CambioHorario
    };
}
