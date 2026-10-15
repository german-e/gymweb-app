# Sprint: Inteligencia de negocio

Una capa analítica para dueños, gerentes y supervisores, **encima** del Panel operativo (que no cambia). Responde en segundos: ¿crezco o pierdo socios? ¿cuánto facturo? ¿qué plan se vende más? ¿qué entrenador tiene más socios? ¿quién se puede ir? ¿cómo evoluciona el negocio?

Todos los números salen de los datos reales. Los pagos anulados nunca suman, y cada indicador declara su fórmula en un tooltip.

## 1. Diseño UX/UI

| Pantalla | Propósito | Contenido |
|---|---|---|
| **Resumen ejecutivo** `/Reportes` | Entender el negocio en 30 segundos | "Lo que está ocurriendo en tu gimnasio" (insights automáticos) · 8 KPIs con valor, variación coloreada según sea buena o mala, comparación nombrada y tendencia de 12 meses · alertas gerenciales · resumen de riesgo · accesos a los reportes |
| **Socios** | ¿Crezco o pierdo socios? | Filtro de período · activos, altas, bajas, neto y retención · línea de activos al cierre de cada mes · columnas de altas contra bajas · columnas divergentes de crecimiento neto · tabla por mes |
| **Ingresos** | ¿Cuánto facturo? | KPIs del día, del mes, del año e ingreso por socio · evolución mensual · comparación anual (año en curso contra el anterior en gris) · últimos 30 días · podio de mejores meses · peor mes completo · tablas |
| **Planes** | ¿Qué se vende? | Más y menos vendido · ingreso por planes · podio 1°/2°/3° · donut de socios activos por plan · barras de ventas e ingreso · tabla ordenable |
| **Entrenadores** | Carteras y participación | Activos, mayor y menor cartera, promedio, socios sin entrenador · ranking con participación % · nuevas asignaciones · tabla |
| **Riesgo de abandono** | ¿Quién se puede ir? | Conteos por nivel con ícono · metodología explicada · tabla con días sin asistir, membresía, factores con puntos, puntaje, nivel, recomendación y acciones (ficha, WhatsApp con mensaje armado) |
| **Exportaciones** | Llevarse los datos | Seis reportes en Excel y PDF, según el período elegido. Cada exportación queda auditada. |

**Sistema visual (sin cambios de estilo):** mismas tarjetas, radios, sombras, Inter, íconos, `page-header`, `data-card`, `filter-chips` y estados vacíos. Navegación: sección "Inteligencia de negocio" en el menú lateral más una subnavegación del módulo que conserva el período. El filtro de período va en **una sola fila arriba** y afecta a toda la pantalla. Los gráficos muestran un skeleton hasta que se dibujan, con tooltips en gráficos y KPIs, y la grilla pasa de 4 a 2 y a 1 columna en móvil.

**Reglas de visualización aplicadas** (guía de dataviz):
- **Paleta validada** con el validador (CVD y contraste) sobre la superficie blanca de la app. Los tonos con contraste menor a 3:1 se compensan con etiquetas visibles y tabla equivalente.
- Trazos de 2 px y barras de hasta 24 px con extremo de datos redondeado.
- Grilla de línea fina, sólida y discreta.
- **Nunca doble eje**: dos magnitudes distintas van en dos gráficos.
- **El color sigue a la entidad**, no a su posición en el ranking (donut por plan).
- El crecimiento neto es **divergente** (azul crece, rojo pierde). La comparación anual usa **énfasis** (año actual en acento, anterior en gris).
- Los colores de estado (riesgo) son exclusivos y van **siempre con ícono y texto**.
- Cada gráfico tiene leyenda (≥2 series), tooltip con todas las series (mouse, touch y teclado con flechas) y **tabla equivalente**.
- Las etiquetas se insertan con `textContent`, porque son datos de usuario.

## 2. Wireframes

