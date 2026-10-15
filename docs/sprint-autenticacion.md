# Sprint: autenticación, usuarios, roles y permisos

El sistema deja de depender de un operador configurado: toda acción la realiza un **usuario autenticado** y la autorización se decide **por permisos**, nunca por el nombre del rol. Se eliminó `Operador:Nombre` de la configuración.

## 1. Diseño funcional

| Historia | Resultado |
|---|---|
| H1 Autenticación | Login con usuario y contraseña, opción "Recordar sesión", mostrar/ocultar contraseña y cierre de sesión. En el primer arranque, una **configuración inicial** crea el administrador: no hay credenciales por defecto. |
| H2 Usuarios | Administración › Usuarios: listado (nombre, usuario, email, rol, estado, último acceso) con buscador y filtros por estado y rol. KPIs: activos, inactivos, accesos en 24 h y bloqueados. Acciones: Ver, Editar, Resetear contraseña, Desbloquear, Activar/Desactivar (con confirmación). Ficha con permisos efectivos, accesos, actividad e historial de la cuenta. |
| H3 Roles | Administrador, Recepcionista, Supervisor y Entrenador se crean al arrancar si faltan. Después se editan, y se pueden crear roles nuevos. |
| H4 Matriz de permisos | Catálogo de 20 permisos (§4) en un solo archivo. Cada rol es un conjunto de códigos. Administración › Roles muestra la matriz roles × permisos y un editor por módulo. |
| H5 UI adaptativa | Menú lateral, botones, acciones de fila, pestañas de fichas, KPIs y bloques del Panel, tarjeta de cobranza, campana de alertas y módulos se **ocultan** si falta el permiso. No hay botones deshabilitados innecesarios. |
| H6 Auditoría | `IUsuarioActual` sale de la sesión: cada evento guarda el nombre y el **`UsuarioId`** del usuario real. Se registran inicio y cierre de sesión, intentos fallidos, bloqueos, accesos denegados, altas y ediciones de usuario, cambio y reseteo de contraseña, cambio de rol, activación/desactivación y cambios de permisos de rol. |
| H7 Perfil | Menú de la cabecera con nombre, usuario, rol y último acceso, más Mi perfil, Cambiar contraseña y Cerrar sesión. Mi perfil muestra los datos editables (nombre, email), lo que puedo hacer, mis accesos y mi actividad. |
| H8 Seguridad | Hash PBKDF2, política de contraseñas, bloqueo temporal, contraseña temporal con cambio obligatorio, sesión revalidada en cada request, expiración controlada y eventos de seguridad (§6). |

## 2. Wireframes

### Login
```
                    (⚡) GymCore
                         Club Management
          ┌──────────────────────────────────────┐
          │ Bienvenido de nuevo                  │
          │ Ingresá con tu usuario…              │
          │ ⚠ Usuario o contraseña incorrectos   │
          │ Usuario     [👤 recepcion1         ] │
          │ Contraseña  [🔑 ••••••••        👁 ] │
          │ ☐ Recordar sesión  ¿Olvidaste tu…?   │
          │ [      Iniciar sesión  →          ]  │
          │ 🔒 Se bloquea tras varios intentos   │
          └──────────────────────────────────────┘
           © 2026 GymCore · Acceso para personal autorizado
```

### Cabecera y menú de perfil
```
[🔍 Buscar…]  🔔②  [(GE) Germán Espíndola ▾]
                    ┌──────────────────────────────┐
                    │ (GE) Germán Espíndola        │
                    │      @german · Administrador │
                    │      🕑 Último acceso 14/10  │
                    ├──────────────────────────────┤
                    │ 👤 Mi perfil                 │
                    │ 🔑 Cambiar contraseña        │
                    │ ↪ Cerrar sesión              │
                    └──────────────────────────────┘
```

