using System.Security.Claims;
using GymCore.Dominio.Entidades;
using GymCore.Web.Componentes;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace GymCore.Tests;

// Reglas de seguridad sin infraestructura: política de contraseñas, bloqueo, sello de sesión, roles y permisos.
public class SeguridadTests
{
    private static readonly DateTime Ahora = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    private static Usuario NuevoUsuario(Rol? rol = null)
    {
        var u = new Usuario("Ana Pérez", "  Ana.Perez ", Autenticacion.Hash("Segura123"), 1, debeCambiarPassword: false);
        u.Rol = rol ?? new Rol { Id = 1, Nombre = "Test" };
        return u;
    }

    // ---------- Política de contraseñas ----------
    [Theory]
    [InlineData("corta1")]          // menos de 8
    [InlineData("sinnumeros")]      // sin número
    [InlineData("12345678")]        // sin letra y común
    [InlineData("password1")]       // común
    [InlineData("anaperez2026")]    // contiene el usuario
    public void Politica_rechaza_contrasenas_debiles(string password) =>
        Assert.NotEmpty(PoliticaPassword.Validar(password, "anaperez"));

    [Fact]
    public void Politica_acepta_una_contrasena_razonable() =>
        Assert.Empty(PoliticaPassword.Validar("Mostrador-2026", "anaperez"));

    [Fact]
    public void Las_contrasenas_temporales_cumplen_la_politica_y_no_se_repiten()
    {
        var generadas = Enumerable.Range(0, 200).Select(_ => PoliticaPassword.GenerarTemporal()).ToList();
        Assert.All(generadas, p => Assert.Empty(PoliticaPassword.Validar(p, "usuario")));
        Assert.True(generadas.Distinct().Count() > 190);
    }

    // ---------- Hash ----------
    [Fact]
    public void La_contrasena_se_guarda_como_hash_verificable()
    {
        var u = NuevoUsuario();
        Assert.DoesNotContain("Segura123", u.PasswordHash);
        Assert.NotEqual(PasswordVerificationResult.Failed, Autenticacion.Verificar(u, "Segura123"));
        Assert.Equal(PasswordVerificationResult.Failed, Autenticacion.Verificar(u, "segura123"));
        Assert.NotEqual(Autenticacion.Hash("Segura123"), Autenticacion.Hash("Segura123")); // sal aleatoria
    }

    // ---------- Usuario: bloqueo y sello ----------
    [Fact]
    public void El_nombre_de_usuario_se_normaliza() => Assert.Equal("ana.perez", NuevoUsuario().NombreUsuario);

    [Fact]
    public void Se_bloquea_al_alcanzar_el_maximo_de_intentos_y_expira()
    {
        var u = NuevoUsuario();
        for (var i = 0; i < 4; i++) Assert.False(u.RegistrarIntentoFallido(Ahora, 5, TimeSpan.FromMinutes(15)));
        Assert.True(u.RegistrarIntentoFallido(Ahora, 5, TimeSpan.FromMinutes(15)));
        Assert.True(u.EstaBloqueado(Ahora.AddMinutes(14)));
        Assert.False(u.EstaBloqueado(Ahora.AddMinutes(16)));
    }

    [Fact]
    public void Mientras_esta_bloqueado_los_intentos_no_extienden_el_bloqueo()
    {
        var u = NuevoUsuario();
        for (var i = 0; i < 5; i++) u.RegistrarIntentoFallido(Ahora, 5, TimeSpan.FromMinutes(15));
        var hasta = u.BloqueadoHasta;
        Assert.False(u.RegistrarIntentoFallido(Ahora.AddMinutes(5), 5, TimeSpan.FromMinutes(15)));
        Assert.Equal(hasta, u.BloqueadoHasta);
    }

    [Fact]
    public void El_acceso_exitoso_reinicia_intentos_y_guarda_el_acceso_anterior()
    {
        var u = NuevoUsuario();
        u.RegistrarAccesoExitoso(Ahora);
        u.RegistrarIntentoFallido(Ahora.AddHours(1), 5, TimeSpan.FromMinutes(15));
        u.RegistrarAccesoExitoso(Ahora.AddDays(1));
        Assert.Equal(0, u.IntentosFallidos);
        Assert.Equal(Ahora, u.AccesoAnterior);
        Assert.Equal(Ahora.AddDays(1), u.UltimoAcceso);
    }

    [Fact]
    public void Cambiar_contrasena_rol_o_estado_invalida_las_sesiones()
    {
        var u = NuevoUsuario();
        var s0 = u.SelloSeguridad;
        u.CambiarPassword(Autenticacion.Hash("Otra12345"));
        var s1 = u.SelloSeguridad;
        u.CambiarRol(2);
        var s2 = u.SelloSeguridad;
        u.Desactivar();
        Assert.Equal(4, new[] { s0, s1, s2, u.SelloSeguridad }.Distinct().Count());
        // Rehash transparente: no cierra sesiones
        var s3 = u.SelloSeguridad;
        u.ActualizarHash(Autenticacion.Hash("Otra12345"));
        Assert.Equal(s3, u.SelloSeguridad);
    }