### Resumen ejecutivo
```
INTELIGENCIA DE NEGOCIO
Resumen ejecutivo                                   [Riesgo de abandono] [Exportar]
[Resumen | Socios | Ingresos | Planes | Entrenadores | Riesgo |      Exportaciones]
┌ ✦ Lo que está ocurriendo en tu gimnasio ─────────────────────────── Automático ┐
│ (↗) La recaudación aumentó un 10,1% vs. septiembre ($ 1.367.000)            →  │
│ (👥) El gimnasio creció 7 socios activos en el mes (hoy son 114)            →  │
│ (🏆) El plan Mensual Ilimitado lidera las ventas (103 contrataciones)       →  │
│ (!) Hay 26 socios con riesgo de abandono (14 alto, 12 medio)                →  │
└──────────────────────────────────────────────────────────────────────────────────┘
┌Socios activos ⓘ┐┌Nuevos del mes  ┐┌Bajas del mes   ┐┌Recaudación mes ┐
│ 114            ││ 9              ││ 2              ││ $ 1.367.000    │
│[▲ 6,5%] vs 107 ││[▲ 50%] vs 6    ││[▲ 100%] vs 1   ││[▲ 10,1%]       │   verde = bueno
│ ╱‾‾╲__╱‾‾‾‾•   ││ __╱╲_╱‾╲_•     ││ ‾╲_╱‾╲__╱•     ││ _╱‾╲╱‾‾╲_╱•    │   rojo = malo
└────────────────┘└────────────────┘└────────────────┘└────────────────┘
 Recaudación anual · Morosidad (pp) · Renovaciones · Asistencia promedio
┌ Alertas gerenciales ───────────────┐┌ Riesgo de abandono ──────────────────┐
│▌⚠ Socios con riesgo alto [Ver]     ││ [⛔ 14 Alto] [⚠ 12 Medio] [✓ 93 Bajo] │
│▌ⓘ Entrenadores sin socios [Ver]    ││ ▇▇▇▇▇▅▅▅▅▅▃▃▃▃▃▃▃▃▃▃▃▃▃▃▃▃▃▃▃▃▃▃▃▃▃   │
│▌ⓘ Planes sin contrataciones [Ver]  ││ · Ana López — Contactar para renovar │
└────────────────────────────────────┘└──────────────────────────────────────┘
```

### Socios · Ingresos · Planes
```
Período: [Últimos 3 meses][Últimos 6 meses][Últimos 12 meses][Personalizado ▸ desde–hasta]   01/05–15/10
[Activos 114] [Altas 58] [Bajas 31] [Neto +27] [Retención 78%]
┌ Socios activos al cierre de cada mes ─────────────────────────────┐
│ 150┤                                         ╭──────• 114          │  crosshair + tooltip
│  75┤          ╭───────────────╮──────────────╯                     │
│   0┼──────────────────────────────────────────────────────────     │
│     Nov  Dic  Ene  Feb  Mar  Abr  May  Jun  Jul  Ago  Sep  Oct     │
└───────────────────────────────────────────────────────────────────┘
┌ Altas y bajas ■ Altas ■ Bajas ─┐  ┌ Crecimiento neto ■ Crece ■ Pierde┐
│  ▌▌  ▌▖  ▌▌  ▌▖  ▌▌  ▌▌          │  │  ▆   ▃   ▅       ▇   ▄          │
│                                  │  │ ─────────────▆─────────────     │
└──────────────────────────────────┘  └──────────────────────────────────┘
Planes:  1° Mensual Ilimitado  103 ventas  $ 2,9 M     (◯ donut socios por plan + leyenda con % )
         2° Mensual 3xSemana    …
```

### Riesgo de abandono
```
[⛔ 14 Riesgo alto · Contactar esta semana] [⚠ 12 Medio · Seguimiento] [✓ 93 Bajo]
▸ Cómo se calcula el riesgo
[🔍 Nombre o DNI]  [Todos 119 | ⛔ Alto 14 | ⚠ Medio 12 | ✓ Bajo 93]
SOCIO          DÍAS SIN ASISTIR  MEMBRESÍA   FACTORES                      RIESGO        RECOMENDACIÓN            ⋯
(AL) López, Ana        30        [Vencida]   +40 No asiste hace 30 días    [⛔ Alto]      Contactar para renovar…  👤 💬
                                             +30 Membresía vencida…        ▇▇▇▇▇▇▇ 70/100
```

