# Sprint: anulación de pagos, auditoría y alertas accionables

> **Actualización:** desde el [sprint de autenticación](sprint-autenticacion.md) el responsable sale del usuario autenticado; la configuración `Operador:Nombre` ya no existe.

Seis historias que profesionalizan la operación sin cambiar la línea visual. Ningún pago se borra, la información histórica se conserva y la base existente se actualiza sola al arrancar la app.

## 1. Diseño UX/UI

| Historia | Dónde | Cómo se ve y se usa |
|---|---|---|
| H1 Anulación | Detalle de pago | Botón **Anular** (contorno rojo) junto a Imprimir y Descargar, también en "Acciones". Abre un modal "Confirmar anulación del pago" con los motivos como opciones (Cobro duplicado / Error de importe / Pago registrado por error / Otro) y un detalle opcional, obligatorio con "Otro". El botón queda deshabilitado hasta que el motivo es válido. El modal muestra a nombre de quién quedará la anulación. Después de anular: badge **ANULADO** en gris oscuro, banner con fecha, operador y motivo, marca de agua "ANULADO" en el comprobante (también al imprimir), filas de anulación en "Auditoría del cobro", y se oculta "Reenviar". |
| | Listado de pagos | Estado **Anulado** (badge oscuro, con el motivo en el tooltip), importe tachado y filtro por estado "Anulado". |
| | Historial del socio | El pago anulado sigue en el timeline (tachado). El total del mes no lo suma. |
| H2 Auditoría | Sidebar › Administración › **Auditoría** | 4 KPIs (eventos de hoy, últimos 7 días, operadores activos, anulaciones del mes con acceso directo). Filtros por texto, desde/hasta, tipo de evento (agrupado por módulo), entidad y usuario, compartibles por URL. Timeline agrupada por día con chip de entidad, enlace a la ficha, resumen, detalle y responsable. Paginación de a 50 y **Exportar CSV** (compatible con Excel). |
| H3 Actividad reciente | Panel | Tarjeta "Actividad reciente" con los últimos 10 eventos en timeline compacta y enlace a la auditoría completa. |
| H4 Centro de alertas | Campana del header | Desplegable con 5 alertas reales, cada una con cantidad y acceso directo: 🔴 pagos vencidos → Socios "Vencidos" · 🟠 por vencer en 7 días → Socios "Próximos a vencer" · 🟡 sin entrenador → Socios "Sin entrenador" · 🔵 planes inactivos → Planes "Inactivos" · 🟢 nuevos del mes → Socios "Nuevos del mes". El número de la campana cuenta solo lo urgente (vencidos + por vencer). |
| H5 KPIs accionables | Panel | Cada tarjeta suma un botón al pie: Socios activos → **Ver socios** · Morosos → **Ver listado** · Pagos del día → **Registrar pago** · Cobranza del mes → **Ver pagos** · Plan más contratado → **Ver plan** · Vencimientos → **Gestionar vencimientos**. |
| H6 Historial de entidades | Fichas | **Planes**: pestaña nueva "Historial". **Socios**: "Actividad" pasa a llamarse "Historial" y suma las acciones administrativas (edición, baja/reactivación, credencial reemitida, anulaciones) con su responsable. **Entrenadores**: el historial existente ahora muestra quién hizo cada cambio. |

Componentes reutilizados: `_StatCard` (con acción nueva), `_EmptyState`, `status-pill`, `timeline`, `audit-user`, `data-card`, `filter-chips` y el modal del Design System. Componente nuevo: **`_EventosTimeline`**, usado en la auditoría, la actividad reciente y el historial de planes.

## 2. Wireframes

### Detalle de pago con anulación
```
Pago R-000123 ⚫ ANULADO                 [⬇ PDF] [🖨 Imprimir] [⊗ Anular]
┌───────────────────────────────────────────────────────────────────────┐
│ ⊗ Pago anulado el 14/10/2026 19:42 por Germán. Motivo: Cobro duplicado│
└───────────────────────────────────────────────────────────────────────┘
┌ Comprobante ───────────────────┐  ┌ 🛡 Auditoría del cobro  [Anulado]┐
│        A N U L A D O           │  │ Operación      R-000123          │
│ Socio · Concepto · Medio …     │  │ Fecha / Hora   14/10 19:30:05    │
│ Anulación: 14/10 · Germán · …  │  │ Responsable    (G) Germán        │
│ Total (anulado)    $̶ ̶1̶.̶5̶0̶0̶     │  │ Anulado el     14/10 19:42:11    │ ← fondo rojo suave
└────────────────────────────────┘  │ Anuló          (G) Germán        │
                                    │ Motivo         Cobro duplicado   │
                                    └──────────────────────────────────┘
┌ ⚠ Confirmar anulación del pago ──────────────────────┐
│ R-000123 · $ 1.500 · Ana Pérez. No se borra: queda   │
│ como anulado y deja de contar en saldos e indicadores│
│ Motivo*  [Cobro duplicado] [Error de importe]        │
│          [Registrado por error] [Otro]               │
│ Detalle  [                              ] (opcional) │
│ ✓ Quedará registrado a nombre de Germán              │
│                     (Cancelar) (Confirmar anulación) │
└──────────────────────────────────────────────────────┘
```

