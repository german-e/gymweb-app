using System.Net;
using System.Text.RegularExpressions;
using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GymCore.Tests;

// App real en memoria (WebApplicationFactory) sobre una base SQLite temporal.
public sealed class AppDePrueba : WebApplicationFactory<Program>
{
    public const string Clave = "Prueba-2026";
    public const string Coleccion = "Aplicación web";
    private readonly string _ruta = Path.Combine(Path.GetTempPath(), $"gymcore-test-{Guid.NewGuid():N}.db");

    public int SocioAsignado { get; private set; }
    public int SocioAjeno { get; private set; }
    public int PagoId { get; private set; }

    public AppDePrueba()
    {
        // Program lee la cadena de conexión al construir el host
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", $"Data Source={_ruta}");
        Sembrar();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development");

    private void Sembrar()
    {
        using var scope = Services.CreateScope();   // arranca la app: esquema y roles iniciales
        var db = scope.ServiceProvider.GetRequiredService<GymDbContext>();
        int Rol(string nombre) => db.Roles.Single(r => r.Nombre == nombre).Id;

        var entrenador = new Entrenador { Nombre = "Romina", Apellido = "Test", Dni = "30000000" };
        var asignado = new Socio { Nombre = "Asignado", Apellido = "Socio", Dni = "40000001" };
        var ajeno = new Socio { Nombre = "Ajeno", Apellido = "Socio", Dni = "40000002" };
        db.AddRange(entrenador, asignado, ajeno);
        db.EntrenadorSocios.Add(new EntrenadorSocio { Entrenador = entrenador, Socio = asignado });
        var pago = new Pago { Socio = asignado, Importe = 1000, Medio = MedioPago.Efectivo };
        db.Pagos.Add(pago);
        // Pago anulado: no debe aparecer en ningún indicador ni reporte
        var anulado = new Pago { Socio = ajeno, Importe = 7777, Medio = MedioPago.Efectivo };
        anulado.Anular("Cobro duplicado", "Sistema", DateTime.UtcNow);
        db.Pagos.Add(anulado);
        db.SaveChanges();

        void Usuario(string nombre, string rol, bool temporal = false, int? entrenadorId = null) =>
            db.Usuarios.Add(new Usuario(nombre + " Test", nombre, Autenticacion.Hash(Clave), Rol(rol), temporal) { EntrenadorId = entrenadorId });
        Usuario("admin", "Administrador");
        Usuario("recepcion", "Recepcionista");
        Usuario("entrenador", "Entrenador", entrenadorId: entrenador.Id);
        Usuario("bloqueable", "Recepcionista");
        Usuario("temporal", "Recepcionista", temporal: true);
        Usuario("efimero", "Recepcionista");
        db.SaveChanges();

        SocioAsignado = asignado.Id;
        SocioAjeno = ajeno.Id;
        PagoId = pago.Id;
    }

    public HttpClient Cliente() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    public T Db<T>(Func<GymDbContext, T> consulta)
    {
        using var scope = Services.CreateScope();
        return consulta(scope.ServiceProvider.GetRequiredService<GymDbContext>());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_ruta); } catch { }
    }
}

// Las clases que levantan la app comparten colección: AppDePrueba fija la cadena de conexión por variable
// de entorno (global del proceso), así que no pueden arrancar en paralelo.
[CollectionDefinition(AppDePrueba.Coleccion)]
public sealed class ColeccionAplicacionWeb { }

[Collection(AppDePrueba.Coleccion)]
public class AutenticacionIntegracionTests : IClassFixture<AppDePrueba>
{
    private readonly AppDePrueba _app;
    public AutenticacionIntegracionTests(AppDePrueba app) => _app = app;

    private static async Task<string> TokenAsync(HttpClient c, string url)
    {
        var html = await c.GetStringAsync(url);
        return Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient c, string url, string paginaToken, Dictionary<string, string> datos)
    {
        datos["__RequestVerificationToken"] = await TokenAsync(c, paginaToken);
        return await c.PostAsync(url, new FormUrlEncodedContent(datos));
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient c, string usuario, string clave = AppDePrueba.Clave) =>
        PostAsync(c, "/Cuenta/Login", "/Cuenta/Login", new() { ["NombreUsuario"] = usuario, ["Password"] = clave });

    private static string? Destino(HttpResponseMessage r) => r.Headers.Location?.OriginalString;

    [Fact]
    public async Task Sin_sesion_toda_pantalla_redirige_al_login()
    {
        var c = _app.Cliente();
        foreach (var url in new[] { "/panel", "/Socios/Socios", "/Pagos", "/Administracion/Usuarios" })
        {
            var r = await c.GetAsync(url);
            Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
            Assert.Contains("/Cuenta/Login", Destino(r));
        }
    }

