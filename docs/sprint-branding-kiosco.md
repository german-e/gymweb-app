# Sprint: Navegación, branding multi-gimnasio y kiosco de ingreso

Objetivo: que cada gimnasio vea el sistema con su propia identidad, que la navegación escale sin ocupar espacio de más
y que el ingreso de socios tenga una experiencia propia de autoservicio. Sin cambios en reglas de negocio.

## 1. Arquitectura de branding

```
ConfiguracionGimnasio (BD, 1 fila hoy)
        │  ServicioBranding.ActualAsync()  ← cache en memoria (10 min, se invalida al guardar)
        ▼
Branding (record inmutable): nombre, eslogan, logos con respaldo, colores ya validados y ajustados,
        contacto, texto de credencial, preferencias de kiosco, VariablesCss
        │
        ├─ _Layout / _LayoutCuenta / _LayoutKiosco → <style>:root{--primary…}</style> + favicon
        ├─ _MarcaIsotipo, _Favicon (parciales)
        └─ Panel, Credencial, Recibo, Ticket, Reportes impresos, Excel, mensajes de WhatsApp
```

- **Sin valores fijos en las vistas:** todo lo que antes leía `Gimnasio:Nombre` de appsettings ahora usa el branding.
  Si no hay configuración guardada se toma `Gimnasio:Nombre` (compatibilidad) y la paleta original.
- **Colores como variables CSS:** los 34 azules fijos de `site.css`/`components.css` pasaron a `rgb(var(--primary-rgb) / α)`
  y `var(--primary)`. Con la paleta por defecto el aspecto es idéntico al anterior.
- **Contraste garantizado (WCAG):** el color principal se oscurece lo mínimo necesario para llegar a 3:1 sobre blanco
  (botones, íconos) y 4,5:1 en su variante de texto; el texto sobre el color (blanco o tinta) se elige por contraste.
  La pantalla de branding avisa cuando ajusta el tono.
- **Seguridad de imágenes:** PNG/JPG/WEBP (favicon PNG/ICO) validados por firma del archivo, con tope de tamaño;
  **SVG prohibido** (podría ejecutar scripts en el dominio). Los colores se validan con regex antes de llegar al CSS.
- **Preparado para multi gimnasio / white label:** `ServicioBranding` es el único punto que decide qué configuración
  aplica. Para multi tenant basta resolver la fila según el gimnasio/sucursal del request (dominio, usuario o
  claim) — vistas y CSS no cambian.

## 2. Wireframes del nuevo sidebar

```
┌──────────────────────────┐
│ [isotipo] Titán Fitness  │  ← logo reducido + nombre + eslogan (fijo arriba)
│           Entrená distinto│
├──────────────────────────┤
│ ▣ Panel                  │
│ ⌂ OPERACIÓN          4 ˅ │  ← grupos colapsables; el de la pantalla actual abierto
│ $ COMERCIAL          4 ˄ │
│    Planes                │
│    Pagos                 │
│  ▌ Punto de Venta        │  ← ítem activo con barra en color de marca
│    Ventas                │
│ ▤ INVENTARIO         4 ˅ │
│ ↗ INTELIGENCIA       3 ˅ │
│ ⚙ ADMINISTRACIÓN     3 ˅ │
│ [cobranza de hoy]        │  ← dentro del área con scroll (se oculta en pantallas bajas)
│            ⋮ scroll ⋮    │
├──────────────────────────┤
│ (DG) Duena Gimnasio  [⇥] │  ← usuario + rol + cerrar sesión: siempre visible
│      Administrador       │
└──────────────────────────┘
```

| Grupo | Ítems (cada uno solo con su permiso) |
|---|---|
| Operación | Socios / Mis socios · Evolución física · Entrenadores · Control de ingreso |
| Comercial | Planes · Pagos · Punto de Venta · Ventas |
| Inventario | Productos · Categorías · Stock y alertas · Movimientos |
| Inteligencia | Reportes · Reportes comerciales · Auditoría |
| Administración | Usuarios · Roles y permisos · Configuración |

Resultado por rol: **Entrenador** → Panel, Mis socios, Evolución física · **Recepcionista** → Operación, Comercial e
Inventario (sin Movimientos, Inteligencia ni Administración) · **Administrador** → todo.
Los grupos sin ítems no se muestran; con 8 ítems o menos todo arranca abierto. El estado abierto/cerrado se
recuerda por navegador. En tablet/móvil el menú es un panel deslizable con fondo para cerrar y tecla Escape.

## 3. Wireframes del kiosco (`/kiosk/checkin`)