### Administración › Auditoría
```
[⚡ Hoy 12] [📅 7 días 85] [👥 Operadores 2] [⊗ Anulaciones 1 → Ver anulaciones]
┌───────────────────────────────────────────────────────────────────────┐
│ [🔍 Socio, plan, importe, motivo…]                                    │
│ Desde[  ] Hasta[  ] Tipo[Todos ▾] Entidad[Todas ▾] Usuario[▾] [Filtrar]│
├───────────────────────────────────────────────────────────────────────┤
│ HOY                                                                   │
│ (⊗) Pago anulado                                              19:42   │
│     [PAGO] Pérez, Juan · R-000123 · $ 1.500 · Cobro duplicado         │
│     (G) Germán                                                        │
│ ($) Pago registrado                                           19:30   │
│     [PAGO] Pérez, Juan · $ 1.500 · Efectivo · Cuota Mensual           │
├───────────────────────────────────────────────────────────────────────┤
│ Mostrando 1–50 de 85        [‹ Anteriores] Página 1 de 2 [Siguientes ›]│
└───────────────────────────────────────────────────────────────────────┘
```

### Centro de alertas
```
                                   🔔③
              ┌ Centro de alertas ─────────────────────┐
              │ 3 requieren atención                   │
              │ (⊗) Pagos vencidos              [ 1 ] ›│
              │ (⏰) Membresías por vencer       [ 2 ] ›│
              │ (👤) Socios sin entrenador       [ 4 ] ›│
              │ (⏸) Planes inactivos            [ 1 ] ›│
              │ (✨) Nuevos socios               [ 3 ] ›│
              │ Ir al panel de gestión →               │
              └────────────────────────────────────────┘
```

### KPI accionable
```
┌──────────────────────────────┐
│ (!) Socios morosos       $ 40.000 │
│     5                         │
│ [ Ver listado            → ]  │
└──────────────────────────────┘
```

## 3. Modelo de dominio

```
Pago (existente)                         EventoSistema (nuevo, solo inserción)
├─ … importe, medio, UsuarioCajero       ├─ Id (long)            ├─ Entidad: Socio|Plan|Pago|Entrenador
├─ Anulado          (private set)        ├─ FechaUtc             ├─ EntidadId?   ← sin FK: sobrevive a borrados
├─ FechaAnulacion   (private set)        ├─ Usuario              ├─ EntidadNombre (copia al momento)
├─ MotivoAnulacion  (private set, ≤300)  ├─ Tipo                 ├─ SocioId?     ← historial del socio
├─ UsuarioAnulo     (private set)        ├─ Resumen (≤500)       └─ Detalle (≤4000)
└─ Anular(motivo, usuario, fechaUtc)     └─ Referencia (no se persiste: entidad nueva sin Id)
     · lanza si ya está anulado o falta motivo/usuario
                                         EntrenadorEvento (existente) + Usuario
```

La anulación es un **método de dominio**: no hay forma de marcar un pago como anulado sin motivo, responsable y fecha.

## 4. Eventos auditables

| Módulo | Evento | Se registra en |
|---|---|---|
| Pagos | Pago registrado · Pago anulado | `Pagos/Nuevo` · `Pagos/Recibo` (handler Anular) |
| Socios | Alta de socio · Datos actualizados (lista de campos que cambiaron) · Baja · Reactivación · Nueva suscripción · Membresía renovada · Credencial reemitida | `Socios/Create` · `Socios/Edit` · `SocioAcciones` · `Suscripciones/Nueva` · `Socios/Detalle` |
| Planes | Creado (indica si se duplicó de otro) · Modificado (lista de campos) · Activado · Desactivado · Eliminado | `Planes/Create` · `Planes/Edit` · `PlanAcciones` · `Planes/Delete` |
| Entrenadores | Alta · Modificación / cambio de horario · Activación · Desactivación · Eliminación · Asignación / desasignación de socio · Observación | `RegistrarEvento` (refleja automáticamente el historial propio) · `EntrenadorAcciones` |