    [Fact]
    public async Task La_api_de_ingreso_sin_sesion_responde_401()
    {
        var r = await _app.Cliente().PostAsync("/api/asistencias/checkin",
            new StringContent("{\"code\":\"40000001\"}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Login_correcto_inicia_sesion_y_lo_audita_con_el_usuario_real()
    {
        var c = _app.Cliente();
        var r = await LoginAsync(c, "ADMIN");   // el usuario no distingue mayúsculas
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal("/panel", Destino(r));
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/panel")).StatusCode);

        var id = _app.Db(db => db.Usuarios.Single(u => u.NombreUsuario == "admin").Id);
        Assert.True(_app.Db(db => db.EventosSistema.Any(e => e.Tipo == TipoEventoSistema.SesionIniciada && e.UsuarioId == id && e.Usuario == "admin Test")));
    }

    [Fact]
    public async Task Credenciales_invalidas_dan_el_mismo_mensaje_exista_o_no_el_usuario()
    {
        var c = _app.Cliente();
        var inexistente = await (await LoginAsync(c, "nadie", "Cualquiera1")).Content.ReadAsStringAsync();
        var incorrecta = await (await LoginAsync(c, "recepcion", "Incorrecta1")).Content.ReadAsStringAsync();
        Assert.Contains("Usuario o contraseña incorrectos", WebUtility.HtmlDecode(inexistente));
        Assert.Contains("Usuario o contraseña incorrectos", WebUtility.HtmlDecode(incorrecta));
    }

    [Fact]
    public async Task Cinco_intentos_fallidos_bloquean_la_cuenta_aun_con_la_clave_correcta()
    {
        var c = _app.Cliente();
        for (var i = 0; i < 5; i++) await LoginAsync(c, "bloqueable", "Incorrecta1");
        var r = await LoginAsync(c, "bloqueable");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);   // no entra
        Assert.Contains("bloqueada temporalmente", WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync()));
        Assert.True(_app.Db(db => db.EventosSistema.Any(e => e.Tipo == TipoEventoSistema.UsuarioBloqueado && e.EntidadNombre == "bloqueable Test")));
    }

    [Fact]
    public async Task Recepcion_cobra_pero_no_ve_auditoria_ni_usuarios_ni_puede_anular()
    {
        var c = _app.Cliente();
        await LoginAsync(c, "recepcion");
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/Pagos/Nuevo")).StatusCode);

        foreach (var url in new[] { "/Administracion/Auditoria", "/Administracion/Usuarios", "/Administracion/Roles" })
        {
            var r = await c.GetAsync(url);
            Assert.Contains("/Cuenta/AccesoDenegado", Destino(r));
        }
        var panel = WebUtility.HtmlDecode(await c.GetStringAsync("/panel"));
        Assert.DoesNotContain("Roles y permisos", panel);       // el menú no muestra lo que no puede usar
        Assert.Contains("Registrar pago", panel);

        var anular = await PostAsync(c, $"/Pagos/Recibo/{_app.PagoId}?handler=Anular", "/Pagos/Nuevo", new() { ["motivo"] = "Cobro duplicado" });
        Assert.Contains("/Cuenta/AccesoDenegado", Destino(anular));
        Assert.False(_app.Db(db => db.Pagos.IgnoreQueryFilters().Single(p => p.Id == _app.PagoId).Anulado));
        Assert.True(_app.Db(db => db.EventosSistema.Any(e => e.Tipo == TipoEventoSistema.AccesoDenegado && e.Usuario == "recepcion Test")));
    }

    [Fact]
    public async Task Entrenador_solo_ve_sus_socios_asignados_y_nada_de_pagos()
    {
        var c = _app.Cliente();
        await LoginAsync(c, "entrenador");
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/Socios/Detalle/{_app.SocioAsignado}")).StatusCode);
        Assert.Contains("/Cuenta/AccesoDenegado", Destino(await c.GetAsync($"/Socios/Detalle/{_app.SocioAjeno}")));
        Assert.Contains("/Cuenta/AccesoDenegado", Destino(await c.GetAsync("/Pagos")));

        var lista = WebUtility.HtmlDecode(await c.GetStringAsync("/Socios/Socios"));
        Assert.Contains("Socio, Asignado", lista);
        Assert.DoesNotContain("Socio, Ajeno", lista);
        var ficha = WebUtility.HtmlDecode(await c.GetStringAsync($"/Socios/Detalle/{_app.SocioAsignado}"));
        Assert.DoesNotContain("data-bs-target=\"#tab-pagos\"", ficha);
        Assert.DoesNotContain("Registrar pago", ficha);
    }

    [Fact]
    public async Task La_contrasena_temporal_obliga_a_cambiarla_antes_de_usar_el_sistema()
    {
        var c = _app.Cliente();
        var r = await LoginAsync(c, "temporal");
        Assert.Contains("/Cuenta/CambiarPassword", Destino(r));
        Assert.Contains("/Cuenta/CambiarPassword", Destino(await c.GetAsync("/panel")));

        var cambio = await PostAsync(c, "/Cuenta/CambiarPassword?obligatorio=true", "/Cuenta/CambiarPassword?obligatorio=true",
            new() { ["Obligatorio"] = "true", ["Actual"] = AppDePrueba.Clave, ["Nueva"] = "MiPropia-2027", ["Confirmacion"] = "MiPropia-2027" });
        Assert.Equal(HttpStatusCode.Redirect, cambio.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/panel")).StatusCode);   // la sesión sigue, ya sin restricción
        Assert.False(_app.Db(db => db.Usuarios.Single(u => u.NombreUsuario == "temporal").DebeCambiarPassword));
    }

    [Fact]
    public async Task Desactivar_un_usuario_cierra_su_sesion_en_la_siguiente_request()
    {
        var c = _app.Cliente();
        await LoginAsync(c, "efimero");
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/panel")).StatusCode);

        _app.Db(db => { db.Usuarios.Single(u => u.NombreUsuario == "efimero").Desactivar(); return db.SaveChanges(); });

        var r = await c.GetAsync("/panel");
        Assert.Contains("/Cuenta/Login", Destino(r));
    }

    // ---------- Inteligencia de negocio ----------
    [Fact]
    public async Task Los_reportes_requieren_permiso_de_reportes()
    {
        var c = _app.Cliente();
        await LoginAsync(c, "recepcion");
        Assert.Contains("/Cuenta/AccesoDenegado", Destino(await c.GetAsync("/Reportes")));
        Assert.DoesNotContain("Resumen ejecutivo", WebUtility.HtmlDecode(await c.GetStringAsync("/panel")));   // el menú no lo ofrece
    }

    [Fact]
    public async Task Todas_las_pantallas_de_reportes_responden_con_datos_reales()
    {
        var c = _app.Cliente();
        await LoginAsync(c, "admin");
        foreach (var url in new[] { "/Reportes", "/Reportes/Socios", "/Reportes/Socios?p=12m", "/Reportes/Ingresos?p=3m",
                     "/Reportes/Ingresos?p=custom&desde=2026-01-01&hasta=2026-03-31", "/Reportes/Planes", "/Reportes/Entrenadores",
                     "/Reportes/Riesgo?nivel=alto", "/Reportes/Exportaciones" })
            Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(url)).StatusCode);

        // El pago anulado (7.777) no suma en la recaudación del resumen ni de ingresos
        var resumen = WebUtility.HtmlDecode(await c.GetStringAsync("/Reportes"));
        Assert.Contains("Lo que está ocurriendo en tu gimnasio", resumen);
        Assert.Contains("$ 1.000", resumen);
        Assert.DoesNotContain("7.777", resumen);
        Assert.DoesNotContain("8.777", WebUtility.HtmlDecode(await c.GetStringAsync("/Reportes/Ingresos")));
    }

