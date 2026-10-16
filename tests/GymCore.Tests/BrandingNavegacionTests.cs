using System.Text;
using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Web.Componentes;
using GymCore.Web.Marca;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace GymCore.Tests;

// Branding multi-gimnasio, navegación por permisos y reglas de ingreso compartidas por recepción y kiosco.
public class BrandingNavegacionTests
{
    // ===================== Colores =====================

    [Theory]
    [InlineData("#2563eb", "#2563EB")]
    [InlineData("2563EB", "#2563EB")]
    [InlineData(" #26e ", "#2266EE")]
    [InlineData("rojo", null)]
    [InlineData("#12345", null)]
    [InlineData("#2563eb; } body { display:none", null)]   // no se puede inyectar CSS
    [InlineData(null, null)]
    public void Normaliza_colores_hexadecimales(string? entrada, string? esperado) =>
        Assert.Equal(esperado, Colores.Normalizar(entrada));

    [Fact]
    public void Contraste_segun_WCAG()
    {
        Assert.Equal(21, Math.Round(Colores.Contraste(Colores.Blanco, new Colores.Rgb(0, 0, 0)), 1));
        Assert.Equal(1, Colores.Contraste(Colores.Blanco, Colores.Blanco));
    }

    [Theory]
    [InlineData("#FACC15")]   // amarillo: ilegible sobre blanco
    [InlineData("#A3E635")]   // verde lima
    [InlineData("#FFFFFF")]
    public void Un_color_claro_se_oscurece_lo_justo_para_ser_legible(string color)
    {
        var b = ServicioBranding.Construir(new ConfiguracionGimnasio { ColorPrincipal = color });
        Assert.True(b.TonoAjustado);
        Assert.True(Colores.Contraste(b.Primario, Colores.Blanco) >= 3.0);
        Assert.True(Colores.Contraste(b.PrimarioFuerte, Colores.Blanco) >= 4.5);
        Assert.True(Colores.Contraste(b.TextoSobrePrimario, b.Primario) >= 3.0);
    }

    [Fact]
    public void Un_color_con_buen_contraste_no_se_modifica()
    {
        var b = ServicioBranding.Construir(new ConfiguracionGimnasio { ColorPrincipal = "#7C3AED", ColorSecundario = "#DB2777" });
        Assert.False(b.TonoAjustado);
        Assert.Equal("#7C3AED", b.Primario.Hex);
        Assert.Contains("--primary:#7C3AED", b.VariablesCss);
        Assert.Contains("--primary-rgb:124 58 237", b.VariablesCss);
        Assert.Contains("--cred-brand-a", b.VariablesCss);   // la credencial toma los colores propios
    }

    [Fact]
    public void La_paleta_por_defecto_conserva_exactamente_el_aspecto_original()
    {
        var b = ServicioBranding.Construir(new ConfiguracionGimnasio());
        Assert.True(b.EsPaletaPorDefecto);
        Assert.Equal("#2563EB", b.Primario.Hex);
        Assert.Equal("#1D4ED8", b.PrimarioFuerte.Hex);
        Assert.Equal("#FFFFFF", b.TextoSobrePrimario.Hex);
        Assert.DoesNotContain("--cred-brand", b.VariablesCss);
    }

    [Fact]
    public void Datos_invalidos_guardados_no_rompen_la_identidad()
    {
        var b = ServicioBranding.Construir(new ConfiguracionGimnasio
        {
            NombreComercial = "  ", ColorPrincipal = "verde", ColorSecundario = "", LogoUrl = "uploads/branding/a.png",
            KioscoSegundosResultado = 999, KioscoSegundosRotacion = 0
        });
        Assert.Equal("GymCore", b.Nombre);
        Assert.Equal("#2563EB", b.ColorPrincipal);
        Assert.Equal("/uploads/branding/a.png", b.Logo);
        Assert.Equal("/uploads/branding/a.png", b.Isotipo);   // sin logo reducido usa el logo
        Assert.Equal(30, b.Kiosco.SegundosResultado);
        Assert.Equal(3, b.Kiosco.SegundosRotacion);
        Assert.Equal(ServicioBranding.MensajeKioscoPorDefecto, b.Kiosco.Mensaje);
    }