Los valores del enum `TipoEventoSistema` se agrupan por centena (100 pagos, 200 socios, 300 planes, 400 entrenadores) y **no deben reutilizarse**.

## 5. Cambios en base de datos (SQLite, no destructivos)

| Cambio | Detalle |
|---|---|
| `Pagos.Anulado` | `INTEGER NOT NULL DEFAULT 0`: ningún pago previo queda anulado. |
| `Pagos.FechaAnulacion`, `MotivoAnulacion`, `UsuarioAnulo` | Nullable. |
| `EntrenadorEventos.Usuario` | Nullable. Los eventos previos quedan sin responsable. |
| Tabla `EventosSistema` | Nueva, sin FKs, con índices por `FechaUtc`, `(Entidad, EntidadId)` y `SocioId`. |

Todo lo aplica `EsquemaSqlite` al arrancar. Verificado sobre una copia de la base real y con un test automatizado que simula una base anterior al sprint.

**Atomicidad:** el evento viaja en el mismo `SaveChanges` que la acción. Si la entidad es nueva (sin Id), `GymDbContext` guarda, completa el Id y vuelve a guardar **dentro de una transacción**: o quedan la entidad y su evento, o ninguno de los dos.

## 6. Impacto sobre indicadores actuales

Un **filtro global de EF** (`HasQueryFilter(p => !p.Anulado)`) excluye los anulados de **todas** las consultas de pagos, incluidas las futuras. Solo el listado, el detalle y los historiales los piden explícitamente con `IgnoreQueryFilters()`.

| Indicador | Efecto al anular |
|---|---|
| Recaudación del mes / del día (Pagos, Panel, sidebar), gráfico de 6 meses | Se resta el importe. |
| Saldo del período, morosos, Deudas, "Pagar total" en Registrar pago | El período vuelve a tener saldo. El socio puede pasar a moroso. |
| Ingresos generados del plan | Se resta. |
| Total pagado y cuotas pagadas en la ficha del socio | No lo cuentan. El timeline lo muestra tachado. |
| Últimos pagos del Panel | El anulado deja de figurar. |

## 7. Implementación (archivos principales)

| Capa | Archivos |
|---|---|
| Dominio | `Entidades/Pago.cs` (anulación), `Entidades/EventoSistema.cs`, `Enums/TipoEventoSistema.cs`, `Entidades/EntrenadorEvento.cs` (+Usuario) |
| Aplicación | `Abstracciones/IOperadorAuditoria.cs`: la persistencia solo conoce el nombre del operador. |
| Infraestructura | `GymDbContext.cs` (usuario e Id automáticos), `Config/EventoSistemaConfig.cs`, `Config/PlanConfig.cs` (filtro global + columnas) |
| Web: servicios | `Componentes/Auditoria.cs` (`db.Auditar(...)`, campos modificados, presentación de eventos), `UsuarioActual.cs` (+permisos), `IndicadoresGimnasio.cs` (+alertas), `SidebarMetricasViewComponent.cs` |
| Web: pantallas | `Pagos/Recibo` (anulación), `Administracion/Auditoria`, `Panel`, `Shared/Components/_EventosTimeline`, `_StatCard`, `SidebarMetricas/Alertas`, historiales en Socios, Planes y Entrenadores |
| Tests | `tests/GymCore.Tests` (xUnit + SQLite en memoria) |

### Preparado para login, roles y multi-gimnasio
- `IUsuarioActual.Puede(permiso)` con `Permisos.AnularPagos` y `Permisos.VerAuditoria`. Hoy, sin login, el operador configurado puede todo. Con autenticación se resuelve por claim `permiso` o rol `Admin`, sin tocar las pantallas: el botón Anular y el menú Auditoría ya respetan el permiso, y el servidor responde 403 si falta.
- La identidad autenticada tiene prioridad sobre el operador configurado: la auditoría la toma sola.
- Para multi-gimnasio: agregar `GimnasioId` a `EventoSistema` (y al resto de entidades) más un filtro global por tenant, igual al de anulados. El diseño sin FKs del evento lo permite sin migrar datos.

## 8. Casos de prueba automatizados

`dotnet test tests/GymCore.Tests`: 14 casos, todos en verde.

