# Rediseño de Planes y Pagos

Modernización visual de los módulos **Planes** y **Pagos** con el mismo sistema de diseño de Socios y Entrenadores.
No se tocó el dominio, el esquema ni las reglas de negocio: los estados e indicadores nuevos se **derivan** de datos que ya existían.

## 1. Propuesta UX/UI

| Principio | Cómo se aplica |
|---|---|
| Un solo producto | Mismo esqueleto en todas las páginas: `page-header` (eyebrow + título + acciones) → `stats-grid` (KPIs) → `data-card` (toolbar + `data-table`). |
| La acción principal primero | Un único `primary-btn` por pantalla (Nuevo plan / Registrar pago). Lo secundario va en `secondary-btn` o en el menú ⋯. |
| Estado de un vistazo | `status-pill` con un color por significado: verde = OK, ámbar = atención, rojo = acción requerida, gris = inactivo, celeste = informativo. |
| Filtrar sin recargar | Búsqueda instantánea, chips y selects (`data-table-filter` / `data-table-facet`) y orden por columna (`data-sortable`). Atajo `/`. |
| Prevenir antes que corregir | Eliminar un plan con suscripciones queda deshabilitado (antes daba error de FK). Todas las acciones destructivas pasan por `_ConfirmModal`. |
| Feedback inmediato | Flash al volver de un POST, spinner en el botón al enviar, skeleton mientras carga el saldo, toasts al copiar o descargar. |
| Responsive | Tablas `stack-sm` (en móvil pasan a tarjetas), grids de 4 → 2 → 1 columna, paneles laterales que bajan debajo del contenido. |

## 2. Wireframes

### Planes — listado (`/Planes/Planes`)
```
┌ CATÁLOGO ───────────────────────────────────────────── [+ Nuevo plan] ┐
│ Planes                                                                │
│ Precios, duración y reglas de acceso…                                 │
├───────────────┬───────────────┬────────────────────┬──────────────────┤
│ ▣ Total       │ ✓ Activos     │ 🏆 Más contratado  │ ↗ Ingreso mens.  │
│   4           │   3   75%     │   Musculación  12  │   $ 1.240.000    │
└───────────────┴───────────────┴────────────────────┴──────────────────┘
┌───────────────────────────────────────────────────────────────────────┐
│ [🔍 Buscar…  /]        [Todos 4|En uso 3|Sin socios 1] [Tipo ▾]       │
├───────────────────────────────────────────────────────────────────────┤
│ PLAN ↕            PRECIO ↕   DURACIÓN ↕  SOCIOS ↕   ESTADO       ⋯    │
│ [∞] Ilimitado     $ 25.000   30 días     👥 8      ● En uso    👁 ✎ ⋮ │
│ [▦] 3x semana     $ 20.000   30 días     👥 4      ● En uso    👁 ✎ ⋮ │
│ [🎟] Pase diario   $ 3.000    1 día       👥 0      ○ Sin socios 👁 ✎ ⋮│
├───────────────────────────────────────────────────────────────────────┤
│ Mostrando 3 de 3                       Tip: clic en encabezado ordena │
└───────────────────────────────────────────────────────────────────────┘
  ⋮ = Ver detalle · Editar · Duplicar · Ver socios · Eliminar
```

### Planes — ficha (`/Planes/Detalle/{id}`)
```
┌───────────────────────────────────────────────────────────────────────┐
│ [ ∞ ]  Mensual ilimitado  ● En uso                  PRECIO POR CICLO  │
│        ∞ Mensual · ↻ 30 días · 👥 8 socios            $ 25.000        │
│        [✎ Editar] [⧉ Duplicar] [⋯]                    / 30 días       │
├──────────┬──────────┬────────────────┬───────────┬────────────────────┤
│ Tipo     │ Duración │ Días por semana│ Sábados   │ Domingos           │
└──────────┴──────────┴────────────────┴───────────┴────────────────────┘
[$ Ingresos generados] [👤 Socios activos] [↻ Renovaciones] [↗ Ingreso est.]
┌ Información │ Socios (12) ──────────────────────────────────────────────┐
│ Descripción …                 │ Beneficios incluidos                  │
│                               │ ✓ Acceso libre todos los días         │
│                               │ ✓ Ciclo de 30 días                    │
│                               │ ✓ Sábados   ✗ Domingos                │
└───────────────────────────────────────────────────────────────────────┘
  Pestaña Socios: buscador + [Vigentes|Todos], vencimiento, períodos, saldo.
```

