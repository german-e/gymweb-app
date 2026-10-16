namespace GymCore.Dominio.Enums;

// Acciones auditables. Los valores se persisten: no reordenar ni reutilizar números.
public enum TipoEventoSistema
{
    // Pagos
    PagoRegistrado = 100,
    PagoAnulado = 101,

    // Socios
    SocioCreado = 200,
    SocioEditado = 201,
    SocioBaja = 202,
    SocioReactivado = 203,
    SuscripcionAlta = 204,
    SuscripcionRenovada = 205,
    CredencialRegenerada = 206,

    // Planes
    PlanCreado = 300,
    PlanEditado = 301,
    PlanActivado = 302,
    PlanDesactivado = 303,
    PlanEliminado = 304,

    // Entrenadores
    EntrenadorCreado = 400,
    EntrenadorEditado = 401,
    EntrenadorActivado = 402,
    EntrenadorDesactivado = 403,
    EntrenadorEliminado = 404,
    EntrenadorAsignado = 405,
    EntrenadorDesasignado = 406,
    EntrenadorObservacion = 407,

    // Seguridad: sesiones
    SesionIniciada = 500,
    SesionCerrada = 501,
    LoginFallido = 502,
    UsuarioBloqueado = 503,
    AccesoDenegado = 504,

    // Seguridad: administración de usuarios y roles
    UsuarioCreado = 600,
    UsuarioEditado = 601,
    PasswordCambiada = 602,
    PasswordReseteada = 603,
    RolCambiado = 604,
    UsuarioActivado = 605,
    UsuarioDesactivado = 606,
    RolCreado = 607,
    RolPermisosCambiados = 608,

    // Inteligencia de negocio
    ReporteExportado = 700,

    // Ventas e inventario
    ProductoCreado = 800,
    ProductoEditado = 801,
    ProductoActivado = 802,
    ProductoDesactivado = 803,
    CategoriaCreada = 804,
    CategoriaEditada = 805,
    StockAjustado = 806,
    VentaRealizada = 807,
    VentaAnulada = 808,

    // Configuración del gimnasio
    BrandingModificado = 900,
    KioscoConfigurado = 901,
    AvisoCreado = 902,
    AvisoEditado = 903,
    AvisoEliminado = 904
}

// Entidad principal afectada por el evento.
public enum EntidadAuditada
{
    Socio = 1,
    Plan = 2,
    Pago = 3,
    Entrenador = 4,
    Usuario = 5,
    Rol = 6,
    Reporte = 7,
    Producto = 8,
    Venta = 9,
    Categoria = 10,
    Configuracion = 11,
    Aviso = 12
}