```
LISTO                                   RESULTADO (5–6 s, tocar para continuar)
┌──────────────────────────────────┐    ┌──────────────────────────────────┐
│            [ LOGO ]              │    │  ✔ ACCESO AUTORIZADO    (verde)  │
│          ┌────────┐              │    │ ┌──────┐ Juan Pérez              │
│          │ ▌▌▌▌▌  │ ← láser      │    │ │ FOTO │ ¡Bienvenido/a, Juan!    │
│          └────────┘              │    │ │  JP  │ Plan · Vencimiento      │
│     Escanee su credencial        │    │ └──────┘ Entrenador · Hora       │
│  ● Listo para escanear           │    │ ▬▬▬▬▬▬▬▬▬▬▬▬▬▭▭  (cuenta regresiva) │
│  [ No tengo credencial · DNI ]   │    └──────────────────────────────────┘
│ 12:21  Viernes 16    Titán · tel │    ⚠ ACCESO RESTRINGIDO (naranja): vencida / día no
└──────────────────────────────────┘      incluido / cupo semanal → "Diríjase a recepción"
                                         ✖ ACCESO DENEGADO (rojo): "Código no válido"
ESPERA (sin actividad, opcional)
┌──────────────────────────────────┐    TECLADO TÁCTIL
│ [imagen]   PROMOCIÓN             │    ┌──────────────┐
│            2×1 en trimestral     │    │  40.000.012  │
│            Traé a un amigo…      │    │ 1  2  3      │
│ [iso] Titán · Escaneá…     ● ○   │    │ … ⌫  0  ✔    │
└──────────────────────────────────┘    └──────────────┘
```

Sin menú, sin barra superior; botón de pantalla completa; **salir requiere mantener presionado 1,5 s**; la pantalla se
mantiene encendida (Wake Lock) y el cursor se oculta. El campo del lector conserva el foco siempre (incluso sobre la
pantalla de espera: escanear ahí registra el ingreso al instante).

## 4. Modelo

```
ConfiguracionGimnasio                         AvisoRecepcion
  Id                                            Id, Tipo (Aviso | Promoción | Evento)
  NombreComercial, Eslogan                      Titulo, Texto, ImagenUrl
  LogoUrl, LogoReducidoUrl, FaviconUrl          FechaEvento (eventos)
  ImagenBienvenidaUrl                           VigenteDesde, VigenteHasta
  ColorPrincipal, ColorSecundario (#RRGGBB)     Activo, Orden, FechaAlta
  Direccion, Telefono, Email, SitioWeb          VigenteEn(día)
  TextoCredencial
  KioscoMensaje, KioscoSegundosResultado, KioscoSonido,
  KioscoPantallaEspera, KioscoSegundosInactividad, KioscoSegundosRotacion
  FechaModificacion, UsuarioModifico
```

Tablas nuevas `ConfiguracionGimnasio` y `AvisosRecepcion`: se crean solas al iniciar (sin migraciones destructivas).
Las imágenes se guardan en `wwwroot/uploads/branding/`.

## 5. Flujo de acceso

```
Lector (teclado + Enter) / teclado táctil
   → POST /kiosk/checkin?handler=Ingreso  (sesión del equipo con permiso ASISTENCIAS_REGISTRAR, antiforgery)
   → ServicioIngreso.RegistrarAsync(código, confirmarExtra: false)    ← mismas reglas que la recepción
        código vacío / no existe          → DENEGADO
        sin suscripción activa vigente    → RESTRINGIDO ("Tu membresía venció el …")
        sábado/domingo no incluido        → RESTRINGIDO
        ingreso en los últimos minutos    → AUTORIZADO (nota: ya estaba registrado; no duplica)
        cupo semanal alcanzado            → RESTRINGIDO (el cargo extra solo lo autoriza el personal)
        ok                                → AUTORIZADO + Asistencia registrada
   → JSON { estado, titulo, mensaje, socio{nombre, foto, plan, vence, entrenador}, hora, nota }
   → pantalla de resultado → vuelve sola a "Listo"
```

Las reglas se **extrajeron sin cambios** de la API `/api/asistencias/checkin` a `ServicioIngreso`; la API usa el
mismo servicio y devuelve exactamente el mismo JSON que antes (cubierto por pruebas).

## 6. Componentes reutilizables

| Componente | Uso |
|---|---|
| `Navegacion.Para(usuario)` | Menú como datos (grupos, ítems, activo); testeable por rol |
| `ServicioBranding` / `Branding` / `Colores` | Identidad y paleta con contraste garantizado |
| `ImagenesMarca` | Validación de imágenes por firma y tamaño |
| `_MarcaIsotipo`, `_Favicon` | Isotipo y favicon de la marca en cualquier layout |
| `_LayoutKiosco` | Layout de pantalla completa sin navegación |
| `_CampoImagen` + `CampoImagenVm` | Subida de imagen con vista previa, quitar y reglas visibles |
| `ServicioIngreso` | Reglas de ingreso para recepción, kiosco y futuros molinetes |
| `navegacion.js`, `kiosco.js`, `configuracion.js` | Comportamiento de menú, kiosco y configuración |

## 7. Cambios en la UI

