# Sprint: estado de planes, trazabilidad de cobros y métricas reales

> **Actualización:** desde el [sprint de autenticación](sprint-autenticacion.md) el responsable sale del usuario autenticado; la configuración `Operador:Nombre` ya no existe.

Cinco historias de bajo riesgo sobre la línea visual existente. No se elimina información, la actualización de esquema no es destructiva y no hace falta tocar la base a mano.

## 1. Plan técnico

| Historia | Enfoque |
|---|---|
| H1 Activar / desactivar planes | `Plan.Activo`. Los dos puntos de alta (`Socios/Create` y `Suscripciones/Nueva`, que también es la renovación) muestran solo planes activos y lo **validan en el servidor**. Las suscripciones existentes no se tocan. |
| H2 Fecha de creación | `Plan.FechaAlta` (UTC, nullable). Se completa al crear o duplicar. Los planes previos quedan "Sin registro": no se inventa una fecha. |
| H3 Trazabilidad de cobros | Servicio `IUsuarioActual` que usa la identidad autenticada si existe y, si no, el operador de `appsettings` (`Operador:Nombre/Rol`). `Pagos/Nuevo` guarda `Pago.UsuarioCajero`. |
| H4 Dashboard real | `IndicadoresGimnasio`: un solo cálculo compartido por Panel, Pagos y el sidebar. Reglas de membresía = `SociosConsultas` (las mismas del listado de Socios). |
| H5 Sidebar inteligente | `SidebarMetricasViewComponent` con dos vistas: tarjeta del sidebar y campana de alertas. Memoriza el cálculo por request para no repetirlo. |

Orden de implementación: persistencia → dominio → servicios → UI. Cada historia es independiente y se puede revertir por separado.

## 2. Cambios de dominio

`GymCore.Dominio/Entidades/Plan.cs`:

```csharp
public bool Activo { get; set; } = true;                  // inactivo: sin nuevas altas ni renovaciones
public DateTime? FechaAlta { get; set; } = DateTime.UtcNow; // null = creado antes de registrar la fecha
```

`Pago.UsuarioCajero` ya existía. Ahora se completa.

## 3. Cambios de persistencia (SQLite)

| Cambio | Detalle |
|---|---|
| `Planes.Activo` | `INTEGER NOT NULL DEFAULT 1`. Todos los planes existentes quedan **activos**. |
| `Planes.FechaAlta` | `TEXT NULL`. |
| `EsquemaSqlite` | Antes solo agregaba columnas nullables. Ahora también agrega NOT NULL si la propiedad declara la anotación `EsquemaSqlite.DefaultAlAgregar` (literal SQL para completar filas existentes). Sigue sin eliminar ni modificar columnas. |
| `PlanConfig` | `b.Property(x => x.Activo).HasAnnotation(EsquemaSqlite.DefaultAlAgregar, "1")` |

**Por qué no `HasDefaultValue(true)`:** con `bool`, EF trata `false` como "sin valor" (*sentinel*) y omitiría la columna al insertar, así que la base pondría `true` y nunca podría crearse un plan inactivo. La anotación propia solo actúa al agregar la columna y no cambia cómo EF inserta.

Verificado sobre una copia de la base actual: columnas agregadas, 2 planes activos, pagos y suscripciones intactos.

## 4. Cambios de UI

| Pantalla | Cambios |
|---|---|
| Planes (listado) | Badge **Activo** (verde) / **Inactivo** (gris) con tooltip. Filtros Activos/Inactivos. Columna **Creado** (ordenable). Acción Activar/Desactivar con modal de confirmación que indica cuántos socios conservan el plan. Filas inactivas atenuadas. El KPI "Planes activos" ahora cuenta `Activo`. |
| Plan (ficha) | Banner si está inactivo (con acceso rápido a "Activar"). "Creado el" en la cabecera. **Creado el** y **Antigüedad del plan** en la franja de datos. Activar/Desactivar en el menú ⋯. |
| Alta de socio / Nueva suscripción | Solo planes activos, con la ayuda "Solo planes activos". Mensaje si no hay ninguno. |
| Pagos (listado) | Columna **Registró** siempre visible (tooltip con fecha y hora), o "Sin registro" en pagos anteriores. Buscable y ordenable. |
| Registrar pago | Barra de acciones: "Registrará: *operador*". |
| Detalle de pago | Tarjeta **Auditoría del cobro**: operación, fecha, hora, usuario responsable, marca UTC original y estado Trazable / Sin trazabilidad. |
| Panel | 6 KPIs reales (socios activos, morosos, pagos del día, recaudación mensual con variación vs. el mismo tramo del mes anterior, plan más contratado, próximos vencimientos). Gráfico de recaudación de 6 meses, alertas, próximos vencimientos y últimos pagos con datos reales. Se quitaron las alertas sin respaldo ("Inventario bajo", "Meta mensual"). Los módulos sin desarrollar muestran "Próximamente" en lugar de enlaces `#`. |
| Layout | Sidebar: cobranza de hoy, cobranza del mes, socios activos y pagos pendientes (resaltado si hay). Campana: cantidad de alertas con su detalle en el tooltip. Menú de usuario con el operador real. |

## 5. Cambios de ViewModels y servicios

