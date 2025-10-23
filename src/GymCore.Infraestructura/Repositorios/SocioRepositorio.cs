
using GymCore.Aplicacion.Abstractiones;
using GymCore.Dominio.Entidades;
using GymCore.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Infraestructura.Repositorios;

public class SocioRepository : ISocioRepository
{
    private readonly GymDbContext _db;
    public SocioRepository(GymDbContext db) => _db = db;

    public async Task ActualizarAsync(Socio entidad)
    {
        _db.Socios.Update(entidad);
        await _db.SaveChangesAsync();
    }

    public async Task AgregarAsync(Socio entidad)
    {
        await _db.Socios.AddAsync(entidad);
        await _db.SaveChangesAsync();
    }

    public async Task EliminarAsync(int id)
    {
        var socio = await _db.Socios.FindAsync(id);
        if (socio is null) return;
        _db.Socios.Remove(socio);
        await _db.SaveChangesAsync();
    }

    public async Task<Socio> ObtenerPorIdAsync(int id)
    {
        var s = await _db.Socios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) throw new KeyNotFoundException($"Socio {id} no existe");
        return s;
    }

    public async Task<IEnumerable<Socio>> ObtenerTodosAsync()
    {
        return await _db.Socios.AsNoTracking().OrderBy(s => s.Apellido).ThenBy(s => s.Nombre).ToListAsync();
    }
}