## 3. Arquitectura de métricas

```
           ┌──────────────── una carga por request (scoped) ────────────────┐
GymDbContext ──► IFuenteHechos.ObtenerAsync() ──► HechosNegocio (modelo de lectura en memoria)
                 (FuenteHechosDb)                  socios · suscripciones · pagos (sin anulados)
                  ▲ seam de caché                   asistencias · asignaciones · planes · entrenadores
                  ▲ seam de tenant/sucursal         + índices perezosos y memo de "activos en el día d"
                                                          │
                              MetricasNegocio (funciones PURAS, sin acceso a datos)
                              KPIs · Socios · Ingresos · Planes · Entrenadores · Riesgo · Alertas · Insights
                                                          │
                              ServicioReportes (orquesta, memoriza KPIs y riesgo por request)
                                   │                                   │
                          Páginas /Reportes/*                   Exportador (Excel / PDF)
```

- **Sin cálculos duplicados:** cada indicador tiene una sola definición en `MetricasNegocio`, que usan pantallas, insights, alertas y exportaciones. Las comparaciones de período (`Periodo.MismoTramoMesAnterior`, etc.) son las mismas para el **Panel, el sidebar y los reportes**. Por eso cambié la cobranza del Panel para que use esas definiciones: la recaudación del mes coincide en todas las pantallas.
- **Caché futura:** decorar `IFuenteHechos` (por ejemplo `FuenteHechosCacheada` con `IMemoryCache`, TTL de 1–5 min por gimnasio, invalidada al registrar o anular un pago). Las métricas no cambian.
- **Multi sucursal / multi gimnasio:** el filtro de alcance (`GimnasioId`/`SucursalId`) se aplica **solo** en `FuenteHechosDb`. Comparativas entre sucursales = el mismo `MetricasNegocio` sobre varios `HechosNegocio`.
- **Reportes avanzados:** cualquier análisis nuevo es una función pura más sobre `HechosNegocio`, testeable con hechos armados en memoria.

## 4. KPIs calculados

| KPI | Definición | Comparación |
|---|---|---|
| Socios activos | Sin baja y con una suscripción que cubre el día | Hoy vs. cierre del mes anterior |
| Socios nuevos del mes | Registrados (fecha de alta) en el mes | Mismos días del mes anterior |
| Bajas del mes | Activos al empezar el mes que hoy no lo están (baja o membresía sin renovar) | Mismos días del mes anterior |
| Recaudación del mes / anual | Pagos sin anulados del mes / del 1/1 a hoy | Mismo tramo del mes / del año anterior |
| Morosidad | % de activos con saldo en su suscripción vigente | Hoy vs. cierre anterior, **en puntos porcentuales** |
| Renovaciones | Suscripciones iniciadas por socios que ya tenían otra | Mismos días del mes anterior |
| Asistencia promedio | Ingresos por día en el mes | Mismos días del mes anterior |
| Altas / bajas / neto (por mes) | Cambio en el conjunto de activos entre el inicio y el cierre del mes. **Neto = altas − bajas** por construcción | — |
| Retención | % de los activos al inicio del período que siguen activos al final | — |
| Ingreso por socio | Promedio mensual de (recaudación ÷ activos al cierre) | Período anterior equivalente |
| Ventas de un plan | Suscripciones iniciadas en el período (altas y renovaciones) | — |
| Cartera de un entrenador | Asignaciones activas de socios sin baja · participación % | — |

Cada tarjeta muestra variación %, ▲/▼, color según sea **bueno o malo** (subir la morosidad es malo), el valor anterior y una tendencia de 12 meses. Si la base es 0 no se inventa un porcentaje; por encima de +300% se muestra el multiplicador ("▲ 23,9×").

**Riesgo de abandono (0–100):** inasistencia (8–13 días +10, 14–20 +25, 21 o más +40, nunca vino +35) · membresía vencida sin renovar +30 · saldo pendiente +20 · frecuencia en baja (4 semanas contra las 4 anteriores: −25% +10, −50% +20) · vence en 7 días o menos sin renovación +10. Niveles: **alto ≥ 50**, **medio 25–49**, **bajo < 25**. La recomendación sigue la acción más útil (renovar → bienvenida → contactar → cobrar → revisar rutina → ofrecer renovación).

