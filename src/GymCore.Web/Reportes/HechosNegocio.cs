namespace GymCore.Web.Reportes;

// Modelo de lectura para inteligencia de negocio: los hechos mínimos (proyecciones livianas, sin entidades EF)
// sobre los que se calculan todas las métricas. Se carga una vez por request (IFuenteHechos) y las métricas
// son funciones puras sobre él: mismos números en todas las pantallas y testeables sin base de datos.
public sealed class HechosNegocio
{
    public required DateOnly Hoy { get; init; }
    public required IReadOnlyList<SocioH> Socios { get; init; }
    public required IReadOnlyList<SuscripcionH> Suscripciones { get; init; }
    public required IReadOnlyList<PagoH> Pagos { get; init; }               // sin anulados
    public required IReadOnlyList<AsistenciaH> Asistencias { get; init; }   // ventana reciente (ver FuenteHechos)
    public required IReadOnlyDictionary<int, DateOnly> UltimaAsistencia { get; init; }   // histórica, por socio
    public required IReadOnlyList<AsignacionH> Asignaciones { get; init; }
    public required IReadOnlyList<PlanH> Planes { get; init; }
    public required IReadOnlyList<EntrenadorH> Entrenadores { get; init; }

    private ILookup<int, SuscripcionH>? _subsPorSocio;
    private ILookup<int, PagoH>? _pagosPorSub;
    private ILookup<int, AsistenciaH>? _asistPorSocio;
    private readonly Dictionary<DateOnly, HashSet<int>> _activos = new();

    public ILookup<int, SuscripcionH> SubsPorSocio => _subsPorSocio ??= Suscripciones.ToLookup(s => s.SocioId);
    public ILookup<int, PagoH> PagosPorSub => _pagosPorSub ??= Pagos.Where(p => p.SuscripcionId != null).ToLookup(p => p.SuscripcionId!.Value);
    public ILookup<int, AsistenciaH> AsistenciasPorSocio => _asistPorSocio ??= Asistencias.ToLookup(a => a.SocioId);

    // Suscripción que cubre el día d (la de vencimiento más lejano si hay varias)
    public SuscripcionH? VigenteEn(int socioId, DateOnly d) =>
        SubsPorSocio[socioId].Where(s => s.Inicio <= d && s.Fin >= d).MaxBy(s => s.Fin);

    // Socio activo el día d: dado de alta, sin baja a esa fecha y con una suscripción que cubre ese día.
    // (La app no cambia el estado de las suscripciones: la vigencia se reconstruye por fechas.)
    public bool EstaActivo(SocioH s, DateOnly d) =>
        s.Alta <= d && (s.Baja is null || s.Baja > d) && VigenteEn(s.Id, d) is not null;

    public HashSet<int> ActivosEn(DateOnly d)
    {
        if (!_activos.TryGetValue(d, out var set))
            _activos[d] = set = Socios.Where(s => EstaActivo(s, d)).Select(s => s.Id).ToHashSet();
        return set;
    }

    public decimal PagadoHasta(int suscripcionId, DateOnly d) =>
        PagosPorSub[suscripcionId].Where(p => p.Fecha <= d).Sum(p => p.Importe);

    // Moroso el día d: activo con saldo en la suscripción vigente (misma regla que Pagos/Pendientes)
    public bool MorosoEn(SocioH s, DateOnly d) =>
        EstaActivo(s, d) && VigenteEn(s.Id, d) is { } sub && PagadoHasta(sub.Id, d) < sub.Precio;

    public decimal Recaudado(DateOnly desde, DateOnly hasta) =>
        Pagos.Where(p => p.Fecha >= desde && p.Fecha <= hasta).Sum(p => p.Importe);

    public int Ingresos(DateOnly desde, DateOnly hasta) =>
        Asistencias.Count(a => a.Fecha >= desde && a.Fecha <= hasta);

    // Renovación: suscripción que empieza en el rango y el socio ya tenía otra anterior
    public int Renovaciones(DateOnly desde, DateOnly hasta) =>
        Suscripciones.Count(s => s.Inicio >= desde && s.Inicio <= hasta
                                 && SubsPorSocio[s.SocioId].Any(o => o.Id != s.Id && o.Inicio < s.Inicio));
}

public sealed record SocioH(int Id, string Nombre, string Apellido, string Dni, string? Telefono, DateOnly Alta, DateOnly? Baja)
{
    public string NombreCompleto => $"{Nombre} {Apellido}";
}

public sealed record SuscripcionH(int Id, int SocioId, int PlanId, DateOnly Inicio, DateOnly Fin, decimal Precio);
public sealed record PagoH(int SocioId, int? SuscripcionId, DateOnly Fecha, decimal Importe);
public sealed record AsistenciaH(int SocioId, DateOnly Fecha);
public sealed record AsignacionH(int EntrenadorId, int SocioId, DateOnly Desde, DateOnly? Hasta, bool Activa);
public sealed record PlanH(int Id, string Nombre, decimal Precio, bool Activo);
public sealed record EntrenadorH(int Id, string NombreCompleto, bool Activo);