    [Fact]
    public async Task Exportar_a_excel_genera_un_xlsx_y_queda_auditado()
    {
        var c = _app.Cliente();
        await LoginAsync(c, "admin");
        var r = await c.GetAsync("/Reportes/Exportaciones?handler=Excel&tipo=Pagos&p=3m");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", r.Content.Headers.ContentType?.MediaType);
        var bytes = await r.Content.ReadAsByteArrayAsync();
        Assert.Equal((byte)'P', bytes[0]);   // un .xlsx es un ZIP
        Assert.Equal((byte)'K', bytes[1]);
        Assert.True(_app.Db(db => db.EventosSistema.Any(e => e.Tipo == TipoEventoSistema.ReporteExportado && e.Usuario == "admin Test" && e.Resumen!.StartsWith("Excel"))));

        var pdf = WebUtility.HtmlDecode(await c.GetStringAsync("/Reportes/Imprimir?tipo=Pagos&p=3m"));
        Assert.Contains("Detalle de pagos", pdf);
        Assert.Contains("Anulado ($ 7.777)", pdf);   // el anulado figura en el detalle, pero con importe 0 en el total
    }

    [Fact]
    public async Task Cerrar_sesion_queda_auditado_y_corta_el_acceso()
    {
        var c = _app.Cliente();
        await LoginAsync(c, "admin");
        var r = await PostAsync(c, "/Cuenta/Logout", "/panel", new());
        Assert.Contains("/Cuenta/Login", Destino(r));
        Assert.Contains("/Cuenta/Login", Destino(await c.GetAsync("/panel")));
        Assert.True(_app.Db(db => db.EventosSistema.Any(e => e.Tipo == TipoEventoSistema.SesionCerrada && e.Usuario == "admin Test")));
    }
}
