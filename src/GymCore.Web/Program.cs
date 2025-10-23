using Microsoft.EntityFrameworkCore;
using GymCore.Infraestructura.Persistencia;
using GymCore.Infraestructura.Repositorios;
using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Aplicacion.Abstractiones;
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




// Add services to the container.
builder.Services.AddRazorPages();

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

app.UseAuthorization();

app.MapRazorPages();

// Inicializar base de datos (demo): crea esquema si no hay migraciones
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GymDbContext>();
    try { db.Database.EnsureCreated(); } catch { /* opcional: log */ }

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
            FechaAlta = DateTime.UtcNow
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
app.MapPost("/api/asistencias/checkin", async (GymDbContext db, CheckInRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Code))
        return Results.BadRequest(new { ok = false, mensaje = "Código vacío" });

    var code = req.Code.Trim();

    // Por ahora usamos DNI como código de acceso
    var socio = await db.Socios
        .Include(s => s.Suscripciones)
            .ThenInclude(sp => sp.Plan)
        .FirstOrDefaultAsync(s => s.Dni == code);

    if (socio is null)
        return Results.Ok(new { ok = false, mensaje = "Socio no encontrado" });

    var ahora = DateTime.Now; // horario local del gym
    var activo = socio.Suscripciones
        .Where(sp => sp.Estado == EstadoSuscripcion.Activo
                   && sp.FechaInicio <= ahora
                   && sp.FechaFin >= ahora)
        .OrderByDescending(sp => sp.FechaFin)
        .FirstOrDefault();

    if (activo is null)
        return Results.Ok(new { ok = false, socio = new { socio.Id, nombre = $"{socio.Apellido}, {socio.Nombre}" }, mensaje = "Suscripción no activa o vencida" });

    // Días permitidos
    var dow = ahora.DayOfWeek;
    if (dow == DayOfWeek.Saturday && !activo.Plan.PermiteAccesoSabado)
        return Results.Ok(new { ok = false, socio = new { socio.Id, nombre = $"{socio.Apellido}, {socio.Nombre}" }, mensaje = "El plan no permite acceso los sábados" });
    if (dow == DayOfWeek.Sunday && !activo.Plan.PermiteAccesoDomingo)
        return Results.Ok(new { ok = false, socio = new { socio.Id, nombre = $"{socio.Apellido}, {socio.Nombre}" }, mensaje = "El plan no permite acceso los domingos" });

    // Duplicado reciente (5 minutos)
    var hace5 = ahora.AddMinutes(-5);
    var duplicado = await db.Asistencias
        .AnyAsync(a => a.SocioId == socio.Id && a.FechaHoraIngreso >= hace5);
    if (duplicado)
        return Results.Ok(new { ok = true, socio = new { socio.Id, nombre = $"{socio.Apellido}, {socio.Nombre}" }, mensaje = "Ingreso ya registrado recientemente" });

    // Tope semanal si corresponde
    if (activo.Plan.Tipo == TipoPlan.MensualDiasPorSemana && activo.Plan.MaxDiasPorSemana.HasValue)
    {
        var inicioSemana = ahora.Date.AddDays(-(int)(dow == 0 ? 6 : dow - DayOfWeek.Monday)); // lunes
        var finSemana = inicioSemana.AddDays(7);
        var usos = await db.Asistencias
            .CountAsync(a => a.SocioId == socio.Id && a.SocioPlanId == activo.Id && a.FechaHoraIngreso >= inicioSemana && a.FechaHoraIngreso < finSemana);
        if (usos >= activo.Plan.MaxDiasPorSemana.Value)
        {
            if (req.ConfirmExtra != true)
            {
                return Results.Ok(new
                {
                    ok = false,
                    requiresExtra = true,
                    socio = new { socio.Id, nombre = $"{socio.Apellido}, {socio.Nombre}" },
                    extraPrice = (decimal?)null,
                    mensaje = $"Cupo semanal excedido ({usos}/{activo.Plan.MaxDiasPorSemana}). Confirmar para registrar ingreso con cargo extra."
                });
            }
        }
    }

    var asistencia = new Asistencia
    {
        SocioId = socio.Id,
        SocioPlanId = activo.Id,
        FechaHoraIngreso = DateTime.UtcNow
    };
    db.Asistencias.Add(asistencia);
    await db.SaveChangesAsync();

    return Results.Ok(new { ok = true, socio = new { socio.Id, nombre = $"{socio.Apellido}, {socio.Nombre}" }, mensaje = "Ingreso registrado" });
})
.WithName("CheckIn")
.Produces(200);

app.Run();

// DTOs mínimos
public record CheckInRequest(string Code, bool? ConfirmExtra);
