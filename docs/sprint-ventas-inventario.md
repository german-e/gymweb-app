# Sprint: Ventas e Inventario

Módulo comercial completo para el mostrador del gimnasio: catálogo de productos, categorías, Punto de Venta (POS),
inventario con libro de movimientos, alertas de stock, reportes comerciales y compras por socio. Todo auditado,
autorizado por permisos y con el mismo Design System del resto de la aplicación.

> No es un módulo de "insumos": cada unidad que entra o sale queda trazada con fecha, usuario, motivo y stock resultante.

## 1. Navegación

Nueva sección **Ventas** en el menú lateral (cada ítem visible solo con su permiso):

| Ítem | Ruta | Permiso |
|---|---|---|
| Punto de Venta | `/Ventas/Pos` | `VENTAS_CREAR` |
| Ventas (listado y tickets) | `/Ventas`, `/Ventas/Ticket/{id}` | `VENTAS_VER` (el ticket también con `VENTAS_CREAR`) |
| Productos | `/Ventas/Productos`, `/Detalle/{id}`, `/Editar/{id?}` | `PRODUCTOS_VER` · alta `PRODUCTOS_CREAR` · edición `PRODUCTOS_EDITAR` |
| Categorías | `/Ventas/Categorias` | `PRODUCTOS_VER` (crear/editar con los permisos de producto) |
| Inventario | `/Ventas/Inventario` | `PRODUCTOS_VER` (compras/ajustes: `INVENTARIO_ADMINISTRAR`) |
| Movimientos | `/Ventas/Movimientos` | `INVENTARIO_ADMINISTRAR` |
| Reportes comerciales | `/Ventas/Reportes` | `REPORTES_COMERCIALES_VER` |

Además: alertas de stock en el **Panel** y en la **campana**, atajo "Punto de Venta" en el Panel, botón **Vender** y pestaña
**Compras** en la ficha del socio (`VENTAS_VER`).

## 2. Historias implementadas

| Historia | Dónde |
|---|---|
| H1 Productos | Listado con KPIs (activos, stock bajo, sin stock, ventas del día), filtros por estado/categoría, búsqueda por nombre, código, código de barras o marca; acciones Ver, Editar, Ajustar stock, Activar/Desactivar. Formulario con vista previa y margen en vivo; código interno automático (`BEB-001`) si se deja vacío. |
| H2 Categorías | Tarjetas con productos y alertas; crear, editar (nombre, descripción, ícono), activar/desactivar. Se siembran Bebidas, Suplementos, Nutrición, Accesorios e Indumentaria **solo si no hay ninguna**. |
| H3 Punto de Venta | Buscador con lector de códigos (Enter), accesos rápidos (productos destacados), chips de categoría, carrito con cantidades, total, medios de pago y **Cobrar** (F9). Socio existente (nombre, N°, plan, vencimiento, última asistencia) o cliente ocasional. |
| H4 Cuota + productos | "Agregar cuota" propone el saldo pendiente del plan vigente; se cobra en el mismo ticket como un `Pago` común (vinculado por `Pago.VentaId`). Requiere además `PAGOS_CREAR`. |
| H5 Stock automático | Toda venta descuenta stock con un movimiento `Venta`; compras, ajustes y correcciones desde el modal "Movimiento de stock". |
| H6 Movimientos | Línea de tiempo del libro de inventario con filtros por producto, tipo, fechas y usuario; también en la ficha del producto. |
| H7 Alertas | 🔴 Sin stock · 🟠 Stock crítico (≤ mínimo) · 🟡 Próximo al mínimo (≤ 1,5 × mínimo). En Panel, campana, Inventario y listado de productos; siempre con ícono + texto. |
| H8 Reportes comerciales | KPIs Ventas hoy, Ventas del mes, Margen estimado, Rentabilidad (con variación y tendencia de 12 meses); evolución mensual ventas/margen, ventas por categoría, más vendidos, más rentables, productos sin ventas; tablas equivalentes. |
| H9 Compras del socio | Pestaña Compras: Fecha, Producto, Cantidad, Importe, Ticket; KPIs Total comprado, Última compra, Ticket promedio. |
| H10 Auditoría | Eventos 800–808 (ver §5), filtrables en Administración › Auditoría ("Ventas e inventario"). |

## 3. Arquitectura

