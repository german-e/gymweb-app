# PostgreSQL en GymCore

GymCore usa **PostgreSQL 17** (proveedor Npgsql para EF Core). Hay tres formas de levantarlo, según
para qué:

| Para qué | Archivo | Comando |
| --- | --- | --- |
| Desarrollar (`dotnet watch`) y correr las pruebas | `docker-compose.dev.yml` | `docker compose -f docker-compose.dev.yml up -d` |
| Probar la imagen completa en tu PC (http://localhost:8080) | `docker-compose.yml` + `docker-compose.local.yml` | `docker compose -f docker-compose.yml -f docker-compose.local.yml up -d --build db gymcore` |
| Producción (con Caddy y HTTPS) | `docker-compose.yml` | `docker compose up -d --build` |

## Desarrollo

```bash
docker compose -f docker-compose.dev.yml up -d   # PostgreSQL en localhost:5432 (gymcore / gymcore_dev)
dotnet watch --project src/GymCore.Web           # aplica las migraciones al arrancar
dotnet test                                      # crea y borra sus propias bases (gymcore_test_*)
```

Conexión desde la extensión PostgreSQL de VS Code: host `localhost`, puerto `5432`, base `gymcore`,
usuario `gymcore`, contraseña `gymcore_dev`.

Las pruebas usan el mismo servidor, en bases separadas que empiezan con `gymcore_test_`: nunca tocan
la base `gymcore`. Para usar otro servidor, definí la variable `GYMCORE_PRUEBAS_POSTGRES` con una cadena
de conexión de un usuario que pueda crear bases.

## Fechas y zona horaria

- Las columnas de fecha son `timestamp without time zone`: se guarda la hora tal como la maneja la
  aplicación, igual que con SQLite (conversor en `GymDbContext`).
- La sesión de PostgreSQL usa `America/Argentina/Buenos_Aires` (lo fija `Program.cs`), para que las
  consultas con `DateTime.Now` o `DateTime.Today` den el "hoy" de Argentina.
- Deuda técnica registrada: guardar los instantes en UTC (`timestamptz`) y las vigencias como `DateOnly`.

## Backups (producción)

Diario, con `pg_dump` desde el contenedor (formato comprimido de PostgreSQL):

```bash
docker compose exec -T db pg_dump -U gymcore -d gymcore -Fc > gymcore-$(date +%F).dump
```

Restaurar sobre una base vacía:

```bash
docker compose exec -T db pg_restore -U gymcore -d gymcore --clean --if-exists < gymcore-2026-11-03.dump
```

Copiá los backups **fuera del servidor**, y probá una restauración de vez en cuando: un backup que
nunca se restauró no está probado.

## Datos de la etapa SQLite

Los volúmenes y archivos de SQLite (`gymcore_data`, `gym.db`) no se borran ni se migran
automáticamente. Si necesitás pasar datos de una base SQLite a PostgreSQL, se puede hacer con un
importador; mientras tanto, guardá el archivo `gym.db` como respaldo.
