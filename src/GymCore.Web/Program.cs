using Microsoft.EntityFrameworkCore;
using GymCore.Infraestructura.Persistencia;
using GymCore.Infraestructura.Repositorios;
using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Aplicacion.Abstractiones;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Data.Sqlite;
using GymCore.Web.Seguridad;
// Ensure this using matches the actual namespace where ISocioRepository is defined

var builder = WebApplication.CreateBuilder(args);


var cs = builder.Configuration.GetConnectionString("Default")
         ?? "Data Source=./data/gym.db";
// Normaliza ruta relativa de SQLite a absoluta y asegura carpeta existente
if (cs.StartsWith("Data Source=./"))
{
    var relative = cs.Substring("Data Source=".Length).Trim();
    // limpiar prefijo ./ o .\
    while (relative.StartsWith("./") || relative.StartsWith(".\\"))
        relative = relative.Substring(2);
    var absPath = Path.Combine(builder.Environment.ContentRootPath, relative);
    var dir = Path.GetDirectoryName(absPath);
    if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
    cs = $"Data Source={absPath}";
}

builder.Services.AddDbContext<GymDbContext>(opt => opt.UseSqlite(cs));

// DI repositorios/servicios
builder.Services.AddScoped<ISocioRepository, SocioRepository>();
// ---------- Seguridad: autenticación por cookie, autorización por permisos ----------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<GymCore.Web.Componentes.IUsuarioActual, GymCore.Web.Componentes.UsuarioActual>();
// La persistencia completa el responsable de la auditoría con el usuario autenticado
builder.Services.AddScoped<IOperadorAuditoria>(sp => sp.GetRequiredService<GymCore.Web.Componentes.IUsuarioActual>());
builder.Services.Configure<OpcionesSeguridad>(builder.Configuration.GetSection("Seguridad"));
builder.Services.AddScoped<Autenticacion>();

// ---------- Inteligencia de negocio: una fuente de hechos por request y métricas centralizadas ----------
builder.Services.AddScoped<GymCore.Web.Reportes.IFuenteHechos, GymCore.Web.Reportes.FuenteHechosDb>();
builder.Services.AddScoped<GymCore.Web.Reportes.ServicioReportes>();
builder.Services.AddScoped<GymCore.Web.Reportes.Exportador>();
// Control de ingreso (recepción y kiosco) y branding del gimnasio
builder.Services.AddScoped<GymCore.Web.Componentes.ServicioIngreso>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<GymCore.Web.Marca.ServicioBranding>();
// Ventas e inventario: servicios independientes (inventario, cobranza, ventas, reportes comerciales)
builder.Services.AddScoped<GymCore.Web.Comercial.ServicioInventario>();
builder.Services.AddScoped<GymCore.Web.Comercial.ServicioCobranza>();
builder.Services.AddScoped<GymCore.Web.Comercial.ServicioVentas>();
builder.Services.AddScoped<GymCore.Web.Comercial.ReportesComerciales>();