```
Pages/Ventas/*            → solo presentación y validación de entrada
Comercial/
  ServicioInventario      → único camino para cambiar stock (Registrar / AjustarAsync), alertas (ResumenAsync, memo por request)
  ServicioCobranza        → suscripción vigente, saldo de cuota y alta de Pago (usado por Pagos/Nuevo y por el POS)
  ServicioVentas          → RegistrarAsync / AnularAsync en transacción (precios del servidor, stock, cuota, auditoría)
  ReportesComerciales     → read model de ítems vendidos + cálculo puro (Calcular) testeable sin base
  InventarioPresentacion  → textos, íconos, colores, motivos y categorías iniciales
```

Reglas clave:

- **El navegador solo envía ids y cantidades.** Precio y costo se leen del catálogo en el servidor y se **congelan** en
  `VentaItem` (cambiar un precio no altera ventas anteriores; el margen histórico usa el costo de ese momento).
- **El stock nunca se edita directo** (`Producto.StockActual` tiene setter privado). Solo cambia vía `MovimientoStock`
  y nunca queda negativo.
- **Concurrencia:** `StockActual` es token de concurrencia. Si dos cajas venden la última unidad, la segunda recibe
  "El stock cambió mientras cobrabas…" y no se registra nada.
- **Una sola transacción por ticket:** venta, ítems, movimientos, cuota (Pago) y eventos de auditoría se guardan juntos.
- **Anulación lógica:** la venta queda marcada (fecha, usuario, motivo), el stock vuelve con un movimiento
  `Anulación de venta` y la cuota incluida se anula como cualquier pago (deja de contar en saldos y recaudación).
- **Reportes comerciales = solo productos.** La cuota cobrada en un ticket es ingreso de membresía y se sigue viendo en
  Pagos e Inteligencia de negocio (no se duplica).
- Pagos/Nuevo ahora usa `ServicioCobranza` (misma lógica que antes, sin cambio de comportamiento).

Preparado para:

- **Lectores de códigos de barras:** el buscador del POS acepta el código + Enter (lo que envía un escáner USB);
  el campo de código de barras del formulario no envía el formulario al recibir Enter.
- **Impresoras térmicas:** el ticket tiene formato de 72–80 mm e imprime solo el comprobante.
- **E-commerce:** `Venta.Canal` (Mostrador / Online) y el servicio de ventas no depende de la pantalla.
- **Multi sucursal:** todo el stock pasa por `ServicioInventario`; agregar `SucursalId` a `MovimientoStock` y un stock
  por sucursal es un cambio localizado.

## 4. Cambios de dominio y base de datos

Tablas nuevas (se crean solas al iniciar con `EsquemaSqlite`, sin migraciones destructivas):
`CategoriasProducto`, `Productos`, `MovimientosStock`, `Ventas`, `VentaItems`.

Columna nueva nullable: `Pagos.VentaId` (índice único; los pagos existentes quedan en NULL).

Índices: `Productos.Codigo` (único), `Productos.CodigoBarras` (único, admite varios vacíos), `MovimientosStock(ProductoId, Fecha)`,
`MovimientosStock.Fecha`, `Ventas.Fecha`, `Ventas.SocioId`, `VentaItems.ProductoId`.

Verificado sobre una copia de `data/gym.db`: tablas, columna e índices creados; socios y pagos intactos; categorías iniciales sembradas.

## 5. Auditoría

| Código | Evento | Entidad |
|---|---|---|
| 800 | Producto creado | Producto |
| 801 | Producto modificado (campos cambiados; detalle del cambio de precio) | Producto |
| 802 / 803 | Producto activado / desactivado | Producto |
| 804 / 805 | Categoría creada / modificada (incluye activar/desactivar) | Categoría |
| 806 | Movimiento de stock (compra, ajuste, corrección; stock antes → después y motivo) | Producto |
| 807 | Venta realizada (número, total, medio, ítems) | Venta |
| 808 | Venta anulada (motivo) | Venta |

Además la cuota del ticket genera `PagoRegistrado` / `PagoAnulado` como cualquier cobro. Los movimientos de stock
guardan usuario e Id de usuario (los completa el DbContext).

## 6. Permisos y roles

