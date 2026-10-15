namespace GymCore.Web.Seguridad;

// Catálogo de permisos. Es la única fuente de verdad: los roles se componen con estos códigos y la UI
// y los handlers se protegen por permiso, nunca por nombre de rol. Agregar un permiso = agregar una línea acá.
public static class Permisos
{
    public const string SociosVer = "SOCIOS_VER";
    public const string SociosVerAsignados = "SOCIOS_VER_ASIGNADOS";
    public const string SociosCrear = "SOCIOS_CREAR";
    public const string SociosEditar = "SOCIOS_EDITAR";
    public const string SociosEliminar = "SOCIOS_ELIMINAR";
    public const string CredencialesEmitir = "CREDENCIALES_EMITIR";
    public const string EvolucionVer = "EVOLUCION_VER";
    public const string EvolucionRegistrar = "EVOLUCION_REGISTRAR";
    public const string AsistenciasRegistrar = "ASISTENCIAS_REGISTRAR";
    public const string EntrenadoresVer = "ENTRENADORES_VER";
    public const string EntrenadoresEditar = "ENTRENADORES_EDITAR";
    public const string PlanesVer = "PLANES_VER";
    public const string PlanesEditar = "PLANES_EDITAR";
    public const string PagosVer = "PAGOS_VER";
    public const string PagosCrear = "PAGOS_CREAR";
    public const string PagosAnular = "PAGOS_ANULAR";
    public const string AuditoriaVer = "AUDITORIA_VER";
    public const string ReportesVer = "REPORTES_VER";
    public const string ReportesExportar = "REPORTES_EXPORTAR";
    public const string ProductosVer = "PRODUCTOS_VER";
    public const string ProductosCrear = "PRODUCTOS_CREAR";
    public const string ProductosEditar = "PRODUCTOS_EDITAR";
    public const string VentasVer = "VENTAS_VER";
    public const string VentasCrear = "VENTAS_CREAR";
    public const string VentasAnular = "VENTAS_ANULAR";
    public const string InventarioAdministrar = "INVENTARIO_ADMINISTRAR";
    public const string ReportesComercialesVer = "REPORTES_COMERCIALES_VER";
    public const string UsuariosAdministrar = "USUARIOS_ADMINISTRAR";
    public const string ConfiguracionAdministrar = "CONFIGURACION_ADMINISTRAR";

    public record Definicion(string Codigo, string Modulo, string Nombre, string Descripcion, bool Sensible = false);

    public static readonly IReadOnlyList<Definicion> Catalogo = new Definicion[]
    {
        new(SociosVer, "Socios", "Ver socios", "Listado y ficha de todos los socios."),
        new(SociosVerAsignados, "Socios", "Ver socios asignados", "Solo los socios asignados al entrenador vinculado al usuario."),
        new(SociosCrear, "Socios", "Crear socios", "Alta de socios con su plan inicial."),
        new(SociosEditar, "Socios", "Editar socios", "Datos, suscripciones, renovaciones y asignación de entrenador."),
        new(SociosEliminar, "Socios", "Dar de baja socios", "Baja y reactivación de socios.", Sensible: true),
        new(CredencialesEmitir, "Socios", "Emitir credenciales", "Ver, imprimir y reemitir la credencial del socio."),
        new(EvolucionVer, "Evolución física", "Ver evolución física", "Mediciones y gráficos de evolución."),
        new(EvolucionRegistrar, "Evolución física", "Registrar evolución", "Cargar y eliminar mediciones."),
        new(AsistenciasRegistrar, "Asistencias", "Control de ingreso", "Registrar ingresos y ver el reporte semanal."),
        new(EntrenadoresVer, "Entrenadores", "Ver entrenadores", "Listado y ficha de entrenadores."),
        new(EntrenadoresEditar, "Entrenadores", "Gestionar entrenadores", "Altas, cambios, asignaciones y estado."),
        new(PlanesVer, "Planes", "Ver planes", "Catálogo de planes y su ficha."),
        new(PlanesEditar, "Planes", "Gestionar planes", "Crear, editar, duplicar, activar y eliminar planes."),
        new(PagosVer, "Pagos", "Ver pagos y cobranza", "Listado de pagos, deudas, recaudación e indicadores financieros."),
        new(PagosCrear, "Pagos", "Registrar pagos", "Cobrar cuotas y emitir comprobantes."),
        new(PagosAnular, "Pagos", "Anular pagos", "Anulación lógica de pagos con motivo.", Sensible: true),
        new(AuditoriaVer, "Administración", "Ver auditoría", "Auditoría global y actividad reciente del sistema.", Sensible: true),
        new(ReportesVer, "Inteligencia de negocio", "Ver reportes", "Resumen ejecutivo, evolución, ingresos, planes, entrenadores y riesgo de abandono."),
        new(ReportesExportar, "Inteligencia de negocio", "Exportar reportes", "Descargar reportes en Excel y PDF (datos de socios y cobros).", Sensible: true),
        new(ProductosVer, "Ventas e inventario", "Ver productos", "Catálogo de productos, categorías, stock y alertas de inventario."),
        new(ProductosCrear, "Ventas e inventario", "Crear productos", "Alta de productos y categorías."),
        new(ProductosEditar, "Ventas e inventario", "Editar productos", "Precios, datos, categorías, activar y desactivar productos."),
        new(VentasVer, "Ventas e inventario", "Ver ventas", "Listado de ventas, tickets y compras de cada socio."),
        new(VentasCrear, "Ventas e inventario", "Vender (Punto de Venta)", "Cobrar productos y cuotas desde el Punto de Venta."),
        new(VentasAnular, "Ventas e inventario", "Anular ventas", "Anulación de tickets con reposición de stock.", Sensible: true),
        new(InventarioAdministrar, "Ventas e inventario", "Administrar inventario", "Registrar compras, ajustes y correcciones de stock; ver movimientos.", Sensible: true),
        new(ReportesComercialesVer, "Ventas e inventario", "Ver reportes comerciales", "Ventas, márgenes, rentabilidad y productos más vendidos.", Sensible: true),
        new(UsuariosAdministrar, "Administración", "Administrar usuarios y roles", "Usuarios, roles, permisos y reseteo de contraseñas.", Sensible: true),
        new(ConfiguracionAdministrar, "Administración", "Configuración", "Parámetros generales del gimnasio.", Sensible: true),
    };