- **Sidebar** nuevo (grupos, scroll propio, fondo uniforme, usuario fijo); en escritorio la barra superior muestra solo el avatar.
- **Configuración** (antes "Pronto"): hub, **Branding** (vista previa en vivo, paletas sugeridas, aviso de contraste,
  restablecer colores) y **Recepción y kiosco** (mensaje, tiempos, sonido, pantalla de espera, avisos con imagen y vigencia).
- **Login** con logo, colores e imagen de bienvenida (panel lateral en pantallas anchas).
- **Panel** con isotipo, nombre del gimnasio y saludo.
- **Credencial** con logo, colores, texto y datos de contacto del gimnasio (código de barras e identificación intactos).
- **Recibo, ticket, reportes impresos y Excel** con nombre/logo/contacto del gimnasio.
- **Control de ingreso** rediseñado con el Design System, historial de la sesión y acceso al modo kiosco.
- **Evolución física** (nueva): a quién le toca medir; el entrenador ve solo sus socios.
- Auditoría: grupo "Configuración" (eventos 900–904: identidad modificada, kiosco configurado, avisos creados/modificados/eliminados).

Correcciones encontradas en el camino:
- Control de ingreso: el nombre del socio se insertaba como HTML (riesgo de XSS) y "Registrar con cargo extra" reenviaba
  el campo ya vaciado, por lo que la confirmación nunca registraba el ingreso. Ambos corregidos.
- `jquery.validate` no estaba en `wwwroot/lib` (solo su licencia): la validación en el navegador estaba rota en todos los
  formularios. Ahora se carga desde CDN con integridad verificada y acepta coma o punto decimal (como el servidor).
- La barra superior desbordaba la página horizontalmente (buscador + alertas + perfil); las tarjetas de indicadores
  ensanchaban la grilla en celulares. Corregido y verificado en 1366×768, 1920×1080, tablet y celular.

## 8. Casos de prueba (30 nuevos, 118 en total)

`BrandingNavegacionTests`: normalización de colores e intento de inyección CSS; contraste WCAG; colores claros se
oscurecen y quedan legibles; colores con buen contraste no cambian; la paleta por defecto conserva el aspecto original;
datos inválidos guardados no rompen la identidad; nombre por defecto desde appsettings y cache que se invalida al
guardar; imágenes reales sí, SVG/contenido falso/demasiado grande no; menú por rol (Administrador, Recepcionista,
Entrenador), grupos nunca vacíos y detección del ítem activo; reglas de ingreso (registrado, duplicado, sin
membresía, inexistente, vacío) y respuestas históricas de la API.

`KioscoBrandingIntegracionTests` (aplicación real): el kiosco autoriza, no duplica, restringe y deniega sin menú ni
barra; exige sesión y permiso; la API de recepción responde igual que antes; el branding se aplica en login, menú y
credencial y queda auditado (color inválido rechazado); Configuración solo para quien la administra; avisos vigentes
rotan y los vencidos no; el menú se adapta a cada rol; Evolución física muestra al entrenador solo sus socios.

Verificación visual con Chrome real (capturas en 1366×768, 1920×1080, tablet vertical y celular): login con marca,
panel, menú colapsado y recordado, menú deslizable en tablet, credencial, ticket, recibo, configuración, y el kiosco en
sus cuatro estados + teclado táctil + pantalla de espera rotando. Sin errores de JavaScript.

## 9. Checklist de validación manual

1. Backup de `data/gym.db` y reiniciar la aplicación.
2. Administración › Configuración › Branding: cargar nombre, eslogan, logo, logo reducido, favicon, imagen de bienvenida y colores. Guardar.
3. Cerrar sesión: el login muestra logo, colores e imagen. Verificar pestaña del navegador (favicon y título).
4. Revisar menú lateral (grupos, scroll, usuario abajo) en 1366×768 y en una tablet.
5. Ficha de un socio › Credencial: logo, colores y texto propios; el código de barras sigue leyéndose.
6. Configuración › Recepción y kiosco: activar pantalla de espera, cargar una promoción con imagen y un evento.
7. Abrir el kiosco en la tablet/monitor de la entrada, pantalla completa. Probar: credencial válida (verde), socio vencido (naranja), código inexistente (rojo), DNI con teclado táctil, y esperar la rotación de avisos.
8. Entrar con un usuario Entrenador y uno Recepcionista y confirmar que el menú muestra solo lo suyo.
9. Imprimir un reporte, un recibo y un ticket: aparecen nombre/logo del gimnasio.

## Observación (no modificada)

En las reglas de ingreso existentes, la detección de "ingreso duplicado" y el tope semanal comparan la hora local
(`DateTime.Now`) con fechas guardadas en UTC. En Argentina (UTC−3) la ventana de duplicado efectiva es ~3 h 5 min en
lugar de 5 min y la semana se corre 3 horas. Se dejó igual porque el sprint pide no modificar reglas; corregirlo es
un cambio de una línea en `ServicioIngreso` cuando se decida.
