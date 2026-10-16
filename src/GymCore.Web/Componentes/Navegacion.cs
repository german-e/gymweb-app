using GymCore.Web.Seguridad;

namespace GymCore.Web.Componentes;

// Menú lateral como datos: grupos colapsables con ítems filtrados por permiso.
// Un grupo sin ítems visibles no se muestra; un ítem nunca aparece si el usuario no puede abrir la pantalla.
public static class Navegacion
{
    /// <param name="Prefijos">rutas de página que lo marcan como activo</param>
    /// <param name="Externo">abre en otra pestaña (kiosco)</param>
    public sealed record Item(string Clave, string Texto, string Icono, string Pagina, string[] Prefijos, bool Externo = false)
    {
        public bool EsActivo(string pagina) => Prefijos.Any(p => p.EndsWith('$')
            ? string.Equals(pagina, p[..^1], StringComparison.OrdinalIgnoreCase)
            : pagina.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    public sealed record Grupo(string Clave, string Texto, string Icono, IReadOnlyList<Item> Items)
    {
        public bool Contiene(string pagina) => Items.Any(i => i.EsActivo(pagina));
    }

    public sealed record Menu(Item Inicio, IReadOnlyList<Grupo> Grupos)
    {
        public IEnumerable<Item> Todos => Grupos.SelectMany(g => g.Items).Prepend(Inicio);
    }

    public static Menu Para(IUsuarioActual u)
    {
        var verSocios = u.Puede(Permisos.SociosVer);
        Item? I(bool visible, string clave, string texto, string icono, string pagina, params string[] prefijos) =>
            visible ? new Item(clave, texto, icono, pagina, prefijos.Length == 0 ? new[] { pagina } : prefijos) : null;

        var grupos = new List<Grupo>
        {
            G("operacion", "Operación", "bi-house-door",
                I(u.PuedeAlguno(Permisos.SociosVer, Permisos.SociosVerAsignados), "socios", verSocios ? "Socios" : "Mis socios", "bi-people", "/Socios/Socios", "/Socios/Socios", "/Socios/Create", "/Socios/Edit", "/Socios/Detalle", "/Socios/Tarjeta", "/Suscripciones"),
                I(u.PuedeAlguno(Permisos.EvolucionVer, Permisos.EvolucionRegistrar) && u.PuedeAlguno(Permisos.SociosVer, Permisos.SociosVerAsignados), "evolucion", "Evolución física", "bi-activity", "/Socios/Evolucion"),
                I(u.Puede(Permisos.EntrenadoresVer), "entrenadores", "Entrenadores", "bi-person-badge", "/Entrenadores/Index", "/Entrenadores"),
                I(u.Puede(Permisos.AsistenciasRegistrar), "ingreso", "Control de ingreso", "bi-door-open", "/Asistencias/Ingreso", "/Asistencias")),
            G("comercial", "Comercial", "bi-cash-coin",
                I(u.Puede(Permisos.PlanesVer), "planes", "Planes", "bi-box-seam", "/Planes/Planes", "/Planes"),
                I(u.Puede(Permisos.PagosVer), "pagos", "Pagos", "bi-credit-card", "/Pagos/Index", "/Pagos"),
                I(u.Puede(Permisos.VentasCrear), "pos", "Punto de Venta", "bi-cart3", "/Ventas/Pos"),
                I(u.Puede(Permisos.VentasVer), "ventas", "Ventas", "bi-receipt", "/Ventas/Index", "/Ventas/Index$", "/Ventas/Ticket")),
            G("inventario", "Inventario", "bi-boxes",
                I(u.Puede(Permisos.ProductosVer), "productos", "Productos", "bi-box2", "/Ventas/Productos/Index", "/Ventas/Productos"),
                I(u.Puede(Permisos.ProductosVer), "categorias", "Categorías", "bi-tags", "/Ventas/Categorias"),
                I(u.Puede(Permisos.ProductosVer), "stock", "Stock y alertas", "bi-clipboard-data", "/Ventas/Inventario"),
                I(u.Puede(Permisos.InventarioAdministrar), "movimientos", "Movimientos", "bi-arrow-left-right", "/Ventas/Movimientos")),
            G("inteligencia", "Inteligencia", "bi-graph-up-arrow",
                I(u.Puede(Permisos.ReportesVer), "reportes", "Reportes", "bi-speedometer2", "/Reportes/Index", "/Reportes"),
                I(u.Puede(Permisos.ReportesComercialesVer), "reportes-comerciales", "Reportes comerciales", "bi-bar-chart-line", "/Ventas/Reportes"),
                I(u.Puede(Permisos.AuditoriaVer), "auditoria", "Auditoría", "bi-shield-check", "/Administracion/Auditoria")),
            G("administracion", "Administración", "bi-gear",
                I(u.Puede(Permisos.UsuariosAdministrar), "usuarios", "Usuarios", "bi-person-gear", "/Administracion/Usuarios/Index", "/Administracion/Usuarios"),
                I(u.Puede(Permisos.UsuariosAdministrar), "roles", "Roles y permisos", "bi-shield-lock", "/Administracion/Roles/Index", "/Administracion/Roles"),
                I(u.Puede(Permisos.ConfiguracionAdministrar), "configuracion", "Configuración", "bi-sliders", "/Configuracion/Index", "/Configuracion")),
        };
        return new Menu(new Item("panel", "Panel", "bi-grid-1x2-fill", "/Panel", new[] { "/Panel" }), grupos.Where(g => g.Items.Count > 0).ToList());
    }

    private static Grupo G(string clave, string texto, string icono, params Item?[] items) =>
        new(clave, texto, icono, items.Where(i => i is not null).Select(i => i!).ToList());
}
