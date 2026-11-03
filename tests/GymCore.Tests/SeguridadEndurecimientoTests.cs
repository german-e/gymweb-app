using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using GymCore.Web.Componentes;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace GymCore.Tests;

// Fase 3 de endurecimiento: cabeceras de seguridad, CSP con nonce, fotos protegidas, firma de imágenes
// y límites de tasa.
[Collection(AppDePrueba.Coleccion)]
public class SeguridadEndurecimientoTests : IClassFixture<AppDePrueba>
{
    private readonly AppDePrueba _app;
    public SeguridadEndurecimientoTests(AppDePrueba app) => _app = app;

    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 1, 2, 3 };

    private static IFormFile Archivo(string nombre, byte[] contenido) =>
        new FormFile(new MemoryStream(contenido), 0, contenido.Length, "f", nombre);

    internal static async Task<HttpResponseMessage> LoginAsync(HttpClient c, string usuario, string clave)
    {
        var html = await c.GetStringAsync("/Cuenta/Login");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        return await c.PostAsync("/Cuenta/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["NombreUsuario"] = usuario, ["Password"] = clave, ["__RequestVerificationToken"] = token
        }));
    }

    [Fact]
    public async Task Las_respuestas_llevan_cabeceras_de_seguridad()
    {
        var r = await _app.Cliente().GetAsync("/Cuenta/Login");

        Assert.Equal("nosniff", r.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", r.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", r.Headers.GetValues("Referrer-Policy").Single());
        Assert.True(r.Headers.Contains("Permissions-Policy"));
        // En Development la CSP va en modo reporte para no romper dotnet watch
        var csp = r.Headers.GetValues("Content-Security-Policy-Report-Only").Single();
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Contains("object-src 'none'", csp);
    }

    [Fact]
    public async Task Cada_script_de_la_pagina_lleva_el_nonce_de_la_csp()
    {
        var r = await _app.Cliente().GetAsync("/Cuenta/Login");
        var csp = r.Headers.GetValues("Content-Security-Policy-Report-Only").Single();
        var nonce = Regex.Match(csp, "'nonce-([^']+)'").Groups[1].Value;
        var html = await r.Content.ReadAsStringAsync();

        var scripts = Regex.Matches(html, "<script[^>]*>").Select(m => m.Value).ToList();
        Assert.NotEmpty(scripts);
        Assert.All(scripts, s => Assert.Contains($"nonce=\"{nonce}\"", s));

        // El nonce cambia en cada request
        var otra = await _app.Cliente().GetAsync("/Cuenta/Login");
        Assert.DoesNotContain(nonce, otra.Headers.GetValues("Content-Security-Policy-Report-Only").Single());
    }

    [Fact]
    public void La_politica_de_produccion_fuerza_https()
    {
        Assert.Contains("upgrade-insecure-requests", CabecerasSeguridad.Politica("abc", forzarHttps: true));
        Assert.DoesNotContain("upgrade-insecure-requests", CabecerasSeguridad.Politica("abc", forzarHttps: false));
    }

    [Fact]
    public async Task Las_fotos_de_socios_exigen_sesion_y_las_imagenes_de_marca_son_publicas()
    {
        Directory.CreateDirectory(Path.Combine(_app.RutaUploads, "socios"));
        Directory.CreateDirectory(Path.Combine(_app.RutaUploads, "branding"));
        await File.WriteAllBytesAsync(Path.Combine(_app.RutaUploads, "socios", "prueba.png"), Png);
        await File.WriteAllBytesAsync(Path.Combine(_app.RutaUploads, "branding", "logo.png"), Png);

        var anonimo = _app.Cliente();
        var foto = await anonimo.GetAsync("/uploads/socios/prueba.png");
        Assert.Equal(HttpStatusCode.Redirect, foto.StatusCode);
        Assert.Contains("/Cuenta/Login", foto.Headers.Location!.OriginalString);

        var logo = await anonimo.GetAsync("/uploads/branding/logo.png");
        Assert.Equal(HttpStatusCode.OK, logo.StatusCode);

        var conSesion = _app.Cliente();
        await LoginAsync(conSesion, "admin", AppDePrueba.Clave);
        var fotoConSesion = await conSesion.GetAsync("/uploads/socios/prueba.png");
        Assert.Equal(HttpStatusCode.OK, fotoConSesion.StatusCode);
        Assert.Equal(Png, await fotoConSesion.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public void Las_fotos_se_validan_por_contenido_y_no_solo_por_extension()
    {
        Assert.Null(FotoStorage.Validar(Archivo("foto.png", Png)));
        Assert.Null(FotoStorage.Validar(Archivo("foto.jpg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0 })));
        Assert.Equal("El archivo no es una imagen válida.",
            FotoStorage.Validar(Archivo("foto.jpg", Encoding.UTF8.GetBytes("<html><script>alert(1)</script>"))));
        Assert.Equal("El archivo no es una imagen válida.", FotoStorage.Validar(Archivo("foto.png", new byte[] { 0xFF, 0xD8, 0xFF })));
        Assert.NotNull(FotoStorage.Validar(Archivo("foto.svg", Encoding.UTF8.GetBytes("<svg/>"))));
    }

    [Fact]
    public void Las_urls_de_archivos_no_permiten_salir_de_la_carpeta()
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Almacenamiento:Uploads"] = _app.RutaUploads }).Build();
        var almacen = new AlmacenamientoArchivos(config, new EntornoFalso());

        Assert.NotNull(almacen.RutaFisica("uploads/socios/a.png"));
        Assert.Null(almacen.RutaFisica("uploads/../appsettings.json"));
        Assert.Null(almacen.RutaFisica("/etc/passwd"));
        Assert.Null(almacen.RutaFisica("otra/socios/a.png"));
        Assert.Throws<ArgumentException>(() => almacen.Carpeta("../fuera"));
    }

    [Theory]
    [InlineData("POST", "/Cuenta/Login", LimitesDeTasa.Login)]
    [InlineData("POST", "/cuenta/login", LimitesDeTasa.Login)]
    [InlineData("POST", "/Cuenta/Configurar", LimitesDeTasa.Login)]
    [InlineData("POST", "/api/asistencias/checkin", LimitesDeTasa.Ingreso)]
    [InlineData("POST", "/Kiosk/Checkin", LimitesDeTasa.Ingreso)]
    [InlineData("GET", "/Cuenta/Login", null)]
    [InlineData("POST", "/Socios/Create", null)]
    public void Solo_se_limitan_los_post_sensibles(string metodo, string ruta, string? esperado)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = metodo;
        ctx.Request.Path = ruta;
        Assert.Equal(esperado, LimitesDeTasa.Clasificar(ctx.Request));
    }

    private sealed class EntornoFalso : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ApplicationName { get; set; } = "GymCore.Tests";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Development";
    }
}

// App con un límite de login bajo, para comprobar que el límite de tasa corta los intentos
public sealed class AppConLimiteDeLogin : AppDePrueba
{
    public AppConLimiteDeLogin() : base(intentosLoginPorMinuto: 3) { }
}

[Collection(AppDePrueba.Coleccion)]
public class LimiteDeTasaIntegracionTests : IClassFixture<AppConLimiteDeLogin>
{
    private readonly AppConLimiteDeLogin _app;
    public LimiteDeTasaIntegracionTests(AppConLimiteDeLogin app) => _app = app;

    [Fact]
    public async Task Superado_el_limite_el_login_responde_429_aunque_la_clave_sea_correcta()
    {
        var c = _app.Cliente();
        for (var i = 0; i < 3; i++)
        {
            var r = await SeguridadEndurecimientoTests.LoginAsync(c, "no-existe", "Incorrecta-1");
            Assert.NotEqual(HttpStatusCode.TooManyRequests, r.StatusCode);
        }

        var bloqueado = await SeguridadEndurecimientoTests.LoginAsync(c, "admin", AppDePrueba.Clave);
        Assert.Equal(HttpStatusCode.TooManyRequests, bloqueado.StatusCode);
        Assert.True(bloqueado.Headers.Contains("Retry-After"));

        // Los GET no cuentan: la pantalla de login se sigue mostrando
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/Cuenta/Login")).StatusCode);
    }
}