### Administración › Usuarios
```
[✓ Activos 5] [– Inactivos 1] [↪ Accesos 24 h 3 · Último: María] [🔒 Bloqueados 0]
┌───────────────────────────────────────────────────────────────────────┐
│ [🔍 Nombre, usuario…]   [Todos|Activos|Inactivos]  [Todos los roles ▾]│
│ USUARIO ↕             EMAIL ↕        ROL ↕          ESTADO     ÚLTIMO │
│ (MR) María Recepción  maria@…        [Recepcionista] ● Activo  Hoy 9:12│ ✎ ⋮
│      @maria                                                           │
│ (CU) Coach Uno        —              [Entrenador]   ● Temporal Nunca  │ ✎ ⋮
└───────────────────────────────────────────────────────────────────────┘
  ⋮ = Ver · Editar · Resetear contraseña · (Desbloquear) · Activar/Desactivar
  Al resetear:  [🔑 Contraseña temporal · se muestra solo esta vez  Repdh-7466  (Copiar)]
```

### Roles y matriz de permisos
```
[★ Administrador · 1 usuario · Acceso total] [🛡 Recepcionista · 9 de 20] [🛡 Supervisor] [🛡 Entrenador]
┌ Matriz de permisos ──────────────────────────────────────────────────┐
│ PERMISO                        Admin  Recepción  Supervisor  Entren. │
│ SOCIOS                                                               │
│ Ver socios        SOCIOS_VER     ✓        ✓          ✓          –    │
│ Ver asignados     SOCIOS_VER_…   ✓        –          –          ✓    │
│ PAGOS                                                                │
│ Anular pagos ◆    PAGOS_ANULAR   ✓        –          ✓          –    │
└──────────────────────────────────────────────────────────────────────┘
```

## 3. Modelo de dominio

```
Rol ─────────────< RolPermiso >            Usuario
├ Id, Nombre (único)    ├ RolId             ├ Id, NombreCompleto, NombreUsuario (único, minúsculas), Email
├ Descripcion           └ Permiso (código)  ├ PasswordHash (PBKDF2)          ├ RolId → Rol
├ EsSistema (no se borra/renombra)          ├ EntrenadorId? → Entrenador (alcance "mis socios")
├ AccesoTotal (todos los permisos,          ├ Activo, FechaAlta, UltimoAcceso, AccesoAnterior
│   incluso futuros)                        ├ IntentosFallidos, BloqueadoHasta
├ Tiene(permiso)                            ├ DebeCambiarPassword, SelloSeguridad
└ AsignarPermisos(códigos)                  └ RegistrarIntentoFallido · RegistrarAccesoExitoso · CambiarPassword
                                              ResetearPassword · CambiarRol · Activar · Desactivar · Desbloquear
EventoSistema + UsuarioId
```

Las reglas viven en la entidad `Usuario` (setters privados): no se puede cambiar la contraseña, el rol o el estado sin renovar el **sello de seguridad**, que es lo que invalida las sesiones abiertas.

## 4. Matriz de permisos

| Permiso | Admin | Recepcionista | Supervisor | Entrenador |
|---|:-:|:-:|:-:|:-:|
| SOCIOS_VER | ✓ | ✓ | ✓ | |
| SOCIOS_VER_ASIGNADOS | ✓ | | | ✓ |
| SOCIOS_CREAR / SOCIOS_EDITAR | ✓ | ✓ | ✓ | |
| SOCIOS_ELIMINAR (baja) ◆ | ✓ | | ✓ | |
| CREDENCIALES_EMITIR | ✓ | ✓ | ✓ | |
| EVOLUCION_VER | ✓ | | ✓ | ✓ |
| EVOLUCION_REGISTRAR | ✓ | | | ✓ |
| ASISTENCIAS_REGISTRAR | ✓ | ✓ | ✓ | |
| ENTRENADORES_VER | ✓ | ✓ | ✓ | |
| ENTRENADORES_EDITAR | ✓ | | | |
| PLANES_VER | ✓ | ✓ | ✓ | |
| PLANES_EDITAR | ✓ | | | |
| PAGOS_VER / PAGOS_CREAR | ✓ | ✓ | ✓ | |
| PAGOS_ANULAR ◆ | ✓ | | ✓ | |
| AUDITORIA_VER ◆ | ✓ | | ✓ | |
| REPORTES_VER | ✓ | | ✓ | |
| USUARIOS_ADMINISTRAR ◆ | ✓ | | | |
| CONFIGURACION_ADMINISTRAR ◆ | ✓ | | | |

◆ = sensible. Es la configuración inicial y se ajusta desde la UI.

