# Contrato: runner de database/ y orden en Docker Compose

Contrato operativo (no hay endpoints nuevos). Procedencia: `Dec`, `Obs`, `Plan` como en `research.md`. Estado: **diseño**; no se ha ejecutado nada en esta etapa.

## Runner (`database/run-scripts.sh`) (Dec, Obs, Plan)

**Entradas** (variables de entorno; sin secretos en archivos ni argumentos):

| Variable | Obligatoria | Valor | Notas |
|---|---|---|---|
| `MSSQL_SA_PASSWORD` | Sí | contraseña de `sa` | Se usa vía `SQLCMDPASSWORD`; nunca se imprime |
| `DB_NAME` | Sí (nuevo: sin valor por defecto) | `CargaMasivaApp` en Compose | Solo letras, dígitos y guion bajo; el valor `CargaMasiva` anterior ya no es el predeterminado |
| `DB_HOST`, `DB_PORT`, `DB_USER` | No | `sqlserver`, `1433`, `sa` | Valores de Compose |
| `SCRIPTS_DIR`, `BOOTSTRAP_SQL` | No | `/database/scripts`, `/database/bootstrap.sql` | Rutas dentro de la imagen |

**Secuencia**:

1. Espera acotada a `master` (60 intentos × 2 s); si no responde: error, código ≠ 0, nada creado.
2. **Comprobación previa de la base** (nuevo, D-03): base inexistente o vacía → continúa; con `__EFMigrationsHistory` → error claro; con tablas pero sin `SchemaMigraciones` → error claro; con `SchemaMigraciones` → continúa. En los errores no se ejecuta bootstrap ni scripts.
3. Bootstrap idempotente (crea la base y `SchemaMigraciones` si faltan).
4. Lee los scripts aplicados y calcula el checksum de los de `database/scripts/`.
5. Falla (sin aplicar nada) si un script aplicado cambió o si uno nuevo es de número menor que el último aplicado.
6. Sin pendientes: informa "Sin scripts pendientes" y termina con éxito sin modificar nada.
7. Con pendientes: una sola sesión con cerrojo exclusivo; cada script en su transacción con `XACT_ABORT`; registra nombre y checksum dentro de la transacción; si falla, se revierte, no se registra y el runner termina con error.

**Salidas**: código 0 solo si no hay pendientes o todos se aplicaron; código ≠ 0 en cualquier otro caso. Mensajes en español, sin contraseñas ni cadenas de conexión.

## Servicio en Compose (Plan)

| Elemento | Contrato |
|---|---|
| Servicio | `database-runner`, build `./database`, `platform: linux/amd64` (misma imagen del motor) |
| Ejecución | única por `up`; `restart: "no"`; sin puertos ni volúmenes |
| Entorno | `DB_NAME`, `MSSQL_SA_PASSWORD`, `DB_HOST=sqlserver`, `DB_PORT=1433` |
| Dependencia | `sqlserver: condition: service_healthy` |
| Imagen | contiene los scripts al construirse: reconstruir tras editar scripts (`docker compose up -d --build`) |

## Orden de arranque (Dec, Constitución §11)

```text
sqlserver (healthy) → database-runner (exit 0) → ms-control, ms-autenticacion, ms-carga-masiva, ms-notificaciones
```

- Los cuatro servicios: `depends_on: database-runner: condition: service_completed_successfully`.
- Runner con error: ningún microservicio arranca; el fallo queda en el estado y los logs del runner.
- Los servicios mantienen healthcheck, puertos (8081–8084), volúmenes de logs, `restart: "no"` y cadena de conexión por entorno con `Database=${DB_NAME:-CargaMasivaApp}`.
- Ningún servicio crea ni modifica el esquema al arrancar.

## Bases y volúmenes (Dec)

`CargaMasivaApp` es la base nueva y vacía de runner y servicios. `CargaMasiva` y `CargaMasivaDb`, sus datos y los volúmenes (`sqlserver-data`, logs) no se modifican ni eliminan. Las bases temporales `CargaMasivaApp_*` de validación se crean y eliminan al terminar.

## Límite de validación (Dec)

Motor y runner amd64 emulados en Apple silicon, sin soporte oficial de Microsoft; la validación vale solo para este equipo. **No se declara Docker validado** hasta ejecutar el quickstart; el equipo de evaluación queda sin validar.