Permisos nuevos (módulo "Ventas e inventario" en Administración › Roles): `PRODUCTOS_VER`, `PRODUCTOS_CREAR`,
`PRODUCTOS_EDITAR`, `VENTAS_VER`, `VENTAS_CREAR`, `VENTAS_ANULAR`*, `INVENTARIO_ADMINISTRAR`*, `REPORTES_COMERCIALES_VER`* (*sensibles).

Roles iniciales (solo para bases nuevas):

- **Recepcionista:** `PRODUCTOS_VER`, `VENTAS_VER`, `VENTAS_CREAR`.
- **Supervisor:** lo anterior + `PRODUCTOS_CREAR`, `PRODUCTOS_EDITAR`, `VENTAS_ANULAR`, `INVENTARIO_ADMINISTRAR`, `REPORTES_COMERCIALES_VER`.
- **Administrador:** acceso total (ya ve todo).
- **Entrenador:** sin acceso al módulo.

> ⚠️ **Bases existentes:** los roles que ya existen no se modifican automáticamente (para no pisar cambios hechos a mano).
> Si tenés roles Recepcionista/Supervisor creados antes de este sprint, asignales los permisos nuevos desde
> **Administración › Roles y permisos**. El Administrador no necesita cambios.

## 7. Casos de prueba automatizados (29 nuevos, 88 en total)

`VentasInventarioTests` (dominio y servicios, SQLite en memoria):
estados de stock y umbrales; stock nunca negativo; margen; venta descuenta stock, agrupa líneas repetidas, congela
precio/costo y audita con Id; stock insuficiente no registra nada; producto inactivo, ticket vacío y cuota sin socio
rechazados; cuota + productos imputa a la suscripción vigente y salda la cuota; anulación repone stock, anula la cuota
y conserva el ticket (no se anula dos veces); **dos cajas no venden la misma última unidad**; compra actualiza costo;
corrección por conteo registra la diferencia; ajuste no deja stock negativo y "Venta" no es un ajuste manual; resumen
de alertas excluye inactivos y ordena por urgencia; métricas comerciales (mes vs. mismo tramo, margen, rentabilidad,
ranking, color estable por categoría, productos sin ventas); ventas anuladas fuera de reportes y de los totales del socio.

`VentasIntegracionTests` (aplicación real por HTTP): categorías iniciales; recepción vende desde el POS y el stock se
descuenta (ticket y pestaña Compras); el navegador no puede imponer precio ni superar stock; recepción no administra
inventario, no ve reportes comerciales ni anula; entrenador sin acceso al módulo; administrador registra compra,
vende, anula (stock repuesto) y ve reportes, movimientos y auditoría; el "volver" tras un ajuste solo acepta URLs locales.

Verificación adicional: recorrido E2E de 25 pantallas sobre datos sintéticos (todas 200, libro de inventario consistente:
stock = Σ movimientos = último stock resultante), POS simulado en navegador headless (escaneo, búsqueda, límites de
stock, socio, cuota, envío sin doble cobro) y gráficos validados con su tooltip.

## 8. Checklist de validación manual

1. Reiniciar la aplicación (crea tablas y categorías). Hacer backup previo de `data/gym.db`.
2. Productos › Nuevo producto: cargar uno con código de barras, stock inicial y mínimo; marcar "Acceso rápido".
3. Punto de Venta: escanear (o tipear el código y Enter), sumar cantidades, elegir socio, "Agregar cuota", Cobrar.
4. Ver el ticket, imprimirlo (térmica u hoja), revisar Inventario y la pestaña Compras del socio.
5. Bajar un producto al mínimo y ver la alerta en Panel, campana e Inventario.
6. Registrar una compra desde Inventario y una corrección por conteo; revisar Movimientos.
7. Anular un ticket con cuota: stock repuesto, pago anulado, evento en Auditoría.
8. Reportes comerciales: cambiar el período, revisar tablas equivalentes.
9. Con un usuario Recepcionista: puede vender, no ve Movimientos ni Reportes comerciales ni puede anular.

## Limitaciones conocidas

- Un ticket se cobra con un único medio de pago (pago dividido: pendiente).
- Sin devoluciones parciales: se anula el ticket completo y se vuelve a cobrar lo que corresponda.
- El stock es único (sin depósitos ni sucursales todavía).
- No emite factura fiscal: el ticket es un comprobante interno.
