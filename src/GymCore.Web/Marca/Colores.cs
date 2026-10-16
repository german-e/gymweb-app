using System.Globalization;
using System.Text.RegularExpressions;

namespace GymCore.Web.Marca;

// Derivación de la paleta a partir de los colores corporativos. Garantiza legibilidad:
// el color de marca se usa en botones, enlaces y estados activos, así que se oscurece lo mínimo necesario
// para alcanzar contraste suficiente sobre blanco (≥ 3:1 en relleno/íconos, ≥ 4,5:1 en texto).
public static partial class Colores
{
    public readonly record struct Rgb(byte R, byte G, byte B)
    {
        public string Hex => $"#{R:X2}{G:X2}{B:X2}";
        public string Css => $"{R} {G} {B}";   // para rgb(var(--x-rgb) / alfa)
    }

    [GeneratedRegex("^#?([0-9a-fA-F]{6}|[0-9a-fA-F]{3})$")]
    private static partial Regex HexRegex();

    public static bool EsValido(string? hex) => hex is not null && HexRegex().IsMatch(hex.Trim());

    // "#2563eb" | "2563EB" | "#26e" → "#2563EB"; null si no es un color válido
    public static string? Normalizar(string? hex)
    {
        if (!EsValido(hex)) return null;
        var h = hex!.Trim().TrimStart('#');
        if (h.Length == 3) h = string.Concat(h.Select(c => $"{c}{c}"));
        return "#" + h.ToUpperInvariant();
    }

    public static Rgb Parse(string hex)
    {
        var h = (Normalizar(hex) ?? throw new FormatException($"Color inválido: {hex}"))[1..];
        return new Rgb(byte.Parse(h[..2], NumberStyles.HexNumber), byte.Parse(h[2..4], NumberStyles.HexNumber), byte.Parse(h[4..], NumberStyles.HexNumber));
    }

    // Mezcla lineal con negro (t > 0 oscurece) o blanco (t < 0 aclara)
    public static Rgb Mezclar(Rgb c, double t)
    {
        byte M(byte v) => (byte)Math.Round(t >= 0 ? v * (1 - t) : v + (255 - v) * -t);
        return new Rgb(M(c.R), M(c.G), M(c.B));
    }

    // WCAG 2.x
    public static double Luminancia(Rgb c)
    {
        static double L(byte v) { var s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
        return 0.2126 * L(c.R) + 0.7152 * L(c.G) + 0.0722 * L(c.B);
    }

    public static double Contraste(Rgb a, Rgb b)
    {
        var (la, lb) = (Luminancia(a), Luminancia(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    public static readonly Rgb Blanco = new(255, 255, 255);
    public static readonly Rgb Tinta = new(15, 23, 42);   // #0F172A, texto principal

    // Oscurece en pasos hasta llegar al contraste pedido contra blanco
    public static Rgb ConContrasteSobreBlanco(Rgb c, double minimo)
    {
        for (var t = 0.0; t <= 1.0; t += 0.04)
        {
            var x = Mezclar(c, t);
            if (Contraste(x, Blanco) >= minimo) return x;
        }
        return Tinta;
    }

    // Texto que va encima del color (botón primario, cabecera de credencial)
    public static Rgb TextoSobre(Rgb fondo) => Contraste(fondo, Blanco) >= Contraste(fondo, Tinta) ? Blanco : Tinta;
}
