using System.Security.Cryptography;

namespace GymCore.Web.Seguridad;

// Parámetros de seguridad (sección "Seguridad" de appsettings; estos son los valores por defecto).
public class OpcionesSeguridad
{
    public int MaximoIntentosFallidos { get; set; } = 5;
    public int MinutosBloqueo { get; set; } = 15;
    public int HorasSesion { get; set; } = 8;          // inactividad máxima de una sesión normal (deslizante)
    public int DiasRecordarSesion { get; set; } = 14;  // "Recordar sesión": duración fija
}

// Reglas mínimas de contraseña. Se valida igual en el alta, el cambio y el reseteo.
public static class PoliticaPassword
{
    public const int LargoMinimo = 8;
    public const int LargoMaximo = 128;

    private static readonly HashSet<string> Comunes = new(StringComparer.OrdinalIgnoreCase)
    {
        "12345678", "123456789", "1234567890", "password", "password1", "contraseña", "contrasena",
        "qwerty123", "gimnasio", "gimnasio1", "gymcore1", "admin123", "abcd1234"
    };

    public static List<string> Validar(string? password, string? nombreUsuario = null)
    {
        var errores = new List<string>();
        if (string.IsNullOrEmpty(password)) { errores.Add("Ingresá una contraseña."); return errores; }
        if (password.Length < LargoMinimo) errores.Add($"Debe tener al menos {LargoMinimo} caracteres.");
        if (password.Length > LargoMaximo) errores.Add($"Debe tener como máximo {LargoMaximo} caracteres.");
        if (!password.Any(char.IsLetter)) errores.Add("Debe incluir al menos una letra.");
        if (!password.Any(char.IsDigit)) errores.Add("Debe incluir al menos un número.");
        if (Comunes.Contains(password)) errores.Add("Es una contraseña demasiado común.");
        if (!string.IsNullOrWhiteSpace(nombreUsuario) && password.Contains(nombreUsuario.Trim(), StringComparison.OrdinalIgnoreCase))
            errores.Add("No puede contener el nombre de usuario.");
        return errores;
    }

    // Contraseña temporal para altas y reseteos: legible, sin caracteres ambiguos, cumple la política
    public static string GenerarTemporal()
    {
        const string letras = "abcdefghjkmnpqrstuvwxyz";
        const string mayus = "ABCDEFGHJKMNPQRSTUVWXYZ";
        const string numeros = "23456789";
        string Tomar(string fuente, int n) => new(Enumerable.Range(0, n).Select(_ => fuente[RandomNumberGenerator.GetInt32(fuente.Length)]).ToArray());
        return $"{Tomar(mayus, 1)}{Tomar(letras, 4)}-{Tomar(numeros, 4)}";
    }
}