    [Fact]
    public void El_reseteo_obliga_a_cambiar_y_desbloquea()
    {
        var u = NuevoUsuario();
        for (var i = 0; i < 5; i++) u.RegistrarIntentoFallido(Ahora, 5, TimeSpan.FromMinutes(15));
        u.ResetearPassword(Autenticacion.Hash("Temp-1234"));
        Assert.True(u.DebeCambiarPassword);
        Assert.False(u.EstaBloqueado(Ahora));
        u.CambiarPassword(Autenticacion.Hash("Propia-2026"));
        Assert.False(u.DebeCambiarPassword);
    }

    // ---------- Roles y catálogo ----------
    [Fact]
    public void Los_roles_iniciales_solo_usan_permisos_del_catalogo() =>
        Assert.All(RolesIniciales.Todos.SelectMany(r => r.Permisos), p => Assert.Contains(p, Permisos.Codigos));

    [Fact]
    public void Matriz_inicial_de_permisos()
    {
        string[] De(string rol) => RolesIniciales.Todos.Single(r => r.Nombre == rol).Permisos;
        var recepcion = De("Recepcionista");
        var supervisor = De("Supervisor");
        var entrenador = De("Entrenador");

        Assert.Contains(Permisos.PagosCrear, recepcion);
        Assert.Contains(Permisos.CredencialesEmitir, recepcion);
        Assert.DoesNotContain(Permisos.AuditoriaVer, recepcion);
        Assert.DoesNotContain(Permisos.UsuariosAdministrar, recepcion);
        Assert.DoesNotContain(Permisos.ConfiguracionAdministrar, recepcion);

        Assert.Subset(supervisor.ToHashSet(), recepcion.ToHashSet());
        Assert.Contains(Permisos.AuditoriaVer, supervisor);
        Assert.Contains(Permisos.ReportesVer, supervisor);
        Assert.DoesNotContain(Permisos.UsuariosAdministrar, supervisor);

        Assert.Contains(Permisos.SociosVerAsignados, entrenador);
        Assert.Contains(Permisos.EvolucionRegistrar, entrenador);
        Assert.DoesNotContain(Permisos.PagosVer, entrenador);
        Assert.DoesNotContain(Permisos.SociosVer, entrenador);

        Assert.True(RolesIniciales.Todos.Single(r => r.Nombre == "Administrador").AccesoTotal);
    }

    [Fact]
    public void Asignar_permisos_reemplaza_el_conjunto()
    {
        var rol = new Rol();
        rol.AsignarPermisos(new[] { Permisos.SociosVer, Permisos.PagosVer });
        rol.AsignarPermisos(new[] { Permisos.PagosVer, Permisos.PagosCrear, Permisos.PagosCrear });
        Assert.Equal(new[] { Permisos.PagosCrear, Permisos.PagosVer }, rol.Permisos.Select(p => p.Permiso).Order());
        Assert.True(new Rol { AccesoTotal = true }.Tiene(Permisos.ConfiguracionAdministrar));
    }

    // ---------- IUsuarioActual desde la sesión ----------
    private static UsuarioActual Actual(ClaimsPrincipal? p) =>
        new(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = p ?? new ClaimsPrincipal() } });

    [Fact]
    public void Los_permisos_salen_del_rol_y_no_de_su_nombre()
    {
        var rol = new Rol { Nombre = "Administrador" };          // el nombre no concede nada
        rol.AsignarPermisos(new[] { Permisos.SociosVer });
        var u = NuevoUsuario(rol);
        var actual = Actual(Autenticacion.CrearPrincipal(u));

        Assert.True(actual.Autenticado);
        Assert.Equal("Ana Pérez", actual.Nombre);
        Assert.True(actual.Puede(Permisos.SociosVer));
        Assert.False(actual.Puede(Permisos.UsuariosAdministrar));
        Assert.True(actual.PuedeAlguno(Permisos.PagosVer, Permisos.SociosVer));
    }

    [Fact]
    public void Acceso_total_concede_todo_el_catalogo()
    {
        var actual = Actual(Autenticacion.CrearPrincipal(NuevoUsuario(new Rol { Nombre = "X", AccesoTotal = true })));
        Assert.All(Permisos.Codigos, p => Assert.True(actual.Puede(p)));
    }

    [Fact]
    public void Sin_sesion_el_responsable_es_Sistema_y_no_puede_nada()
    {
        var actual = Actual(null);
        Assert.False(actual.Autenticado);
        Assert.Null(actual.UsuarioId);
        Assert.Equal("Sistema", actual.Nombre);
        Assert.False(actual.Puede(Permisos.SociosVer));
    }
}
