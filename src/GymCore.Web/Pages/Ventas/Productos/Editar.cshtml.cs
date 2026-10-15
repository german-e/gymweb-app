using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using GymCore.Dominio.Entidades;
using GymCore.Dominio.Enums;
using GymCore.Infraestructura.Persistencia;
using GymCore.Web.Comercial;
using GymCore.Web.Componentes;
using GymCore.Web.Pages.Socios;
using GymCore.Web.Seguridad;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GymCore.Web.Pages.Ventas.Productos;

// Alta (sin id, PRODUCTOS_CREAR) y edición (con id, PRODUCTOS_EDITAR) de productos.
[RequierePermiso(Permisos.ProductosCrear, Permisos.ProductosEditar)]
public class EditarModel : PageModel
{
    private readonly GymDbContext _db;
    private readonly IUsuarioActual _usuario;
    private readonly ServicioInventario _inventario;
    public EditarModel(GymDbContext db, IUsuarioActual usuario, ServicioInventario inventario)
    { _db = db; _usuario = usuario; _inventario = inventario; }

    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    public bool EsEdicion => Id.HasValue;

    [BindProperty] public InputModel Input { get; set; } = new();
    public List<SelectListItem> Categorias { get; private set; } = new();
    public Dictionary<int, string> IconosCategoria { get; private set; } = new();
    public int StockActual { get; private set; }

    public class InputModel
    {
        [Display(Name = "Código interno"), StringLength(30)]
        public string? Codigo { get; set; }

        [Display(Name = "Código de barras"), StringLength(40)]
        [RegularExpression(@"^[0-9A-Za-z\-]+$", ErrorMessage = "Solo números, letras y guiones.")]
        public string? CodigoBarras { get; set; }

        [Required(ErrorMessage = "Ingresá el nombre."), StringLength(120), Display(Name = "Nombre")]
        public string Nombre { get; set; } = "";

        [Required(ErrorMessage = "Elegí la categoría."), Display(Name = "Categoría")]
        public int? CategoriaId { get; set; }

        [StringLength(60), Display(Name = "Marca")]
        public string? Marca { get; set; }

        [StringLength(500), Display(Name = "Descripción")]
        public string? Descripcion { get; set; }

        [Range(0, 100_000_000, ErrorMessage = "El precio de compra no es válido."), Display(Name = "Precio de compra")]
        public decimal PrecioCompra { get; set; }

        [Range(0.01, 100_000_000, ErrorMessage = "Ingresá el precio de venta."), Display(Name = "Precio de venta")]
        public decimal PrecioVenta { get; set; }

        [Range(0, ServicioInventario.CantidadMaxima, ErrorMessage = "El stock inicial no es válido."), Display(Name = "Stock inicial")]
        public int StockInicial { get; set; }

        [Range(0, ServicioInventario.CantidadMaxima, ErrorMessage = "El stock mínimo no es válido."), Display(Name = "Stock mínimo")]
        public int StockMinimo { get; set; }

        [Display(Name = "Acceso rápido en el Punto de Venta")]
        public bool Destacado { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int? categoria)
    {
        if (!PuedeOperar()) return Forbid();
        if (EsEdicion)
        {
            var p = await _db.Productos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == Id);
            if (p is null) return NotFound();
            Input = new InputModel
            {
                Codigo = p.Codigo, CodigoBarras = p.CodigoBarras, Nombre = p.Nombre, CategoriaId = p.CategoriaId, Marca = p.Marca,
                Descripcion = p.Descripcion, PrecioCompra = p.PrecioCompra, PrecioVenta = p.PrecioVenta,
                StockMinimo = p.StockMinimo, Destacado = p.Destacado
            };
            StockActual = p.StockActual;
        }
        else
        {
            Input.CategoriaId = categoria;
            Input.StockMinimo = 5;
        }
        await CargarAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!PuedeOperar()) return Forbid();
        Input.Codigo = Normalizar(Input.Codigo)?.ToUpperInvariant();
        Input.CodigoBarras = Normalizar(Input.CodigoBarras);
        Input.Nombre = Input.Nombre?.Trim() ?? "";

        Producto? p = null;
        if (EsEdicion)
        {
            p = await _db.Productos.FirstOrDefaultAsync(x => x.Id == Id);
            if (p is null) return NotFound();
            StockActual = p.StockActual;
            ModelState.Remove("Input.StockInicial");
        }

        var categoria = Input.CategoriaId is { } cid ? await _db.CategoriasProducto.FirstOrDefaultAsync(c => c.Id == cid) : null;
        if (Input.CategoriaId.HasValue && categoria is null) ModelState.AddModelError("Input.CategoriaId", "La categoría no existe.");
        else if (categoria is { Activa: false } && categoria.Id != p?.CategoriaId) ModelState.AddModelError("Input.CategoriaId", "La categoría está desactivada.");