### Planes — formulario (`Create` / `Edit` / `Create?duplicar={id}`)
```
┌──────────────────┐  ┌ ▣ Información general ──────────────────────────┐
│ ▸ Información    │  │ Nombre*  [                ]  Tipo* [Mensual ▾]  │
│   Precio         │  │ Descripción [                               ]   │
│   Acceso         │  └─────────────────────────────────────────────────┘
├──────────────────┤  ┌ $ Precio y duración ────────────────────────────┐
│ VISTA PREVIA     │  │ Precio* [$     ]  Moneda* [ARS]  Duración* [30] │
│ [∞] Nombre       │  │ Atajos: (1 día)(1 semana)(Mensual)(Trim.)(Anual)│
│ Mensual ilimit.  │  └─────────────────────────────────────────────────┘
│ $ 25.000 /30 días│  ┌ 🚪 Reglas de acceso ────────────────────────────┐
│ ✓ Libre todos    │  │ Días por semana [  ]                            │
│ ✓ Sábados        │  │ Acceso los sábados ……………………………………… [●━]     │
└──────────────────┘  │ Acceso los domingos …………………………………… [●━]     │
                      └─────────────────────────────────────────────────┘
                      [ ⓘ hint ……………………… (Cancelar) (✓ Crear plan) ]  ← sticky
```

### Pagos — listado (`/Pagos`)
```
[$ Recaudación mes] [⌛ Pagos pendientes] [⚠ Cuotas vencidas] [📅 Pagos del día]
┌─ ROJO ───────────────┐┌─ ÁMBAR ──────────────┐┌─ VERDE ─────────────────┐
│ ⊗ Pagos vencidos  3  ││ ⏰ Próximos venc.  5 ││ ✓ Pagos al día  42      │
│ · Ana Pérez  …       ││ · Juan Gómez  en 2 d ││ 89% de las vigentes     │
│ · …                  ││ Ver todos →          ││ Ver socios activos →    │
└──────────────────────┘└──────────────────────┘└─────────────────────────┘
┌───────────────────────────────────────────────────────────────────────┐
│ [🔍 Socio, DNI, plan…]   [Todos|Hoy|Este mes] [Método ▾] [Estado ▾]   │
│ FECHA ↕   SOCIO ↕          PLAN ↕     MÉTODO       IMPORTE ↕  ESTADO  │
│ 14/10     (AP) Pérez, Ana  3x semana  🏦 Transf.   $ 10.000  ● Parcial│
│ 13/10     (JG) Gómez, Juan Ilimitado  💵 Efectivo  $ 25.000  ● Pagado │
└───────────────────────────────────────────────────────────────────────┘
```

### Registrar pago (`/Pagos/Nuevo`)
```
┌ 👤 Socio ─────────────────────────────┐  ┌ ESTADO DE CUENTA ─────────┐
│ [🔍 DNI o apellido          ]         │  │ (skeleton mientras carga) │
│ (✓ Pérez, Ana · DNI 30111222)         │  │ Plan vigente              │
└───────────────────────────────────────┘  │ Mensual 3x semana         │
┌ $ Importe y medio de pago ────────────┐  │ 01/10 al 31/10            │
│ Importe* [$ 10.000 ]  [✨ Pagar total] │  │ ▓▓▓▓▓▓░░░░ 50%            │
│ [💵 Efectivo][💳 Débito][💳 Crédito]  │  │ Precio        $ 20.000    │
│ [🏦 Transferencia][📱 Mercado Pago]   │  │ Pagado        $ 10.000    │
└───────────────────────────────────────┘  │ Faltante      $ 10.000    │
┌ 💬 Detalles ──────────────────────────┐  └───────────────────────────┘
│ Referencia [    ]  Observaciones [  ] │
└───────────────────────────────────────┘
```

### Detalle de pago (`/Pagos/Recibo/{id}`)
```
Pago R-000123 ● Pagado                     [⬇ Descargar PDF] [🖨 Imprimir]
┌ Comprobante (imprimible) ────────────┐  ┌ (AP) Ana Pérez ● Activa ────┐
│ GymCore              Recibo R-000123 │  │ Plan asociado · Vencimiento │
│ Socio · DNI · Concepto · Medio …     │  ├ Acciones ───────────────────┤
│ Pagado del período · Saldo           │  │ 🖨 Imprimir recibo          │
├──────────────────────────────────────┤  │ 📄 Descargar PDF            │
│ Total abonado            $ 10.000    │  │ ➤ Reenviar (WhatsApp/email) │
└──────────────────────────────────────┘  │ $ Registrar otro pago       │
                                          ├ Historial del socio ────────┤
                                          │ ● Cuota 3x semana  $ 10.000 │
                                          │ ● Cuota 3x semana  $ 20.000 │
                                          └─────────────────────────────┘
```

## 3. Componentes reutilizables

Parciales Razor (`Pages/Shared/Components`) con su modelo en `Componentes/ComponentesVm.cs`:

| Componente | Uso |
|---|---|
| `_StatCard` (existente) | KPIs de Planes, Pagos y Deudas. |
| `_EmptyState` (existente) | Listados vacíos y "sin resultados". |
| `_ConfirmModal` (existente) | Eliminar plan. Ahora muestra spinner al confirmar. |
| `_Avatar` (existente) | Socios en pagos, ficha de plan y detalle de pago. |
| **`_AlertCard`** (nuevo) | Semáforo de vencimientos: `AlertCardVm(variante, ícono, título, cantidad, texto, items, acción)`. |
| **`_PagosTimeline`** (nuevo) | Historial de pagos agrupado por mes. Se usa en la ficha del socio (reemplaza la tabla) y en el detalle de pago. |