Sumé al pedido original cinco permisos para cubrir las reglas de rol sin decidir por nombres: `SOCIOS_VER_ASIGNADOS`, `CREDENCIALES_EMITIR`, `EVOLUCION_VER`, `EVOLUCION_REGISTRAR` y `ASISTENCIAS_REGISTRAR`, más `ENTRENADORES_*`.

**Cómo se aplica:**
- Cada PageModel lleva `[RequierePermiso(...)]` y cada handler POST sensible, el suyo. Un filtro global los evalúa y registra los accesos denegados.
- Una política global (`FallbackPolicy`) exige sesión en todo, salvo login, configuración inicial y error.
- El check-in (`/api/asistencias/checkin`) usa la política `ASISTENCIAS_REGISTRAR`: sin sesión responde 401.
- **Alcance del entrenador:** con `SOCIOS_VER_ASIGNADOS` y sin `SOCIOS_VER` solo ve y opera sobre los socios con asignación activa a su ficha (`Usuario.EntrenadorId`), en el listado, la ficha y las mediciones. Se valida en el servidor.
- **Regla anti-bloqueo:** siempre queda al menos un usuario activo con `USUARIOS_ADMINISTRAR`. Nadie puede desactivarse a sí mismo y el rol Administrador (`AccesoTotal`) no se edita.

## 5. Cambios de base de datos (SQLite, aditivos)

| Cambio | Detalle |
|---|---|
| Tabla `Usuarios` | Índice único por `NombreUsuario`. FK a `Roles` (Restrict) y a `Entrenadores` (SetNull). |
| Tabla `Roles` | Índice único por `Nombre`. |
| Tabla `RolPermisos` | PK `(RolId, Permiso)`, cascade desde el rol. |
| `EventosSistema.UsuarioId` | Nullable, con índice: actividad por usuario. |

Las crea `EsquemaSqlite` al arrancar. Los roles iniciales se siembran si faltan, sin pisar ediciones. Verificado sobre una copia de la base actual: datos intactos.

## 6. Políticas de seguridad

| Aspecto | Política |
|---|---|
| Contraseñas | PBKDF2-HMAC-SHA512 con sal por usuario (`PasswordHasher` de ASP.NET Core), con rehash transparente si el algoritmo se fortalece. Nunca se guardan ni se registran en texto plano. |
| Política mínima | 8 a 128 caracteres, al menos una letra y un número, no puede contener el usuario, lista de contraseñas comunes bloqueada. Checklist en vivo en pantalla; la validación real es del servidor. |
| Bloqueo | 5 intentos fallidos → 15 minutos (configurable en `Seguridad`). Los intentos durante el bloqueo no lo extienden. Un administrador puede desbloquear o resetear. |
| Enumeración de usuarios | Mismo mensaje y mismo costo de cálculo exista o no el usuario. "Usuario inactivo" solo se informa con la contraseña correcta. |
| Contraseñas temporales | Al crear o resetear se genera una contraseña aleatoria, que se muestra **una sola vez**. El usuario debe cambiarla antes de usar cualquier pantalla. |
| Sesión | Cookie `HttpOnly`, `SameSite=Lax` y cifrada (Data Protection). Normal: 8 h de inactividad (deslizante), cookie de sesión del navegador. "Recordar sesión": 14 días fijos. |
| Validación de sesión | En cada request se verifica que el usuario exista, esté activo y su sello coincida. Los permisos se recargan del rol: un cambio de permisos o rol aplica de inmediato. Cambiar o resetear la contraseña y desactivar cierran las demás sesiones. |
| Cierre de sesión | Solo por POST con antiforgery (un enlace externo no puede desloguear). |
| Eventos de seguridad | Inicio y cierre de sesión, intento fallido (con IP), bloqueo, acceso denegado (página, handler y permiso requerido). Filtrables en Auditoría › "Sesiones y seguridad". |

## 7. Flujo de autenticación

