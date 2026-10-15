using GymCore.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymCore.Infraestructura.Persistencia.Config;

public class CategoriaProductoConfig : IEntityTypeConfiguration<CategoriaProducto>
{
    public void Configure(EntityTypeBuilder<CategoriaProducto> b)
    {
        b.ToTable("CategoriasProducto");
        b.Property(x => x.Nombre).IsRequired().HasMaxLength(60);
        b.Property(x => x.Descripcion).HasMaxLength(200);
        b.Property(x => x.Icono).IsRequired().HasMaxLength(40);
        b.HasIndex(x => x.Nombre).IsUnique();
    }
}

public class ProductoConfig : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> b)
    {
        b.ToTable("Productos");
        b.Property(x => x.Codigo).IsRequired().HasMaxLength(30);
        b.Property(x => x.CodigoBarras).HasMaxLength(40);
        b.Property(x => x.Nombre).IsRequired().HasMaxLength(120);
        b.Property(x => x.Marca).HasMaxLength(60);
        b.Property(x => x.Descripcion).HasMaxLength(500);
        b.Property(x => x.PrecioCompra).HasColumnType("decimal(18,2)");
        b.Property(x => x.PrecioVenta).HasColumnType("decimal(18,2)");
        b.Ignore(x => x.Estado);
        b.Property(x => x.StockActual).IsConcurrencyToken();   // evita perder ventas simultáneas del mismo producto
        b.HasIndex(x => x.Codigo).IsUnique();
        b.HasIndex(x => x.CodigoBarras).IsUnique();   // SQLite admite varios NULL en un índice único
        b.HasOne(x => x.Categoria).WithMany(c => c.Productos).HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MovimientoStockConfig : IEntityTypeConfiguration<MovimientoStock>
{
    public void Configure(EntityTypeBuilder<MovimientoStock> b)
    {
        b.ToTable("MovimientosStock");
        b.Property(x => x.CostoUnitario).HasColumnType("decimal(18,2)");
        b.Property(x => x.Motivo).HasMaxLength(300);
        b.Property(x => x.Usuario).HasMaxLength(120);
        b.HasIndex(x => new { x.ProductoId, x.Fecha });
        b.HasIndex(x => x.Fecha);
        b.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Venta).WithMany().HasForeignKey(x => x.VentaId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class VentaConfig : IEntityTypeConfiguration<Venta>
{
    public void Configure(EntityTypeBuilder<Venta> b)
    {
        b.ToTable("Ventas");
        b.Property(x => x.ClienteNombre).HasMaxLength(120);
        b.Property(x => x.Observaciones).HasMaxLength(500);
        b.Property(x => x.Usuario).HasMaxLength(120);
        b.Property(x => x.UsuarioAnulo).HasMaxLength(120);
        b.Property(x => x.MotivoAnulacion).HasMaxLength(Pago.MotivoMaxLength);
        b.Property(x => x.TotalProductos).HasColumnType("decimal(18,2)");
        b.Property(x => x.Total).HasColumnType("decimal(18,2)");
        b.Ignore(x => x.Numero);
        b.HasIndex(x => x.Fecha);
        b.HasIndex(x => x.SocioId);
        b.HasOne(x => x.Socio).WithMany().HasForeignKey(x => x.SocioId).OnDelete(DeleteBehavior.Restrict);
        // La cuota del ticket es un Pago común (saldos, recibos y reportes de membresía no cambian)
        b.HasOne(x => x.Cuota).WithOne(p => p.Venta).HasForeignKey<Pago>(p => p.VentaId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class VentaItemConfig : IEntityTypeConfiguration<VentaItem>
{
    public void Configure(EntityTypeBuilder<VentaItem> b)
    {
        b.ToTable("VentaItems");
        b.Property(x => x.Descripcion).IsRequired().HasMaxLength(120);
        b.Property(x => x.PrecioUnitario).HasColumnType("decimal(18,2)");
        b.Property(x => x.CostoUnitario).HasColumnType("decimal(18,2)");
        b.Ignore(x => x.Subtotal);
        b.Ignore(x => x.Margen);
        b.HasIndex(x => x.ProductoId);
        b.HasOne(x => x.Venta).WithMany(v => v.Items).HasForeignKey(x => x.VentaId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
    }
}
