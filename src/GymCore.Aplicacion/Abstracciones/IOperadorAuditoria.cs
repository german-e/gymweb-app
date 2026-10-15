namespace GymCore.Aplicacion.Abstractiones;

// Responsable que se registra en la auditoría. Lo implementa la capa web (IUsuarioActual, a partir del
// usuario autenticado); la persistencia solo necesita su identidad, sin depender de cómo se autentica.
public interface IOperadorAuditoria
{
    int? UsuarioId { get; }   // null fuera de una sesión (procesos de sistema)
    string Nombre { get; }
}