```
Request ──► ¿cookie válida? ──no──► /Cuenta/Login?ReturnUrl=…  (API: 401)
              │sí                         │ ¿hay usuarios? no ─► /Cuenta/Configurar (crea admin)
              ▼                           ▼
   ValidarSesionAsync (cada request)   POST credenciales
   · usuario existe y activo            · bloqueado → mensaje + evento
   · sello coincide                     · inválidas → +1 intento (5 → bloqueo) + evento
   · recarga permisos del rol           · inactivo → mensaje + evento
     (si falla: cierra sesión)          · ok → acceso, evento SesionIniciada, cookie
              ▼                                   │
   FiltroPermisos                                 ▼
   · ¿contraseña temporal? ─► /Cuenta/CambiarPassword (obligatorio)
   · [RequierePermiso] de página y handler ─no─► evento AccesoDenegado ─► /Cuenta/AccesoDenegado
              ▼ sí
            Página
```

## 8. Integración con auditoría

- `IUsuarioActual` implementa `IOperadorAuditoria { UsuarioId, Nombre }`. El `DbContext` completa ambos en cada evento nuevo, y también el responsable del historial de entrenadores y `Pago.UsuarioCajero` al cobrar.
- Se lee del `HttpContext` en cada acceso, no en el constructor: el `DbContext` se crea durante la validación de la cookie y, si se fijara al construirse, la auditoría registraría "Sistema".
- Antes de autenticarse (login), el evento lleva como responsable a quien intenta ingresar. Fuera de una request (arranque, procesos) el responsable es "Sistema".
- La ficha del usuario y Mi perfil muestran su actividad usando `UsuarioId`, sin depender del nombre.

## 9. Código (archivos principales)

| Capa | Archivos |
|---|---|
| Dominio | `Entidades/Usuario.cs`, `Entidades/Rol.cs` (+`RolPermiso`), `Enums/TipoEventoSistema.cs` (500–608), `EventoSistema.UsuarioId` |
| Aplicación | `IOperadorAuditoria` (+`UsuarioId`) |
| Infraestructura | `Config/SeguridadConfig.cs`, `GymDbContext` (DbSets, responsable con Id) |
| Web: seguridad | `Seguridad/Permisos.cs` (catálogo + roles iniciales), `Seguridad/Autenticacion.cs` (login, cookie, validación), `Seguridad/FiltroPermisos.cs` (`[RequierePermiso]`), `Seguridad/PoliticaSeguridad.cs`, `Componentes/UsuarioActual.cs`, `Program.cs` |
| Web: pantallas | `Cuenta/Login`, `Configurar`, `CambiarPassword`, `Perfil`, `Logout`, `AccesoDenegado` · `Administracion/Usuarios` (Index, Editar, Detalle) · `Administracion/Roles` (Index, Editar) · `_LayoutCuenta` · layout y vistas adaptadas por permiso |
| Tests | `tests/GymCore.Tests`: `SeguridadTests.cs`, `AutenticacionIntegracionTests.cs` |

### Preparado para multi-sucursal y SaaS
- **Multi-gimnasio:** agregar `GimnasioId` a `Usuario`, `Rol` y entidades, más un claim `gym:gimnasio` y un filtro global por tenant, igual al de pagos anulados. El catálogo de permisos es común; los roles quedan por gimnasio.
- **Permisos por sucursal:** pasar de `Usuario.RolId` a una tabla `UsuarioRol(UsuarioId, RolId, SucursalId?)` y emitir claims del tipo `permiso@sucursal`. `IUsuarioActual.Puede(permiso)` puede sumar una sobrecarga con sucursal sin tocar las pantallas actuales.
- **Equipos de entrenadores:** el alcance "asignados" ya está aislado en `SociosConsultas.AsignadosAsync`, que puede extenderse a equipos.
- **Login externo (Google, Microsoft, SSO):** se suma como otro esquema de autenticación. Mientras emita los mismos claims, el resto no cambia.

## 10. Casos de prueba automatizados

`dotnet test tests/GymCore.Tests`: **44 casos en verde** (30 nuevos).