**Alertas gerenciales:** caída de ingresos (≤ −15%) · aumento de morosidad (≥ +3 pp y ≥ 10%) · disminución de asistencia (≤ −20%) · dos meses seguidos con neto negativo · socios con riesgo alto · entrenadores activos sin socios · planes activos sin contrataciones en 3 meses.

## 5. Servicios nuevos

| Clase | Rol |
|---|---|
| `Reportes/Periodo` | Presets 3/6/12 meses y personalizado (acotado a 36 meses), período anterior, mismo tramo del mes y del año, y conversión UTC ↔ fecha local |
| `Reportes/HechosNegocio` (+ registros `SocioH`, `SuscripcionH`, `PagoH`, …) | Modelo de lectura con `EstaActivo`, `ActivosEn` (memoizado), `MorosoEn`, `Recaudado` y `Renovaciones` |
| `Reportes/IFuenteHechos` · `FuenteHechosDb` | Carga única y liviana por request |
| `Reportes/MetricasNegocio` | Todas las métricas (puras) |
| `Reportes/ServicioReportes` | Orquestación para las páginas |
| `Reportes/Exportador` | Una `TablaReporte` por reporte → Excel (ClosedXML) y vista PDF, más auditoría |
| `Reportes/Presentacion` | Formatos, variaciones, estilos de riesgo y base `ReportesPageModel` (permiso y período) |
| `wwwroot/js/charts.js` | Línea, columnas agrupadas, divergentes, donut y sparklines (SVG propio, sin dependencias) |

## 6. Cambios de dominio

**No hay entidades nuevas ni columnas nuevas.** Todo se reconstruye con los datos existentes. Solo se agregan:
- `TipoEventoSistema.ReporteExportado` (700) y `EntidadAuditada.Reporte`.
- El permiso **`REPORTES_EXPORTAR`** (sensible), además de `REPORTES_VER`.

Decisión clave: la app no cambia el estado de las suscripciones, así que **la vigencia histórica se reconstruye por fechas de cobertura**. Esto también corrige que `FechaBaja` se pisa al reactivar un socio: las bajas se miden como "dejó de estar activo".

## 7. Consultas optimizadas

- Seis consultas por request, todas `AsNoTracking` con **proyección solo de las columnas necesarias** (sin entidades EF). Las sumas de dinero se hacen en memoria (SQLite no agrega `decimal`).
- Ventanas de carga: pagos de 25 meses (comparación interanual mes a mes), asistencias de 13 meses, y la última asistencia histórica por `GROUP BY`.
- **Índices nuevos** (se crean solos al arrancar): `Pagos(FechaPago)`, `Asistencias(FechaHoraIngreso)` y `Asistencias(SocioId, FechaHoraIngreso)`.
- Medido con 151 socios, 826 suscripciones, 797 pagos y 6.297 asistencias: Resumen 350 ms en frío y el resto de las pantallas 40–110 ms.

## 8. Código

Páginas en `Pages/Reportes/`: `Index`, `Socios`, `Ingresos`, `Planes`, `Entrenadores`, `Riesgo`, `Exportaciones`, `Imprimir`, más los parciales `_Nav`, `_FiltroPeriodo`, `_KpiTile`, `_Viz` y `_BarrasH`. Estilos en `components.css` (sección "Inteligencia de negocio") y `reporte-impresion.css`.

**Permisos:** el módulo pide `REPORTES_VER`. Exportaciones y PDF piden `REPORTES_EXPORTAR`. Los roles iniciales nuevos de Supervisor ya traen `REPORTES_EXPORTAR`; **si el rol Supervisor ya existía en tu base, agregáselo desde Administración › Roles**. Administrador tiene acceso total.

## 9. Casos de prueba

`dotnet test tests/GymCore.Tests`: **59 en verde** (15 nuevos).