        if (Input.Codigo is { } cod && await _db.Productos.AnyAsync(x => x.Codigo == cod && x.Id != (Id ?? 0)))
            ModelState.AddModelError("Input.Codigo", "Ya existe un producto con este código.");
        if (Input.CodigoBarras is { } cb && await _db.Productos.AnyAsync(x => x.CodigoBarras == cb && x.Id != (Id ?? 0)))
            ModelState.AddModelError("Input.CodigoBarras", "Este código de barras ya está asignado a otro producto.");

        if (!ModelState.IsValid)
        {
            await CargarAsync();
            return Page();
        }

        if (p is null)
        {
            p = new Producto { FechaAlta = DateTime.UtcNow, Activo = true };
            _db.Productos.Add(p);
        }
        p.Codigo = Input.Codigo ?? await CodigoSugeridoAsync(categoria!);
        p.CodigoBarras = Input.CodigoBarras;
        p.Nombre = Input.Nombre;
        p.CategoriaId = categoria!.Id;
        p.Marca = Normalizar(Input.Marca);
        p.Descripcion = Normalizar(Input.Descripcion);
        p.PrecioCompra = Math.Round(Input.PrecioCompra, 2);
        p.PrecioVenta = Math.Round(Input.PrecioVenta, 2);
        p.StockMinimo = Input.StockMinimo;
        p.Destacado = Input.Destacado;

        if (!EsEdicion)
        {
            if (Input.StockInicial > 0)
                _inventario.Registrar(p, TipoMovimientoStock.StockInicial, Input.StockInicial, "Alta del producto", p.PrecioCompra);
            _db.Auditar(TipoEventoSistema.ProductoCreado, EntidadAuditada.Producto, null, p.Nombre,
                $"{p.Codigo} · {categoria.Nombre} · {SocioPresentacion.Dinero(p.PrecioVenta)} · stock inicial {Input.StockInicial}", referencia: p);
            await _db.SaveChangesAsync();
            TempData["Flash"] = $"{p.Nombre} creado. Ya está disponible en el Punto de Venta.";
            return RedirectToPage("Detalle", new { id = p.Id });
        }

        var cambios = _db.CamposModificados(p, Auditoria.CamposProducto);
        if (cambios.Count == 0)
        {
            TempData["Flash"] = "No había cambios para guardar.";
            return RedirectToPage("Detalle", new { id = p.Id });
        }
        var entry = _db.Entry(p);
        var detallePrecio = entry.Property(x => x.PrecioVenta).IsModified
            ? $"Precio de venta {SocioPresentacion.Dinero((decimal)entry.Property(x => x.PrecioVenta).OriginalValue)} → {SocioPresentacion.Dinero(p.PrecioVenta)}" : null;
        _db.Auditar(TipoEventoSistema.ProductoEditado, EntidadAuditada.Producto, p.Id, p.Nombre,
            "Cambió " + string.Join(", ", cambios), detallePrecio);
        await _db.SaveChangesAsync();
        TempData["Flash"] = "Cambios guardados. Los precios nuevos aplican a las próximas ventas; las anteriores conservan su precio.";
        return RedirectToPage("Detalle", new { id = p.Id });
    }

    private bool PuedeOperar() => _usuario.Puede(EsEdicion ? Permisos.ProductosEditar : Permisos.ProductosCrear);

    private async Task CargarAsync()
    {
        var cats = await _db.CategoriasProducto.AsNoTracking().OrderBy(c => c.Nombre).ToListAsync();
        Categorias = cats.Where(c => c.Activa || c.Id == Input.CategoriaId)
            .Select(c => new SelectListItem(c.Nombre + (c.Activa ? "" : " (inactiva)"), c.Id.ToString())).ToList();
        IconosCategoria = cats.ToDictionary(c => c.Id, c => c.Icono);
    }

    // Código sugerido: 3 letras de la categoría + correlativo (BEB-001, SUP-014…)
    private async Task<string> CodigoSugeridoAsync(CategoriaProducto c)
    {
        var prefijo = new string(SinTildes(c.Nombre).ToUpperInvariant().Where(char.IsLetter).Take(3).ToArray());
        if (prefijo.Length == 0) prefijo = "PRD";
        var existentes = await _db.Productos.AsNoTracking().Where(x => x.Codigo.StartsWith(prefijo + "-")).Select(x => x.Codigo).ToListAsync();
        var n = existentes.Select(x => int.TryParse(x[(prefijo.Length + 1)..], out var k) ? k : 0).DefaultIfEmpty(0).Max() + 1;
        return $"{prefijo}-{n:D3}";
    }

    private static string SinTildes(string s) => new(s.Normalize(NormalizationForm.FormD)
        .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray());

    private static string? Normalizar(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