var opcionesSeguridad = builder.Configuration.GetSection("Seguridad").Get<OpcionesSeguridad>() ?? new OpcionesSeguridad();
builder.Services.AddAuthentication(Autenticacion.Esquema)
    .AddCookie(Autenticacion.Esquema, o =>
    {
        o.Cookie.Name = ".GymCore.Sesion";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;   // Always en producción con HTTPS
        o.LoginPath = "/Cuenta/Login";
        o.LogoutPath = "/Cuenta/Logout";
        o.AccessDeniedPath = "/Cuenta/AccesoDenegado";
        o.ExpireTimeSpan = TimeSpan.FromHours(opcionesSeguridad.HorasSesion);   // inactividad máxima
        o.SlidingExpiration = true;
        o.Events.OnValidatePrincipal = Autenticacion.ValidarSesionAsync;
        // La API responde con códigos HTTP en lugar de redirigir al login
        o.Events.OnRedirectToLogin = c => { if (c.Request.Path.StartsWithSegments("/api")) c.Response.StatusCode = 401; else c.Response.Redirect(c.RedirectUri); return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = c => { if (c.Request.Path.StartsWithSegments("/api")) c.Response.StatusCode = 403; else c.Response.Redirect(c.RedirectUri); return Task.CompletedTask; };
    });

builder.Services.AddAuthorization(o =>
{
    // Todo requiere sesión salvo lo marcado como anónimo (login, configuración inicial, errores)
    o.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    // Una política por permiso (nombre = código), para endpoints y convenciones
    foreach (var permiso in Permisos.Codigos)
        o.AddPolicy(permiso, p => p.RequireAuthenticatedUser().RequireAssertion(c =>
            c.User.HasClaim(Reclamos.AccesoTotal, "true") || c.User.HasClaim(Reclamos.Permiso, permiso)));
});

builder.Services.AddRazorPages(o =>
{
    o.Conventions.AllowAnonymousToPage("/Cuenta/Login");
    o.Conventions.AllowAnonymousToPage("/Cuenta/Configurar");
    o.Conventions.AllowAnonymousToPage("/Error");
    // Página sin PageModel: se protege por convención
    o.Conventions.AuthorizePage("/Asistencias/Ingreso", Permisos.AsistenciasRegistrar);
}).AddMvcOptions(o => o.Filters.Add<FiltroPermisos>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// Inicializar base de datos (demo): crea esquema si no hay migraciones
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GymDbContext>();
    try { db.Database.EnsureCreated(); } catch { /* opcional: log */ }

    // Ajuste de esquema en SQLite: agregar columna TarjetaCodigo si falta e índice único
    try
    {
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info('Socios')";
            using var reader = await cmd.ExecuteReaderAsync();
            var tieneTarjeta = false;
            while (await reader.ReadAsync())
            {
                // PRAGMA table_info: 0:cid,1:name,2:type,...
                var col = reader.GetString(1);
                if (col == "TarjetaCodigo") { tieneTarjeta = true; break; }
            }
            if (!tieneTarjeta)
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Socios\" ADD COLUMN \"TarjetaCodigo\" TEXT NULL;");
                await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Socios_TarjetaCodigo\" ON \"Socios\" (\"TarjetaCodigo\");");
                // Inicial: usar DNI como código de tarjeta
                await db.Database.ExecuteSqlRawAsync("UPDATE \"Socios\" SET \"TarjetaCodigo\" = \"Dni\" WHERE \"TarjetaCodigo\" IS NULL;");
            }
        }
    }
    catch { /* best-effort */ }

    // Alinea una base existente con el modelo: columnas nuevas (nullable) y tablas nuevas
    try { await EsquemaSqlite.ActualizarAsync(db); } catch { /* best-effort */ }

    // Roles iniciales: se crean si faltan (no se pisan los cambios hechos desde Administración > Roles)
    foreach (var def in RolesIniciales.Todos)
    {
        if (await db.Roles.AnyAsync(r => r.Nombre == def.Nombre)) continue;
        var rol = new Rol { Nombre = def.Nombre, Descripcion = def.Descripcion, AccesoTotal = def.AccesoTotal, EsSistema = true };
        rol.AsignarPermisos(def.Permisos);
        db.Roles.Add(rol);
    }
    await db.SaveChangesAsync();

    // Categorías de productos por defecto (solo si no hay ninguna)
    try { await GymCore.Web.Comercial.InventarioPresentacion.SembrarCategoriasAsync(db); } catch { /* best-effort */ }

    // Seed mínimo para pruebas de check-in
    if (!db.Planes.Any())
    {
        var plan3x = new Plan
        {
            Nombre = "Mensual 3xSemana",
            Tipo = TipoPlan.MensualDiasPorSemana,
            Precio = 20000,
            Moneda = "ARS",
            MaxDiasPorSemana = 3,
            DuracionDias = 30,
            PermiteAccesoSabado = true,
            PermiteAccesoDomingo = false
        };
        var planFull = new Plan
        {
            Nombre = "Mensual Ilimitado",
            Tipo = TipoPlan.MensualIlimitado,
            Precio = 28000,
            Moneda = "ARS",
            DuracionDias = 30,
            PermiteAccesoSabado = true,
            PermiteAccesoDomingo = true
        };
        db.Planes.AddRange(plan3x, planFull);
        await db.SaveChangesAsync();
    }

    if (!db.Socios.Any())
    {
        var socio = new Socio
        {
            Nombre = "Juan",
            Apellido = "Pérez",
            Dni = "12345678",
            FechaAlta = DateTime.UtcNow,
            TarjetaCodigo = "12345678"
        };
        db.Socios.Add(socio);
        await db.SaveChangesAsync();

        var plan = await db.Planes.FirstAsync(p => p.Tipo == TipoPlan.MensualDiasPorSemana);
        var hoyLocal = DateTime.Now.Date;
        var sp = new SocioPlan
        {
            SocioId = socio.Id,
            PlanId = plan.Id,
            FechaInicio = hoyLocal,
            FechaFin = hoyLocal.AddDays(plan.DuracionDias),
            Estado = EstadoSuscripcion.Activo
        };
        db.SocioPlanes.Add(sp);
        await db.SaveChangesAsync();
    }
}

// Minimal API: Check-in de asistencias con lector tipo teclado (DNI/código)
app.MapPost("/api/asistencias/checkin", async (GymCore.Web.Componentes.ServicioIngreso ingreso, CheckInRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Code))
        return Results.BadRequest(new { ok = false, mensaje = "Código vacío" });
    // Reglas en ServicioIngreso (compartidas con el kiosco); la respuesta JSON es la misma de siempre
    var r = await ingreso.RegistrarAsync(req.Code, req.ConfirmExtra == true);
    return Results.Ok(GymCore.Web.Componentes.ServicioIngreso.RespuestaApi(r));
})
.RequireAuthorization(Permisos.AsistenciasRegistrar)
.WithName("CheckIn")
.Produces(200);

app.Run();

// Visible para las pruebas de integración (WebApplicationFactory)
public partial class Program { }

// DTOs mínimos
public record CheckInRequest(string Code, bool? ConfirmExtra);
