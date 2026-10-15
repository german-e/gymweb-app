using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Comercial;
using GymCore.Web.Componentes;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Ventas;

[RequierePermiso(Permisos.ProductosVer)]
public class CategoriasModel : PageModel
{
    private readonly GymDbContext _db;
    private readonly IUsuarioActual _usuario;
    public CategoriasModel(GymDbContext db, IUsuarioActual usuario) { _db = db; _usuario = usuario; }

    public record Fila(CategoriaProducto Categoria, int Productos, int Activos, int Alertas);
    public List<Fila> Filas { get; private set; } = new();

    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashError { get; set; }

    public async Task OnGetAsync()
    {
        var cats = await _db.CategoriasProducto.AsNoTracking().OrderBy(c => c.Nombre).ToListAsync();
        var prods = await _db.Productos.AsNoTracking().Select(p => new { p.CategoriaId, p.Activo, p.StockActual, p.StockMinimo }).ToListAsync();
        Filas = cats.Select(c =>
        {
            var ps = prods.Where(p => p.CategoriaId == c.Id).ToList();
            return new Fila(c, ps.Count, ps.Count(p => p.Activo),
                ps.Count(p => p.Activo && Producto.EstadoDe(p.StockActual, p.StockMinimo) is EstadoStock.SinStock or EstadoStock.Critico));
        }).ToList();
    }

    [RequierePermiso(Permisos.ProductosCrear, Permisos.ProductosEditar)]
    public async Task<IActionResult> OnPostGuardarAsync(int? id, string? nombre, string? descripcion, string? icono)
    {
        if (!_usuario.Puede(id is null ? Permisos.ProductosCrear : Permisos.ProductosEditar)) return Forbid();

        nombre = nombre?.Trim();
        descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
        icono = InventarioPresentacion.IconosCategoria.Any(i => i.Icono == icono) ? icono! : "bi-box";
        if (string.IsNullOrEmpty(nombre) || nombre.Length > 60) { FlashError = "Ingresá un nombre de hasta 60 caracteres."; return RedirectToPage(); }
        if (descripcion?.Length > 200) descripcion = descripcion[..200];
        var nombreBusqueda = nombre.ToLower();
        if (await _db.CategoriasProducto.AnyAsync(c => c.Nombre.ToLower() == nombreBusqueda && c.Id != (id ?? 0)))
        { FlashError = $"Ya existe la categoría {nombre}."; return RedirectToPage(); }

        if (id is null)
        {
            var c = new CategoriaProducto { Nombre = nombre, Descripcion = descripcion, Icono = icono, Activa = true };
            _db.CategoriasProducto.Add(c);
            _db.Auditar(TipoEventoSistema.CategoriaCreada, EntidadAuditada.Categoria, null, nombre, descripcion, referencia: c);
            Flash = $"Categoría {nombre} creada.";
        }
        else
        {
            var c = await _db.CategoriasProducto.FirstOrDefaultAsync(x => x.Id == id);
            if (c is null) return NotFound();
            var anterior = c.Nombre;
            c.Nombre = nombre; c.Descripcion = descripcion; c.Icono = icono;
            if (!_db.ChangeTracker.HasChanges()) { Flash = "No había cambios para guardar."; return RedirectToPage(); }
            _db.Auditar(TipoEventoSistema.CategoriaEditada, EntidadAuditada.Categoria, c.Id, nombre,
                anterior != nombre ? $"Renombrada: {anterior} → {nombre}" : "Datos actualizados");
            Flash = $"Categoría {nombre} actualizada.";
        }
        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    // Desactivar una categoría no toca sus productos: solo impide asignarla a productos nuevos
    [RequierePermiso(Permisos.ProductosEditar)]
    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var c = await _db.CategoriasProducto.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        c.Activa = !c.Activa;
        _db.Auditar(TipoEventoSistema.CategoriaEditada, EntidadAuditada.Categoria, c.Id, c.Nombre, c.Activa ? "Categoría activada" : "Categoría desactivada");
        await _db.SaveChangesAsync();
        Flash = c.Activa ? $"{c.Nombre} está activa." : $"{c.Nombre} fue desactivada: no se podrá elegir para productos nuevos. Sus productos siguen a la venta.";
        return RedirectToPage();
    }
}