| Archivo | Rol |
|---|---|
| `Componentes/UsuarioActual.cs` | `IUsuarioActual` (Nombre, Rol, Iniciales). Registrado en `Program.cs` junto con `AddHttpContextAccessor`. |
| `Componentes/IndicadoresGimnasio.cs` | Records `Cobranza` y `Membresias`, y los métodos `DelRequestAsync`, `PlanMasContratadoAsync` y `RecaudacionMensualAsync`. |
| `Componentes/SidebarMetricasViewComponent.cs` | Vistas `Sidebar` y `Alertas` en `Pages/Shared/Components/SidebarMetricas/`. Si la consulta falla, el layout se muestra igual, sin cifras. |
| `Pages/Planes/PlanAcciones.cs` | `AlternarEstadoAsync`, compartido por listado y ficha (handler `ToggleEstado`). |
| `Pages/Planes/PlanPresentacion.cs` | `Estado(Plan)`, `FechaAlta`, `Creado`, `Antiguedad`, `TextoCambioEstado`, `Conservan`. |
| `PanelModel`, `Pagos/IndexModel` | Consumen `IndicadoresGimnasio` en lugar de cálculos propios. |

## 6. Riesgos detectados

| Riesgo | Mitigación / estado |
|---|---|
| **No hay autenticación.** "Usuario responsable" es el operador configurado, no una persona autenticada. | El servicio ya prefiere la identidad autenticada. Al agregar login (Identity, Windows, etc.) se registra automáticamente al usuario real. Hasta entonces, configurar `Operador:Nombre` por puesto de caja. |
| `UsuarioCajero` es texto libre, no una FK a usuarios. | Suficiente para auditoría básica. Con login conviene guardar también el Id del usuario. |
| Pagos anteriores sin cajero. | Se muestran como "Sin trazabilidad". No se completan retroactivamente, para no falsear la auditoría. |
| Planes anteriores sin fecha de alta. | "Sin registro" y antigüedad "—". Se ordenan al final al ordenar por fecha. |
| El layout calcula métricas en cada página (carga el resumen de todos los socios). | Memorizado por request. A gimnasios de este tamaño no les impacta. Con miles de socios, cachear 30–60 s e invalidar al registrar un pago. |
| La campana suma alertas por tipo: un socio puede figurar como "por vencer" y "con saldo" a la vez. | El tooltip muestra el desglose. Si se prefiere, cambiar a socios distintos. |
| Activar/desactivar no deja historial de quién lo hizo. | Recomendado: tabla de eventos de plan, como `EntrenadorEvento`, usando `IUsuarioActual`. |
| Bases creadas desde cero con `EnsureCreated` tienen `Activo NOT NULL` sin DEFAULT. | No afecta: EF siempre envía el valor al insertar. |

## 7. Checklist de pruebas manuales

**Antes de empezar:** hacé un backup de `data/gym.db` y arrancá la app.

**Actualización de esquema**
- [ ] La app arranca sin errores sobre la base existente.
- [ ] Planes existentes figuran como **Activo** y "Creado: Sin registro".
- [ ] Socios, suscripciones y pagos previos siguen visibles y con los mismos importes.

**H1 – Activar / desactivar**
- [ ] Listado → ⋯ → Desactivar en un plan con socios: el modal dice cuántos lo conservan. Al confirmar aparece el mensaje y el badge gris.
- [ ] Filtro "Inactivos" muestra solo ese plan.
- [ ] Socios → Nuevo socio: el plan inactivo no aparece en la lista.
- [ ] Ficha de un socio → Renovar membresía: el plan inactivo no aparece.
- [ ] El socio que ya tenía ese plan lo sigue viendo vigente y su credencial sigue habilitada. El control de ingreso lo deja pasar.
- [ ] Ficha del plan inactivo: banner gris → "Activar" → vuelve a aparecer en altas.
- [ ] Desactivar todos los planes → Nueva suscripción muestra "No hay planes activos".

**H2 – Fecha de creación**
- [ ] Crear un plan: la columna "Creado" muestra la fecha de hoy y la ficha "Creado el … · Antigüedad 0 d".
- [ ] Duplicar un plan: la copia tiene fecha de hoy, no la del original.
- [ ] Ordenar por "Creado" en ambos sentidos.

**H3 – Trazabilidad**
- [ ] Registrar pago: la barra inferior dice "Registrará: *operador*".
- [ ] En Pagos, la columna "Registró" muestra al operador. Al pasar el mouse se ven la fecha y la hora.
- [ ] Detalle del pago: tarjeta "Auditoría del cobro" en estado **Trazable**, con fecha, hora (con segundos), usuario y marca UTC.
- [ ] Un pago anterior al sprint: "Sin trazabilidad" / "No registrado".
- [ ] Cambiar `Operador:Nombre` en `appsettings.json`, reiniciar y registrar otro pago: queda el nuevo nombre, y el menú de usuario también lo muestra.

**H4 – Dashboard**
- [ ] No aparecen nombres ni montos de ejemplo (María López, Carla Ruiz, $18.400, +12,4 %…).
- [ ] "Socios activos" y "Socios morosos" coinciden con los KPIs de la pantalla Socios.
- [ ] "Recaudación mensual" coincide con "Recaudación del mes" de Pagos.
- [ ] Registrar un pago y volver al Panel: suben "Pagos del día" y la recaudación, y aparece primero en "Últimos pagos".
- [ ] Gráfico: los meses sin cobros se ven en cero y el tooltip muestra el monto.
- [ ] Botones del header y de los módulos llevan a pantallas reales. Reportes, Insumos y Configuración dicen "Próximamente".

**H5 – Sidebar**
- [ ] La tarjeta muestra la cobranza de hoy, la del mes, los socios activos y los pagos pendientes. Coinciden con el Panel.
- [ ] Con deudas, "Pagos pendientes" se resalta en ámbar. Clic en la tarjeta lleva a Pagos.
- [ ] La campana muestra el total de alertas y el desglose en el tooltip. Sin alertas, no muestra el número.
- [ ] Responsive: en móvil, abrir el menú lateral y verificar la tarjeta.
