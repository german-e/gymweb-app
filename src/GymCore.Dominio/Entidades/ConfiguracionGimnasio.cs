namespace GymCore.Dominio.Entidades;

// Identidad y preferencias de un gimnasio (branding, datos de contacto y modo kiosco).
// Hoy hay una sola fila; el servicio de branding resuelve "cuál" corresponde al request, de modo que el paso a
// multi sucursal / multi gimnasio / white label es agregar la clave del tenant sin tocar las vistas.
public class ConfiguracionGimnasio
{
    public int Id { get; set; }

    // ---------- Identidad ----------
    public string NombreComercial { get; set; } = "GymCore";
    public string? Eslogan { get; set; }                 // subtítulo bajo el nombre (sidebar, login, credencial)
    public string? LogoUrl { get; set; }                 // logo completo (login, reportes, kiosco, credencial)
    public string? LogoReducidoUrl { get; set; }         // isotipo cuadrado (sidebar, favicon de respaldo)
    public string? FaviconUrl { get; set; }
    public string? ImagenBienvenidaUrl { get; set; }     // panel lateral del login y fondo del kiosco
    public string ColorPrincipal { get; set; } = ColorPrincipalPorDefecto;
    public string ColorSecundario { get; set; } = ColorSecundarioPorDefecto;

    // ---------- Datos de contacto (credenciales, tickets y reportes) ----------
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? SitioWeb { get; set; }
    public string? TextoCredencial { get; set; }         // ej. "Presentá tu credencial al ingresar"

    // ---------- Kiosco de ingreso / pantalla de recepción ----------
    public string? KioscoMensaje { get; set; }           // por defecto "Escanee su credencial"
    public int KioscoSegundosResultado { get; set; } = 6;
    public bool KioscoSonido { get; set; } = true;
    public bool KioscoPantallaEspera { get; set; }       // rota avisos y promociones mientras no hay ingresos
    public int KioscoSegundosInactividad { get; set; } = 30;
    public int KioscoSegundosRotacion { get; set; } = 8;

    public DateTime? FechaModificacion { get; set; }     // UTC
    public string? UsuarioModifico { get; set; }

    public const string ColorPrincipalPorDefecto = "#2563EB";
    public const string ColorSecundarioPorDefecto = "#4F46E5";
}

public enum TipoAvisoRecepcion { Aviso = 1, Promocion = 2, Evento = 3 }

// Contenido que rota en la pantalla de espera del kiosco
public class AvisoRecepcion
{
    public int Id { get; set; }
    public TipoAvisoRecepcion Tipo { get; set; } = TipoAvisoRecepcion.Aviso;
    public string Titulo { get; set; } = null!;
    public string? Texto { get; set; }
    public string? ImagenUrl { get; set; }
    public DateTime? FechaEvento { get; set; }           // local; solo eventos
    public DateOnly? VigenteDesde { get; set; }
    public DateOnly? VigenteHasta { get; set; }
    public bool Activo { get; set; } = true;
    public int Orden { get; set; }
    public DateTime FechaAlta { get; set; } = DateTime.UtcNow;

    public bool VigenteEn(DateOnly dia) =>
        Activo && (VigenteDesde is null || VigenteDesde <= dia) && (VigenteHasta is null || VigenteHasta >= dia)
        && (Tipo != TipoAvisoRecepcion.Evento || FechaEvento is null || DateOnly.FromDateTime(FechaEvento.Value) >= dia);
}