| Caso | Verifica |
|---|---|
| `Anular_completa_fecha_motivo_y_responsable` | Datos de anulación e importe original intactos. |
| `Anular_dos_veces_no_esta_permitido` | Se conserva el primer motivo. |
| `Anular_exige_motivo` (×2) | Motivo vacío o en blanco. |
| `Anular_recorta_motivos_demasiado_largos` | Límite de 300 caracteres. |
| `Pago_anulado_no_se_borra_y_queda_fuera_de_las_consultas_por_defecto` | Filtro global y `IgnoreQueryFilters`. |
| `Anular_el_pago_vuelve_a_dejar_saldo_pendiente` | Saldo y moroso (`SociosConsultas`). |
| `La_recaudacion_no_suma_pagos_anulados` | Cobranza del día y del mes, recaudación mensual. |
| `El_evento_toma_el_usuario_del_operador_actual` / `Sin_operador_el_responsable_es_Sistema` | Responsable automático. |
| `Evento_de_entidad_nueva_recibe_su_Id_al_guardar` | Id diferido de plan y socio, y `SocioId` en altas. |
| `Historial_de_entrenador_se_refleja_en_la_auditoria_con_responsable` | Reflejo del historial de entrenador. |
| `Los_campos_modificados_se_detectan_con_etiquetas_legibles` | Solo los cambios reales. |
| `Base_anterior_al_sprint_se_actualiza_y_conserva_los_pagos` | `NOT NULL DEFAULT 0` y tabla recreada sin perder datos. |

## 9. Checklist de validación manual

**Antes:** backup de `data/gym.db` y reiniciar la app (la instancia en curso no tiene el código nuevo).

**H1 – Anulación**
- [ ] Detalle de un pago → **Anular**: el botón "Confirmar" está deshabilitado hasta elegir un motivo. Con "Otro", el detalle se vuelve obligatorio.
- [ ] Confirmar: aparecen el mensaje, el badge ANULADO, el banner con fecha, operador y motivo, la marca de agua y las filas de anulación en la auditoría. "Reenviar" desaparece.
- [ ] Imprimir el comprobante anulado: la marca de agua sale en papel.
- [ ] Pagos: la fila figura como "Anulado", con el importe tachado. La recaudación del mes y del día bajan en ese importe, igual que el Panel y el sidebar.
- [ ] Si el pago completaba la cuota, el socio aparece en Deudas y como moroso, y "Pagar total" propone el saldo.
- [ ] Ficha del socio → Pagos: el pago sigue visible y tachado, y el total del mes no lo suma. En Historial figura "Pago anulado" con su responsable.
- [ ] El pago **no** se puede volver a anular (no aparece el botón).

**H2 – Auditoría**
- [ ] Menú Administración › Auditoría. Registrar un pago, editar un socio, desactivar un plan y asignar un entrenador: cada acción aparece con hora, usuario, entidad y enlace.
- [ ] Editar un socio sin cambiar nada: **no** genera evento. Cambiar el teléfono: "Cambió: teléfono".
- [ ] Filtrar por tipo, entidad, usuario, fechas y texto. Paginar. "Limpiar" quita los filtros.
- [ ] Exportar CSV: abre en Excel con acentos correctos y los filtros aplicados.
- [ ] Eliminar un plan sin uso: el evento queda en la auditoría, sin enlace roto.

**H3 – Actividad reciente**
- [ ] El Panel muestra los últimos 10 eventos. En una base sin eventos se ve el estado vacío.

**H4 – Centro de alertas**
- [ ] La campana abre el desplegable con 5 alertas y sus cantidades. Cada una lleva al listado ya filtrado (Socios "Vencidos", "Próximos a vencer", "Sin entrenador", "Nuevos del mes"; Planes "Inactivos").
- [ ] El número de la campana es vencidos + por vencer. Sin pendientes urgentes, no muestra número.

**H5 – KPIs accionables**
- [ ] Cada tarjeta del Panel tiene su botón y lleva a la pantalla indicada. Las cifras coinciden con las de Socios y Pagos.

**H6 – Historiales**
- [ ] Plan → Historial: creación, modificación (con campos) y activación/desactivación con responsable.
- [ ] Socio → Historial: alta, suscripciones, pagos (con cajero), asignaciones, ediciones y bajas.
- [ ] Entrenador → Historial: los eventos nuevos muestran el responsable. Los anteriores, no.

**Regresión**
- [ ] Registrar pago, alta de socio con plan, renovación, asignar/quitar entrenador, control de ingreso, recibo e impresión siguen funcionando igual.
- [ ] Responsive: modal, centro de alertas y filtros de auditoría en móvil.
