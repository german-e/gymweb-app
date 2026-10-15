namespace GymCore.Dominio.Entidades;

// Un rol es un conjunto de permisos. El sistema nunca decide por el nombre del rol, solo por sus permisos.
public class Rol
{
    public int Id { get; set; }
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }

    // Rol protegido (los iniciales): no se puede eliminar ni renombrar
    public bool EsSistema { get; set; }

    // Concede todos los permisos, incluidos los que se agreguen en el futuro (rol Administrador)
    public bool AccesoTotal { get; set; }

    public ICollection<RolPermiso> Permisos { get; set; } = new List<RolPermiso>();
    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();

    public bool Tiene(string permiso) => AccesoTotal || Permisos.Any(p => p.Permiso == permiso);

    // Reemplaza los permisos del rol por los indicados (solo códigos conocidos)
    public void AsignarPermisos(IEnumerable<string> permisos)
    {
        var nuevos = permisos.Distinct().ToHashSet();
        foreach (var p in Permisos.Where(p => !nuevos.Contains(p.Permiso)).ToList()) Permisos.Remove(p);
        foreach (var p in nuevos.Where(p => Permisos.All(x => x.Permiso != p))) Permisos.Add(new RolPermiso { Permiso = p });
    }
}

public class RolPermiso
{
    public int RolId { get; set; }
    public Rol Rol { get; set; } = null!;
    public string Permiso { get; set; } = null!;   // código del catálogo (ej. PAGOS_ANULAR)
}
