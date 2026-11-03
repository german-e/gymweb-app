using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Options;

namespace GymCore.Web.Seguridad;

// Cabeceras HTTP de seguridad en todas las respuestas (OWASP Secure Headers):
//  - Content-Security-Policy con nonce por request: el navegador solo ejecuta los <script> que emite la app
//    (llevan el nonce) y los de los CDN permitidos. Un script inyectado por XSS no conoce el nonce y no corre.
//  - X-Content-Type-Options, X-Frame-Options/frame-ancestors, Referrer-Policy, Permissions-Policy, COOP.
// El nonce lo agrega NonceScriptTagHelper a cada <script> de las vistas, sin tocar cada página.
public static class CabecerasSeguridad
{
    private const string ClaveNonce = "gym:csp-nonce";

    public static string? Nonce(HttpContext http) => http.Items[ClaveNonce] as string;

    public static IApplicationBuilder UseCabecerasSeguridad(this IApplicationBuilder app)
    {
        var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
        var opciones = app.ApplicationServices.GetRequiredService<IOptions<OpcionesSeguridad>>().Value;
        var modo = opciones.Csp ?? (env.IsDevelopment() ? ModoCsp.SoloReporte : ModoCsp.Activa);
        var produccion = !env.IsDevelopment();

        return app.Use(async (http, next) =>
        {
            // Hexadecimal: 128 bits aleatorios sin "+", "/" ni "=". En Base64 el "+" se escribe en el HTML como
            // "&#x2B;" (el navegador lo decodifica bien, pero el HTML no coincide textualmente con la cabecera).
            var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            http.Items[ClaveNonce] = nonce;

            var h = http.Response.Headers;
            h["X-Content-Type-Options"] = "nosniff";
            h["X-Frame-Options"] = "DENY";
            h["Referrer-Policy"] = "strict-origin-when-cross-origin";
            h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
            h["Cross-Origin-Opener-Policy"] = "same-origin";
            // upgrade-insecure-requests solo cuando la request llegó por HTTPS (detrás de Caddy o Cloudflare):
            // así una prueba local por http://localhost:8080 no rompe la carga de CSS y JS
            var forzarHttps = produccion && http.Request.IsHttps;
            if (modo == ModoCsp.Activa) h["Content-Security-Policy"] = Politica(nonce, forzarHttps);
            else if (modo == ModoCsp.SoloReporte) h["Content-Security-Policy-Report-Only"] = Politica(nonce, forzarHttps);

            await next();
        });
    }

    // Orígenes externos que usa la app hoy: jsDelivr (Bootstrap, jQuery, íconos), cdnjs (html2canvas y jsPDF
    // en el recibo) y Google Fonts. Estilos: 'unsafe-inline' porque las vistas usan atributos style="...".
    public static string Politica(string nonce, bool forzarHttps) => string.Join("; ", new[]
    {
        "default-src 'self'",
        $"script-src 'self' 'nonce-{nonce}' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com",
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://fonts.googleapis.com",
        "font-src 'self' data: https://fonts.gstatic.com https://cdn.jsdelivr.net",
        "img-src 'self' data: blob:",
        "connect-src 'self'",
        "object-src 'none'",
        "base-uri 'self'",
        "form-action 'self'",
        "frame-ancestors 'none'",
        forzarHttps ? "upgrade-insecure-requests" : ""
    }.Where(d => d.Length > 0));
}

// Agrega el nonce de la CSP a todos los <script> de las vistas Razor (registrado en _ViewImports).
[HtmlTargetElement("script")]
public sealed class NonceScriptTagHelper : TagHelper
{
    [ViewContext, HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override int Order => int.MaxValue;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (CabecerasSeguridad.Nonce(ViewContext.HttpContext) is { } nonce && !output.Attributes.ContainsName("nonce"))
            output.Attributes.SetAttribute("nonce", nonce);
    }
}