| Tipo | Casos |
|---|---|
| Unitarios (`ReportesTests`) | Presets y período anterior; período personalizado invertido o en el futuro, acotado a 36 meses; mismo tramo con meses cortos y año anterior. Actividad por cobertura y baja. **Altas − bajas = neto** en cada mes con 40 socios generados. Variación %, puntos porcentuales, sentido bueno o malo y multiplicador. KPIs ejecutivos sobre un escenario concreto (activos, nuevos, recaudación +50%, morosidad 33,3%). Mejores meses y peor mes **completo**. Ranking de planes (ventas, ingreso, participación, menos vendido). Cartera de entrenadores sin bajas, que suma 100%. Riesgo alto, medio y bajo, fuera de alcance y frecuencia en baja. Alertas por umbral e insights con los números reales. |
| Integración (app real) | Recepción no accede a reportes y no ve el menú. Todas las pantallas y períodos responden. **El pago anulado no aparece** en el resumen ni en ingresos. El Excel es un `.xlsx` válido y queda auditado. La vista PDF muestra el anulado en el detalle pero no lo suma. |
| Runtime de gráficos (jsdom) | `charts.js` ejecutado sobre el HTML real con datos sintéticos: 7 gráficos y 12 sparklines dibujados, tooltip por hover y navegación por teclado, sin errores de JS. |

Los dos errores reales que encontraron los tests ya están corregidos: el rango personalizado invertido en el futuro y la prioridad de la recomendación cuando la membresía está vencida.

## 10. Checklist de validación manual

**Antes:** backup de `data/gym.db`, reiniciar la app e iniciar sesión con un usuario que tenga `REPORTES_VER`.

- [ ] El menú lateral muestra "Inteligencia de negocio" solo a quien tiene el permiso. Recepción no la ve y por URL recibe acceso denegado.
- [ ] **Resumen:** los insights se leen como frases con números reales. Los 8 KPIs muestran variación y comparación. Pasar el mouse por ⓘ explica el cálculo. La "Recaudación del mes" coincide con el Panel y con Pagos.
- [ ] Un KPI que empeora (bajas o morosidad en subida) se ve en rojo; uno que mejora, en verde.
- [ ] Anular un pago de hoy: bajan la recaudación del Resumen, la de Ingresos y la del Panel.
- [ ] **Socios:** cambiar entre 3, 6 y 12 meses y un rango personalizado actualiza todo. En la tabla, Neto = Altas − Bajas en cada mes.
- [ ] Gráficos: el tooltip aparece con mouse y con Tab + flechas. En móvil, el gráfico se redibuja al rotar la pantalla.
- [ ] **Ingresos:** el mes en curso figura como "parcial" y no cuenta como peor mes. En la comparación anual, el año anterior se ve en gris.
- [ ] **Planes:** el podio coincide con la tabla. El donut tiene leyenda con valores y porcentajes, y cada plan conserva su color al cambiar el período.
- [ ] **Entrenadores:** el ranking suma el 100% de participación. "Socios sin entrenador" lleva al listado filtrado.
- [ ] **Riesgo:** filtrar por nivel, buscar y ordenar por puntaje. Los factores explican el puntaje, y WhatsApp abre con el mensaje armado.
- [ ] **Exportaciones:** descargar los 6 Excel (abren con formato de moneda y porcentaje, encabezado fijo y filtros). Cada PDF abre el diálogo de impresión con encabezado de marca y se guarda como PDF.
- [ ] Auditoría › "Inteligencia de negocio": cada exportación figura con usuario, formato y cantidad de filas.

## Limitaciones conocidas

- **Bajas del mismo mes:** un socio que entra y sale dentro de un mismo mes no cuenta como alta ni como baja (el neto sigue siendo exacto).
- **Interanual:** compara contra lo que hay en la base. En un gimnasio con menos de un año de datos, la variación anual se muestra como multiplicador y, sin base, no se muestra.
- **PDF:** se genera con la impresión del navegador ("Guardar como PDF"). Para PDF generado del lado del servidor (envío automático por email), evaluar QuestPDF (requiere licencia según facturación) o PdfSharp/MigraDoc (MIT).
- **Modo oscuro:** la app no lo tiene. Si se agrega, los tokens `.viz-root` tienen que pasar por el validador sobre la superficie oscura.
