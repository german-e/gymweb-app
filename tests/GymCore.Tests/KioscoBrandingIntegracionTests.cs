using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Tests;

// Kiosco de ingreso, branding y navegación de punta a punta (aplicación real por HTTP).
[Collection(AppDePrueba.Coleccion)]
public class KioscoBrandingIntegracionTests : IClassFixture<AppDePrueba>
{
    private readonly AppDePrueba _app;
    public KioscoBrandingIntegracionTests(AppDePrueba app) => _app = app;

    private static string Token(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

    private static async Task<HttpResponseMessage> PostAsync(HttpClient c, string url, string pagina, Dictionary<string, string> datos)
    {
        datos["__RequestVerificationToken"] = Token(await c.GetStringAsync(pagina));
        return await c.PostAsync(url, new FormUrlEncodedContent(datos));
    }

    private async Task<HttpClient> SesionAsync(string usuario)
    {
        var c = _app.Cliente();
        await PostAsync(c, "/Cuenta/Login", "/Cuenta/Login", new() { ["NombreUsuario"] = usuario, ["Password"] = AppDePrueba.Clave });
        return c;
    }

    private static string? Destino(HttpResponseMessage r) => r.Headers.Location?.OriginalString;

    private string SocioConPlan(string dni, bool vigente)
    {
        _app.Db(db =>
        {
            var plan = new Plan { Nombre = "Libre " + dni, Tipo = TipoPlan.MensualIlimitado, Precio = 1000, DuracionDias = 30, PermiteAccesoSabado = true, PermiteAccesoDomingo = true };
            var s = new Socio { Nombre = "Kiosco", Apellido = "Socio" + dni, Dni = dni };
            db.SocioPlanes.Add(new SocioPlan
            {
                Socio = s, Plan = plan, Estado = EstadoSuscripcion.Activo,
                FechaInicio = DateTime.Today.AddDays(vigente ? -1 : -60), FechaFin = DateTime.Today.AddDays(vigente ? 29 : -30)
            });
            return db.SaveChanges();
        });
        return dni;
    }

    private static async Task<JsonElement> IngresoAsync(HttpClient c, string token, string codigo)
    {
        var r = await c.PostAsync("/kiosk/checkin?handler=Ingreso", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["codigo"] = codigo, ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;
    }

    [Fact]
    public async Task El_kiosco_autoriza_restringe_y_deniega_a_pantalla_completa()
    {
        var vigente = SocioConPlan("50000001", vigente: true);
        var vencido = SocioConPlan("50000002", vigente: false);
        var c = await SesionAsync("recepcion");
        var html = WebUtility.HtmlDecode(await c.GetStringAsync("/kiosk/checkin"));
        Assert.Contains("Escanee su credencial", html);
        Assert.Contains("Listo para escanear", html);
        Assert.DoesNotContain("class=\"sidebar\"", html);   // sin menú ni barra superior
        Assert.DoesNotContain("class=\"topbar\"", html);
        var token = Token(html);

        var ok = await IngresoAsync(c, token, vigente);
        Assert.Equal("autorizado", ok.GetProperty("estado").GetString());
        Assert.Equal("ACCESO AUTORIZADO", ok.GetProperty("titulo").GetString());
        Assert.Equal("Libre 50000001", ok.GetProperty("socio").GetProperty("plan").GetString());
        Assert.Equal(DateTime.Today.AddDays(29).ToString("dd/MM/yyyy"), ok.GetProperty("socio").GetProperty("vence").GetString());

        var otra = await IngresoAsync(c, token, vigente);
        Assert.Equal("autorizado", otra.GetProperty("estado").GetString());
        Assert.Contains("ya estaba registrado", otra.GetProperty("nota").GetString());
        Assert.Equal(1, _app.Db(db => db.Asistencias.Count(a => a.Socio.Dni == vigente)));

        var restringido = await IngresoAsync(c, token, vencido);
        Assert.Equal("restringido", restringido.GetProperty("estado").GetString());
        Assert.Contains("venció", restringido.GetProperty("mensaje").GetString());
        Assert.Equal("Diríjase a recepción", restringido.GetProperty("nota").GetString());

        var denegado = await IngresoAsync(c, token, "no-existe");
        Assert.Equal("denegado", denegado.GetProperty("estado").GetString());
        Assert.Equal("Código no válido", denegado.GetProperty("mensaje").GetString());
        Assert.Equal(JsonValueKind.Null, denegado.GetProperty("socio").ValueKind);
    }

    [Fact]
    public async Task El_kiosco_requiere_sesion_y_permiso_de_control_de_ingreso()
    {
        var anonimo = await _app.Cliente().GetAsync("/kiosk/checkin");
        Assert.Contains("/Cuenta/Login", Destino(anonimo));
        var entrenador = await SesionAsync("entrenador");
        Assert.Contains("/Cuenta/AccesoDenegado", Destino(await entrenador.GetAsync("/kiosk/checkin")));
    }

    [Fact]
    public async Task La_api_de_recepcion_responde_igual_que_antes()
    {
        var c = await SesionAsync("recepcion");
        var r = await c.PostAsync("/api/asistencias/checkin", new StringContent("{\"code\":\"no-existe\"}", System.Text.Encoding.UTF8, "application/json"));
        var json = JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;
        Assert.False(json.GetProperty("ok").GetBoolean());
        Assert.Equal("Socio no encontrado", json.GetProperty("mensaje").GetString());
        var vacio = await c.PostAsync("/api/asistencias/checkin", new StringContent("{\"code\":\"\"}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, vacio.StatusCode);
    }

    [Fact]
    public async Task El_branding_se_aplica_en_login_menu_y_credencial_y_queda_auditado()
    {
        var admin = await SesionAsync("admin");
        var r = await PostAsync(admin, "/Configuracion/Branding", "/Configuracion/Branding", new()
        {
            ["Input.NombreComercial"] = "Titán Fitness Club", ["Input.Eslogan"] = "Entrená distinto",
            ["Input.ColorPrincipal"] = "#e11d48", ["Input.ColorSecundario"] = "#F97316",
            ["Input.Telefono"] = "11 5555-0000", ["Input.TextoCredencial"] = "Mostrá tu credencial"
        });
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);

        var login = WebUtility.HtmlDecode(await _app.Cliente().GetStringAsync("/Cuenta/Login"));
        Assert.Contains("Titán Fitness Club", login);
        Assert.Contains("--primary:#E11D48", login);

        var panel = WebUtility.HtmlDecode(await admin.GetStringAsync("/panel"));
        Assert.Contains("class=\"brand-name\">Titán Fitness Club</span>", panel);
        Assert.Contains("Entrená distinto", panel);

        var ficha = WebUtility.HtmlDecode(await admin.GetStringAsync($"/Socios/Detalle/{_app.SocioAsignado}"));
        Assert.Contains("Mostrá tu credencial", ficha);
        Assert.Contains("11 5555-0000", ficha);

        Assert.True(_app.Db(db => db.EventosSistema.Any(e => e.Tipo == TipoEventoSistema.BrandingModificado && e.EntidadId != null)));

        // Color inválido: no se guarda
        var malo = await PostAsync(admin, "/Configuracion/Branding", "/Configuracion/Branding", new()
        {
            ["Input.NombreComercial"] = "Otro", ["Input.ColorPrincipal"] = "red;}", ["Input.ColorSecundario"] = "#F97316"
        });
        Assert.Equal(HttpStatusCode.OK, malo.StatusCode);
        Assert.Equal("Titán Fitness Club", _app.Db(db => db.ConfiguracionesGimnasio.Single().NombreComercial));
    }

    [Fact]
    public async Task Configuracion_solo_para_quien_la_administra()
    {
        var recepcion = await SesionAsync("recepcion");
        foreach (var url in new[] { "/Configuracion", "/Configuracion/Branding", "/Configuracion/Recepcion" })
            Assert.Contains("/Cuenta/AccesoDenegado", Destino(await recepcion.GetAsync(url)));
        var admin = await SesionAsync("admin");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/Configuracion")).StatusCode);
    }

    [Fact]
    public async Task Avisos_de_recepcion_rotan_en_el_kiosco_solo_si_estan_vigentes()
    {
        var admin = await SesionAsync("admin");
        await PostAsync(admin, "/Configuracion/Recepcion?handler=Kiosco", "/Configuracion/Recepcion", new()
        {
            ["mensaje"] = "Bienvenido al club", ["segundosResultado"] = "5", ["pantallaEspera"] = "true", ["segundosInactividad"] = "20", ["segundosRotacion"] = "6"
        });
        await PostAsync(admin, "/Configuracion/Recepcion?handler=Aviso", "/Configuracion/Recepcion", new()
        {
            ["tipo"] = "Promocion", ["titulo"] = "2x1 en trimestral", ["texto"] = "Solo este mes", ["orden"] = "1"
        });
        await PostAsync(admin, "/Configuracion/Recepcion?handler=Aviso", "/Configuracion/Recepcion", new()
        {
            ["tipo"] = "Aviso", ["titulo"] = "Aviso vencido", ["hasta"] = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd"), ["orden"] = "2"
        });
        var kiosco = WebUtility.HtmlDecode(await admin.GetStringAsync("/kiosk/checkin"));
        Assert.Contains("Bienvenido al club", kiosco);
        Assert.Contains("data-espera=\"1\"", kiosco);
        Assert.Contains("2x1 en trimestral", Regex.Unescape(kiosco));
        Assert.DoesNotContain("Aviso vencido", Regex.Unescape(kiosco));
        Assert.True(_app.Db(db => db.EventosSistema.Count(e => e.Tipo == TipoEventoSistema.AvisoCreado) >= 2));
    }

    [Fact]
    public async Task El_menu_se_adapta_a_cada_rol()
    {
        var entrenador = WebUtility.HtmlDecode(await (await SesionAsync("entrenador")).GetStringAsync("/panel"));
        Assert.Contains(">Mis socios<", entrenador);
        Assert.Contains(">Evolución física<", entrenador);
        Assert.DoesNotContain(">Comercial<", entrenador);
        Assert.DoesNotContain(">Inventario<", entrenador);
        Assert.Contains("sidebar-user", entrenador);   // usuario visible siempre

        var recepcion = WebUtility.HtmlDecode(await (await SesionAsync("recepcion")).GetStringAsync("/panel"));
        Assert.Contains(">Comercial<", recepcion);
        Assert.Contains(">Inventario<", recepcion);
        Assert.DoesNotContain(">Administración<", recepcion);
        Assert.DoesNotContain(">Movimientos<", recepcion);
        Assert.DoesNotContain(">Inteligencia<", recepcion);
    }

    [Fact]
    public async Task Evolucion_fisica_muestra_al_entrenador_solo_sus_socios()
    {
        var c = await SesionAsync("entrenador");
        var html = WebUtility.HtmlDecode(await c.GetStringAsync("/Socios/Evolucion"));
        Assert.Contains("Socio, Asignado", html);
        Assert.DoesNotContain("Socio, Ajeno", html);
        Assert.Contains("/Cuenta/AccesoDenegado", Destino(await (await SesionAsync("recepcion")).GetAsync("/Socios/Evolucion")));
    }
}
