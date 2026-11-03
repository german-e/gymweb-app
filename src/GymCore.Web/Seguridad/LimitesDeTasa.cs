using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace GymCore.Web.Seguridad;

// Límites de tasa (rate limiting) por IP, con el middleware que trae ASP.NET Core.
//  - "login":   POST de login, configuración inicial y cambio de contraseña. Frena la fuerza bruta y el
//               rociado de contraseñas (una clave contra muchos usuarios), y evita que un tercero bloquee
//               a propósito la cuenta del administrador con intentos fallidos.
//  - "ingreso": check-in por API y kiosco. Evita que alguien recorra DNI en el kiosco para ver datos de socios.
// El resto de las requests no se limita. Detrás de Caddy/Cloudflare la IP real llega por X-Forwarded-For
// (ASPNETCORE_FORWARDEDHEADERS_ENABLED=true en el Dockerfile).
public static class LimitesDeTasa
{
    public const string Login = "login";
    public const string Ingreso = "ingreso";

    public static IServiceCollection AddLimitesDeTasa(this IServiceCollection services) =>
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = async (ctx, ct) =>
            {
                var http = ctx.HttpContext;
                if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
                    http.Response.Headers.RetryAfter = ((int)Math.Ceiling(espera.TotalSeconds)).ToString();

                http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("GymCore.Seguridad")
                    .LogWarning("Límite de tasa superado: {Metodo} {Ruta} desde {Ip}",
                        http.Request.Method, http.Request.Path, http.Connection.RemoteIpAddress);

                http.Response.ContentType = "text/plain; charset=utf-8";
                await http.Response.WriteAsync("Demasiados intentos en poco tiempo. Esperá un minuto y volvé a probar.", ct);
            };

            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
            {
                var categoria = Clasificar(http.Request);
                if (categoria is null) return RateLimitPartition.GetNoLimiter("sin-limite");

                var ip = http.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
                return RateLimitPartition.GetFixedWindowLimiter($"{categoria}:{ip}", _ =>
                {
                    var opciones = http.RequestServices.GetRequiredService<IOptions<OpcionesSeguridad>>().Value;
                    return new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = Math.Max(1, categoria == Login ? opciones.IntentosLoginPorMinuto : opciones.IngresosPorMinuto),
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    };
                });
            });
        });

    // Solo se limitan los POST de las rutas sensibles (los GET de esas páginas no cuentan)
    public static string? Clasificar(HttpRequest req)
    {
        if (!HttpMethods.IsPost(req.Method)) return null;
        var ruta = req.Path;
        bool Es(string prefijo) => ruta.StartsWithSegments(prefijo, StringComparison.OrdinalIgnoreCase);

        if (Es("/Cuenta/Login") || Es("/Cuenta/Configurar") || Es("/Cuenta/CambiarPassword")) return Login;
        if (Es("/api/asistencias/checkin") || Es("/Kiosk/Checkin")) return Ingreso;
        return null;
    }
}
