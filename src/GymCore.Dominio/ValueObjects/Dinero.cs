namespace GymCore.Dominio.ValueObjects;

public record Dinero(decimal Monto, string Moneda = "ARS")
{
    public static Dinero operator +(Dinero a, Dinero b)
        => a.Moneda == b.Moneda ? new Dinero(a.Monto + b.Monto, a.Moneda)
                                : throw new InvalidOperationException("Monedas distintas.");
}