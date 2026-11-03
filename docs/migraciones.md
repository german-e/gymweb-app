# Migraciones de base de datos (EF Core + PostgreSQL)

El esquema de la base se maneja con **migraciones de EF Core**. No se usa `EnsureCreated` ni
`ALTER TABLE` escritos a mano. Para levantar PostgreSQL en desarrollo, ver `docs/postgresql.md`.

## Preparación (una sola vez por equipo)

```bash
dotnet tool restore          # instala dotnet-ef en la versión fijada en .config/dotnet-tools.json
```

## Generar la migración inicial (una sola vez)

Desde la épica 2 la base es PostgreSQL: las migraciones de SQLite de la épica 1 se borran y se genera
una nueva migración inicial. Necesitás PostgreSQL corriendo solo para aplicarlas, no para generarlas.

```bash
dotnet ef migrations add Inicial \
  --project src/GymCore.Infraestructura \
  --startup-project src/GymCore.Web \
  --output-dir Persistencia/Migraciones
```

Se crean tres archivos en `src/GymCore.Infraestructura/Persistencia/Migraciones/`. **Se versionan en Git.**

## Flujo de trabajo cada vez que cambia el modelo

1. Modificás una entidad (`GymCore.Dominio/Entidades`) o su configuración (`Persistencia/Config`).
2. Generás la migración con un nombre que describa el cambio:
   ```bash
   dotnet ef migrations add AgregarAptoMedicoASocio \
     --project src/GymCore.Infraestructura --startup-project src/GymCore.Web
   ```
3. **Revisás el archivo generado** (`Up` y `Down`). Prestá atención a todo lo que borre columnas o tablas,
   y a columnas nuevas obligatorias en tablas con datos (necesitan un valor por defecto).
4. Corrés la app: la migración se aplica sola al arrancar.
5. Commit del cambio de la entidad **junto con** la migración.

Si te olvidás el paso 2, la prueba `El_proyecto_tiene_migraciones_y_el_modelo_no_tiene_cambios_sin_migrar`
y el paso "Migraciones al día con el modelo" del CI fallan.

## Comandos útiles

| Para qué | Comando (agregar `--project` y `--startup-project` como arriba) |
| --- | --- |
| Ver las migraciones y cuáles están aplicadas | `dotnet ef migrations list` |
| ¿Hay cambios del modelo sin migrar? | `dotnet ef migrations has-pending-model-changes` |
| Deshacer la última migración (si **no** se aplicó ni se subió) | `dotnet ef migrations remove` |
| Ver el SQL que se ejecutaría | `dotnet ef migrations script --idempotent` |
| Aplicar a mano sobre la base de desarrollo | `dotnet ef database update` |

## Si una migración falla al arrancar

La aplicación **no arranca**, a propósito: es mejor que operar con un esquema a medio actualizar.
El motivo aparece en el log (`docker logs gymcore` o la consola de `dotnet run`).