Helpers de presentación (textos, íconos y colores en un solo lugar):

- `Pages/Planes/PlanPresentacion.cs`: tipo de plan → texto/ícono/color, estado del plan y de cada suscripción, beneficios.
- `Pages/Pagos/PagoPresentacion.cs`: `EstadoCobro` (Pagado / Parcial / Pendiente / Vencido) → texto y color, número de recibo, concepto.

Comportamientos nuevos en `wwwroot/js/components.js` (por atributos, sin dependencias):

| Atributo | Efecto |
|---|---|
| `table[data-sortable] th[data-sort="text\|num"]` | Ordena por columna (clic o Enter). La celda puede fijar la clave con `data-sort-value`. |
| `[data-bs-toggle="tooltip"]` | Tooltips de Bootstrap. |
| `form[data-submit-loading]` | Deshabilita el botón y muestra spinner (`data-loading-text`) si la validación pasó. |
| `[data-copy="texto"]` | Copia al portapapeles y muestra un toast. |
| `GymUi.toast(msg, variante)` | Confirmación visual no bloqueante. |

Estilos nuevos al final de `wwwroot/css/components.css` (sección "Planes y Pagos"): `alert-card`, `benefit-list`, `price-block`, `plan-preview`, `medio-picker`, `saldo-list`, `skeleton`, `pay-timeline`, `receipt-layout`, `app-toast`, encabezados ordenables y elevación al pasar el mouse sobre las tarjetas KPI.

## 4. Plan de refactor visual

| # | Paso | Estado |
|---|---|---|
| 1 | Base `PlanFormPageModel` + parcial `_PlanForm` (Alta, Edición y Duplicar comparten formulario y validaciones, igual que Entrenadores). | Hecho |
| 2 | Listado de Planes: KPIs, buscador, filtros, orden y menú de acciones. | Hecho |
| 3 | Ficha de Plan (`Detalle`). `Suscriptos?planId=` redirige a `Detalle#tab-socios`. | Hecho |
| 4 | Eliminar plan: modal + página de respaldo, bloqueado si tiene suscripciones. | Hecho |
| 5 | Pagos: KPIs, semáforo de vencimientos y listado con estados. | Hecho |
| 6 | Registrar pago: secciones, selector visual de medio, panel de cuenta con skeleton. Se conservaron IDs, handlers AJAX y reglas. | Hecho |
| 7 | Detalle de pago (`Recibo`): panel lateral con socio, acciones e historial. | Hecho |
| 8 | Deudas (`Pendientes`): KPIs, barra de progreso de pago, orden. | Hecho |
| 9 | Ficha del socio: pestaña Pagos en formato timeline. | Hecho |
| 10 | Menú lateral: resalta el módulo actual (antes siempre marcaba "Panel"). | Hecho |

### Cómo se calculan los indicadores

| Indicador | Definición |
|---|---|
| Plan activo / "En uso" | Tiene al menos un socio con suscripción `Activo` y `FechaFin >= hoy` (la misma regla del listado anterior). |
| Plan más contratado | Más suscripciones históricas. |
| Ingreso mensual estimado | Σ precio del plan × suscripciones vigentes. |
| Renovaciones (plan) | Suscripciones al plan − socios distintos. |
| Ingresos generados (plan) | Σ pagos asociados a suscripciones del plan. |
| Pago "Pagado" / "Pago parcial" | El total abonado al período cubre (o no) el precio del plan. Un pago a cuenta es "Pagado". |
| Pagos pendientes | Socios vigentes con saldo (misma regla que Deudas). |
| Cuotas vencidas / próximos vencimientos / al día | Clasificación de `SociosConsultas` (la misma del listado de Socios; aviso a 7 días). |

## 5. Recomendaciones (fase 2: requieren cambios de dominio)

Quedaron fuera porque no existen los datos y agregarlos cambia reglas de negocio. Los puntos 1, 2, 4 y 7 se implementaron en el sprint siguiente (ver [sprint-planes-cobranza.md](sprint-planes-cobranza.md)).

1. ~~**Activar / desactivar plan**~~ (hecho).
2. ~~**Fecha de creación del plan**~~ (hecho).
3. ~~**Anular pago**~~ (hecho, ver [sprint-auditoria-anulaciones.md](sprint-auditoria-anulaciones.md)).
4. ~~**Usuario que registró**~~ (hecho, con el operador configurado mientras no haya login).
5. **Reenvío real del comprobante**: hoy se usa WhatsApp (`wa.me`) o `mailto` con el texto. Para adjuntar el PDF automáticamente hace falta un servicio de email o WhatsApp Business.
6. **Paginación de pagos en el servidor**: el listado sigue mostrando los últimos 200, como antes. Con más volumen conviene paginar y filtrar por fechas en el servidor.
7. ~~**Panel y sidebar con datos reales**~~ (hecho).
8. **Orden en móvil**: en pantallas chicas la tabla pasa a tarjetas y se ocultan los encabezados ordenables. Si hace falta, sumar un select "Ordenar por".