| Tipo | Casos |
|---|---|
| Unitarios (`SeguridadTests`) | Política: rechaza cortas, sin número, sin letra, comunes y con el usuario; acepta válidas; las temporales cumplen la política y no se repiten. Hash con sal, verificable y que nunca contiene la clave. Normalización del usuario. Bloqueo a los 5 intentos y expiración; los intentos durante el bloqueo no lo extienden. El acceso exitoso reinicia intentos y guarda el acceso anterior. Contraseña, rol y estado cambian el sello; el rehash no. El reseteo obliga a cambiar y desbloquea. Roles iniciales solo con permisos del catálogo; matriz inicial (recepción sin auditoría ni usuarios, supervisor ⊇ recepción, entrenador sin pagos); asignación de permisos. Permisos por claims, nunca por nombre de rol; acceso total; sin sesión = "Sistema" sin permisos. |
| Integración (`AutenticacionIntegracionTests`, app real en memoria) | Sin sesión, todo redirige al login. La API responde 401. Login correcto auditado con `UsuarioId`. Mismo mensaje exista o no el usuario. Bloqueo tras 5 intentos aun con la clave correcta. Recepción cobra, pero auditoría, usuarios y roles le dan acceso denegado (y queda auditado); no ve el menú de roles y no puede anular. El entrenador ve su socio asignado, no el ajeno ni pagos, y su ficha no muestra la pestaña Pagos. La contraseña temporal obliga a cambiarla y después la sesión sigue. Desactivar a un usuario corta su sesión en la siguiente request. El logout queda auditado y corta el acceso. |

Además, recorrí la app completa sobre una copia de la base real con los tres perfiles, sin errores en el log: primer arranque, altas por la UI, contraseña temporal mostrada una sola vez, bloqueo y desbloqueo, prevención de la auto-desactivación del admin y cobro registrado a nombre de la recepcionista.

## 11. Checklist de validación manual

**Antes:** backup de `data/gym.db` y reiniciar la app.

**Primer arranque**
- [ ] Abrir la app: lleva a "Creá el administrador". Una contraseña débil muestra los errores y el checklist marca lo que falta.
- [ ] Crear el administrador: entra directo al Panel con el mensaje de bienvenida. `/Cuenta/Configurar` ya no está disponible.

**Usuarios y roles**
- [ ] Crear una recepcionista y un entrenador (vinculado a su ficha de entrenador). Se muestra la contraseña temporal una sola vez, con botón Copiar.
- [ ] Usuario duplicado (aunque cambien mayúsculas): error claro.
- [ ] Roles y permisos: la matriz refleja §4. Quitarle "Registrar pagos" a Recepcionista: en la siguiente navegación de la recepcionista desaparece el botón "Registrar pago".
- [ ] No se puede desactivar el propio usuario. Quitarle el rol administrador al único admin muestra el error anti-bloqueo.

**Login y sesión**
- [ ] Ingreso con la contraseña temporal: obliga a cambiarla. El checklist rechaza una clave que contenga el usuario.
- [ ] 5 claves incorrectas: mensaje de bloqueo con los minutos. El admin desbloquea desde el listado.
- [ ] "Recordar sesión": cerrar y reabrir el navegador mantiene la sesión. Sin tildarlo, se pierde.
- [ ] Cambiar la contraseña en un navegador cierra la sesión del mismo usuario en otro.
- [ ] Desactivar a un usuario con sesión abierta: en su próximo clic vuelve al login.
- [ ] Cerrar sesión desde el menú de perfil.

**UI por permisos**
- [ ] Recepcionista: sin sección Administración, sin Anular en el detalle de pago, sin "Dar de baja" en socios ni edición de planes y entrenadores. `/Administracion/Auditoria` por URL muestra "No tenés acceso".
- [ ] Entrenador: menú "Mis socios" solo con sus asignados. Panel con "Mis socios asignados", sin cobranza, sin tarjeta del sidebar y sin campana. La ficha no muestra Membresía, Pagos, Credencial ni Historial, pero puede cargar mediciones. Un socio ajeno por URL muestra acceso denegado.
- [ ] Supervisor: ve Auditoría y puede anular pagos, pero no ve Usuarios ni Roles.

**Auditoría**
- [ ] Auditoría › "Sesiones y seguridad": inicios, cierres, fallidos, bloqueos y accesos denegados con usuario e IP.
- [ ] Un cobro de la recepcionista queda a su nombre en el listado de pagos, el comprobante y la auditoría.
- [ ] Ficha de un usuario: accesos, historial de la cuenta (alta, rol, reseteos) y su actividad.