    public static readonly IReadOnlySet<string> Codigos = Catalogo.Select(p => p.Codigo).ToHashSet();

    public static Definicion? Buscar(string codigo) => Catalogo.FirstOrDefault(p => p.Codigo == codigo);
}

// Roles con los que arranca un gimnasio. Se crean si faltan; después se editan desde Administración > Roles.
public static class RolesIniciales
{
    public record Definicion(string Nombre, string Descripcion, bool AccesoTotal, string[] Permisos);

    private static readonly string[] Recepcion =
    {
        Seguridad.Permisos.SociosVer, Seguridad.Permisos.SociosCrear, Seguridad.Permisos.SociosEditar,
        Seguridad.Permisos.CredencialesEmitir, Seguridad.Permisos.AsistenciasRegistrar,
        Seguridad.Permisos.PlanesVer, Seguridad.Permisos.EntrenadoresVer,
        Seguridad.Permisos.PagosVer, Seguridad.Permisos.PagosCrear,
        Seguridad.Permisos.ProductosVer, Seguridad.Permisos.VentasVer, Seguridad.Permisos.VentasCrear
    };

    public static readonly IReadOnlyList<Definicion> Todos = new Definicion[]
    {
        new("Administrador", "Acceso total al sistema, incluidos usuarios, roles y configuración.", true, Array.Empty<string>()),
        new("Recepcionista", "Atención en mostrador: socios, cobros, credenciales y control de ingreso.", false, Recepcion),
        new("Supervisor", "Todo lo de recepción más anulaciones, bajas, reportes y auditoría.", false, Recepcion.Concat(new[]
        {
            Seguridad.Permisos.SociosEliminar, Seguridad.Permisos.PagosAnular, Seguridad.Permisos.EvolucionVer,
            Seguridad.Permisos.ReportesVer, Seguridad.Permisos.ReportesExportar, Seguridad.Permisos.AuditoriaVer,
            Seguridad.Permisos.ProductosCrear, Seguridad.Permisos.ProductosEditar, Seguridad.Permisos.VentasAnular,
            Seguridad.Permisos.InventarioAdministrar, Seguridad.Permisos.ReportesComercialesVer
        }).ToArray()),
        new("Entrenador", "Sus socios asignados y su evolución física. Sin acceso a pagos ni cobranza.", false, new[]
        {
            Seguridad.Permisos.SociosVerAsignados, Seguridad.Permisos.EvolucionVer, Seguridad.Permisos.EvolucionRegistrar
        }),
    };
}