    [Fact]
    public async Task Sin_configuracion_usa_el_nombre_de_appsettings_y_se_invalida_al_guardar()
    {
        using var bd = new BaseDePrueba();
        using var db = bd.Contexto();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Gimnasio:Nombre"] = "Club Atlas" }).Build();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var s = new ServicioBranding(db, cache, config);
        Assert.Equal("Club Atlas", (await s.ActualAsync()).Nombre);

        var cfg = await s.EditableAsync();
        cfg.NombreComercial = "Atlas Fitness";
        await db.SaveChangesAsync();
        Assert.Equal("Club Atlas", (await s.ActualAsync()).Nombre);   // cacheado
        s.Invalidar();
        Assert.Equal("Atlas Fitness", (await s.ActualAsync()).Nombre);
    }

    // ===================== Imágenes =====================

    private static IFormFile Archivo(string nombre, byte[] contenido) =>
        new FormFile(new MemoryStream(contenido), 0, contenido.Length, "f", nombre);

    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 1, 2, 3 };

    [Fact]
    public async Task Solo_acepta_imagenes_reales_y_nunca_SVG()
    {
        Assert.Null(await ImagenesMarca.ValidarAsync(Archivo("logo.png", Png), ImagenesMarca.Uso.Logo));
        Assert.Null(await ImagenesMarca.ValidarAsync(null, ImagenesMarca.Uso.Logo));
        Assert.Contains("SVG", await ImagenesMarca.ValidarAsync(Archivo("logo.svg", Encoding.UTF8.GetBytes("<svg onload=alert(1)>")), ImagenesMarca.Uso.Logo));
        Assert.Equal("El archivo no es una imagen válida.", await ImagenesMarca.ValidarAsync(Archivo("logo.png", Encoding.UTF8.GetBytes("<script>alert(1)</script>")), ImagenesMarca.Uso.Logo));
        Assert.NotNull(await ImagenesMarca.ValidarAsync(Archivo("grande.png", Png.Concat(new byte[2 * 1024 * 1024]).ToArray()), ImagenesMarca.Uso.Logo));
        Assert.Null(await ImagenesMarca.ValidarAsync(Archivo("favicon.ico", new byte[] { 0, 0, 1, 0, 1, 0 }), ImagenesMarca.Uso.Favicon));
        Assert.NotNull(await ImagenesMarca.ValidarAsync(Archivo("favicon.jpg", new byte[] { 0xFF, 0xD8, 0xFF, 0 }), ImagenesMarca.Uso.Favicon));
    }

    // ===================== Navegación =====================

    private sealed class Usuario : IUsuarioActual
    {
        private readonly HashSet<string> _p; private readonly bool _total;
        public Usuario(bool total, params string[] permisos) { _total = total; _p = permisos.ToHashSet(); }
        public static Usuario DeRol(string rol)
        {
            var d = RolesIniciales.Todos.Single(r => r.Nombre == rol);
            return new Usuario(d.AccesoTotal, d.Permisos);
        }
        public int? UsuarioId => 1; public string Nombre => "Test"; public bool Autenticado => true; public string NombreUsuario => "t";
        public string Rol => "r"; public string Iniciales => "T"; public int? EntrenadorId => null; public DateTime? AccesoAnterior => null;
        public bool Puede(string permiso) => _total || _p.Contains(permiso);
        public bool PuedeAlguno(params string[] permisos) => permisos.Any(Puede);
    }

    private static Dictionary<string, string[]> Menu(Usuario u) =>
        Navegacion.Para(u).Grupos.ToDictionary(g => g.Clave, g => g.Items.Select(i => i.Clave).ToArray());

    [Fact]
    public void Administrador_ve_todos_los_grupos()
    {
        var m = Menu(Usuario.DeRol("Administrador"));
        Assert.Equal(new[] { "operacion", "comercial", "inventario", "inteligencia", "administracion" }, m.Keys);
        Assert.Contains("configuracion", m["administracion"]);
        Assert.Contains("movimientos", m["inventario"]);
    }

    [Fact]
    public void Recepcion_ve_operacion_comercial_e_inventario_sin_ruido()
    {
        var m = Menu(Usuario.DeRol("Recepcionista"));
        Assert.Equal(new[] { "operacion", "comercial", "inventario" }, m.Keys);
        Assert.Equal(new[] { "socios", "entrenadores", "ingreso" }, m["operacion"]);
        Assert.Equal(new[] { "planes", "pagos", "pos", "ventas" }, m["comercial"]);
        Assert.DoesNotContain("movimientos", m["inventario"]);
    }

    [Fact]
    public void Entrenador_ve_solo_panel_mis_socios_y_evolucion()
    {
        var menu = Navegacion.Para(Usuario.DeRol("Entrenador"));
        var g = Assert.Single(menu.Grupos);
        Assert.Equal(new[] { "Mis socios", "Evolución física" }, g.Items.Select(i => i.Texto));
        Assert.Equal("Panel", menu.Inicio.Texto);
    }

    [Fact]
    public void Ningun_grupo_queda_vacio_y_el_activo_se_detecta_por_ruta()
    {
        var m = Navegacion.Para(new Usuario(false, Permisos.VentasVer));
        var g = Assert.Single(m.Grupos);
        Assert.True(g.Contiene("/Ventas/Ticket"));
        Assert.True(g.Contiene("/Ventas/Index"));
        Assert.False(g.Contiene("/Ventas/Pos"));   // "Ventas" no queda activo en el Punto de Venta
        Assert.Empty(Navegacion.Para(new Usuario(false)).Grupos);
    }

    // ===================== Ingreso (reglas compartidas) =====================

    [Fact]
    public async Task Ingreso_registra_detecta_duplicado_y_rechaza_sin_membresia()
    {
        using var bd = new BaseDePrueba();
        var (socio, sp) = await bd.SocioConPlanAsync();
        using (var db = bd.Contexto())
        {
            var plan = db.Planes.Single(p => p.Id == sp.PlanId);
            plan.PermiteAccesoSabado = plan.PermiteAccesoDomingo = true;
            var vencido = new Socio { Nombre = "Vera", Apellido = "Vencida", Dni = "11111111" };
            db.SocioPlanes.Add(new SocioPlan { Socio = vencido, PlanId = plan.Id, FechaInicio = DateTime.Today.AddDays(-60), FechaFin = DateTime.Today.AddDays(-30), Estado = EstadoSuscripcion.Activo });
            await db.SaveChangesAsync();
        }
        using var db2 = bd.Contexto();
        var s = new ServicioIngreso(db2);

        var r1 = await s.RegistrarAsync(socio.Dni, false);
        Assert.Equal(ServicioIngreso.Estado.Registrado, r1.Estado);
        Assert.Equal(ServicioIngreso.Estado.Duplicado, (await s.RegistrarAsync(socio.Dni, false)).Estado);
        Assert.Equal(ServicioIngreso.Estado.SinSuscripcion, (await s.RegistrarAsync("11111111", false)).Estado);
        Assert.Equal(ServicioIngreso.Estado.NoEncontrado, (await s.RegistrarAsync("99999999", false)).Estado);
        Assert.Equal(ServicioIngreso.Estado.CodigoVacio, (await s.RegistrarAsync("  ", false)).Estado);
        Assert.Equal(1, db2.Asistencias.Count());
    }

    [Fact]
    public void La_api_conserva_sus_respuestas_historicas()
    {
        string Mensaje(ServicioIngreso.Estado e)
        {
            var r = ServicioIngreso.RespuestaApi(new(e, new Socio { Nombre = "A", Apellido = "B" }, UsosSemana: 3, MaximoSemana: 3));
            return (string)r.GetType().GetProperty("mensaje")!.GetValue(r)!;
        }
        Assert.Equal("Socio no encontrado", Mensaje(ServicioIngreso.Estado.NoEncontrado));
        Assert.Equal("Suscripción no activa o vencida", Mensaje(ServicioIngreso.Estado.SinSuscripcion));
        Assert.Equal("Ingreso ya registrado recientemente", Mensaje(ServicioIngreso.Estado.Duplicado));
        Assert.Equal("Ingreso registrado", Mensaje(ServicioIngreso.Estado.Registrado));
        Assert.StartsWith("Cupo semanal excedido (3/3)", Mensaje(ServicioIngreso.Estado.CupoExcedido));
    }
}
