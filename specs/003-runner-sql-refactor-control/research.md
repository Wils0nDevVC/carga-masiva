# Research: runner de database/, Compose y limpieza de ms-control

Procedencia: `Dec` = decisión del usuario o de la constitución v3.0.0 (incluye el prompt del plan); `Spec` = spec 003; `Obs` = comprobado leyendo el repositorio en esta etapa (solo lecturas; no se ejecutó Docker, SQL ni migraciones); `Plan` = decisión técnica de este plan.

## Estado observado (Obs)

- `database/run-scripts.sh`: espera hasta 60 intentos × 2 s a `master`; crea la base y `SchemaMigraciones` con `bootstrap.sql`; compara scripts `V[0-9][0-9][0-9]__*.sql` con el registro; falla si un aplicado cambió (checksum) o si uno nuevo tiene número menor que el último aplicado; encadena los pendientes en un archivo maestro con `:on error exit`, cerrojo `sp_getapplock` de sesión, y cada script en su transacción con `XACT_ABORT`, registrando nombre y checksum dentro de la transacción. Valor por defecto de `DB_NAME`: `CargaMasiva`. Secretos solo por `MSSQL_SA_PASSWORD` (se exporta como `SQLCMDPASSWORD`, no va en argumentos).
- `database/Dockerfile`: base `mssql/server:2022-CU27-ubuntu-22.04` (incluye `sqlcmd`), copia script, bootstrap y `scripts/` al construir; `ENTRYPOINT bash /database/run-scripts.sh`. Los scripts quedan embebidos en la imagen: tras editarlos hay que reconstruir (hallazgo de la Feature 001 R-11).
- `database/scripts/V001`, `V002` son idénticas a `referencias/` (la tabla `SchemaMigraciones` la crea el runner, no V001).
- El runner no figura en `compose.yaml`; nunca se versionó un servicio del runner en Compose.
- `compose.yaml` actual: `sqlserver` (healthcheck con `sqlcmd`), `ms-control` (`depends_on sqlserver healthy`) y los otros tres (`depends_on ms-control healthy`); `DB_NAME` por defecto `CargaMasivaDb`; `.env` local y `.env.example` con `DB_NAME=CargaMasivaDb`.
- `ms-control`: ninguna referencia a las siete entidades fuera de `Acity.Domain/Entities`, `Persistence/Configurations` y `Persistence/Migrations` (búsqueda por nombre en `*.cs`). `GenericRepository<T>` usa `context.Set<TEntity>()` de forma genérica, sin una entidad concreta. No hay `DbSet`, handlers ni controladores de negocio; solo `HealthController`.
- `ms-autenticacion`, `ms-carga-masiva` y `ms-notificaciones`: `Acity.Domain` sin contenido y `AppDbContext` sin entidades.

## D-01 Reutilizar el runner existente (Dec, Plan)

**Decisión**: conservar el runner de `database/` (script Bash + `sqlcmd` sobre la imagen de SQL Server) con cambios mínimos (D-03, D-02). **Por qué**: ya implementa lo que pide la spec (solo pendientes, registro con checksum, orden estricto, atomicidad por script, cerrojo, espera acotada) y la constitución v3.0.0 lo reactiva; reescribirlo sería sobreingeniería. **Descartadas**: ejecutable .NET o microservicio de migraciones (prohibido por §11); una imagen de herramientas aparte (tecnología nueva); Flyway/DbUp (paquetes no autorizados).

## D-02 Base nueva y vacía `CargaMasivaApp` y `DB_NAME` obligatorio en el runner (Dec, Plan)

**Decisión**: el runner y los cuatro servicios usan la base nueva y vacía `CargaMasivaApp` (valor por defecto de `DB_NAME` en `compose.yaml` y `.env.example`; el `.env` local, no versionado, se actualiza en la implementación). `CargaMasiva` y `CargaMasivaDb` y sus volúmenes no se tocan. En `run-scripts.sh` se elimina el valor por defecto `CargaMasiva` y `DB_NAME` pasa a ser obligatorio. **Por qué**: el usuario fija una base nueva y vacía; el valor por defecto actual apunta a la base heredada que se conserva. **Alternativas**: reutilizar `CargaMasiva` (compatible con el runner, pero el usuario pidió base nueva); reutilizar `CargaMasivaDb` (historial EF, incompatible: mezclaría historiales). El nombre `CargaMasivaApp` es una elección de este plan (detalle menor); cambiarlo solo exige editar `DB_NAME`.

**Bases temporales de prueba**: los escenarios de fallo y de concurrencia usan bases con prefijo `CargaMasivaApp_` creadas por el runner y eliminadas al terminar la validación, como se hizo en la Feature 002. Nunca se eliminan `CargaMasiva`, `CargaMasivaDb` ni volúmenes.

## D-03 Comprobación previa para no mezclar historiales (Dec, Spec FR-013, Plan)

**Decisión**: antes del bootstrap (que crea `SchemaMigraciones`), el runner consulta desde `master`:
1. si la base no existe: es nueva, continúa (el bootstrap la crea);
2. si existe y contiene `dbo.__EFMigrationsHistory`: termina con error y mensaje claro (historial de EF Core; no se mezcla);
3. si existe, no tiene `dbo.SchemaMigraciones` y sí tablas de usuario: termina con error y mensaje claro (objetos sin registro del runner);
4. en otro caso (vacía, o con `SchemaMigraciones`): continúa.

En los casos de error no se ejecuta el bootstrap ni ningún script, de modo que la base queda intacta. **Por qué**: hoy el bootstrap crearía `SchemaMigraciones` dentro de una base ajena antes de fallar en V001, lo que viola FR-013; la comprobación cuesta una consulta de catálogo. **Descartadas**: confiar solo en que V001 falle por objetos existentes (modifica la base y el mensaje no es claro); guardar un indicador adicional (más estado).

## D-04 Retiro del modelo de Control tras revisar referencias (Dec)

> **Nota (Feature 004, 2026-11-02)**: tras este retiro, la Feature 004 incorpora a Control solo `CargaArchivo`, `CargaEstadoHistorial` e `Incidencia` (por su función de recepción y seguimiento, con `Usuario` y `Producto`/`CargaPeriodo` diferidos), y a los demás servicios sus entidades por ingeniería inversa. La decisión de retirar las siete entidades de esta feature (por no tener entonces referencias ni función aprobada) se conserva como historial.

**Proceso en dos fases (Dec)**:
1. **Revisión (provisional)**: mientras se reúne evidencia, cualquier entidad cuyo uso esté en duda se conserva provisionalmente; no se retira nada por suposición.
2. **Decisión final**: por entidad se documenta su uso o ausencia, y se conserva solo si existe una referencia actual o una función de Control implementada o aprobada. No se conservan ni se agregan entidades por necesidades futuras supuestas.

**Evidencia reunida (Obs, solo lecturas)**:
- Búsqueda por nombre en los `*.cs`, `*.csproj` y `*.json` de los cuatro servicios y del Gateway, excluyendo `bin/` y `obj/`: las siete entidades aparecen únicamente en `ms-control/src/Acity.Domain/Entities`, `Persistence/Configurations` y `Persistence/Migrations` (6 a 10 archivos cada una, todos de ese modelo). 0 referencias fuera de él, ni en Application, Api, el repositorio, el Unit of Work ni los otros servicios.
- Código funcional de Control: solo existen `HealthController` y `GlobalExceptionHandler`; no hay controladores de negocio, comandos, consultas, handlers ni `DbSet`. `GenericRepository<T>` usa `Set<TEntity>()` de forma genérica.
- Funciones de Control **aprobadas**: la constitución (§2) y el README (§3) solo describen el rol de Control (recepción de archivos, seguimiento de cargas, validar el permiso para cargas masivas). No existe spec, plan ni tasks aprobados de una feature de Control; las features 001 (reglas de persistencia para los cuatro servicios), 002 (fundación) y 003 (esta) no definen funciones de negocio de Control. Una descripción de rol no es una función aprobada y la existencia de la tabla en el esquema tampoco justifica la entidad.

**Resultado por entidad**:

| Entidad | Referencias actuales (fuera del modelo/migraciones) | Función de Control implementada o aprobada que la use | Decisión final |
|---|---|---|---|
| Rol | 0 | Ninguna | Retirar |
| Usuario | 0 | Ninguna | Retirar |
| CargaArchivo | 0 | Ninguna implementada ni aprobada (el rol de Control en la constitución no es una función aprobada) | Retirar |
| CargaPeriodo | 0 | Ninguna | Retirar |
| Producto | 0 | Ninguna | Retirar |
| Incidencia | 0 | Ninguna implementada ni aprobada | Retirar |
| CargaEstadoHistorial | 0 | Ninguna | Retirar |

Si en la implementación, antes de retirar, aparece una referencia no detectada (por ejemplo, tras una búsqueda adicional), esa entidad se conserva y se documenta con su referencia; el resultado final por entidad se registra en `quickstart.md`.

**Se retira en Control**: `Acity.Domain/Entities/*` (7), `Persistence/Configurations/*` (7), `Persistence/Migrations/*` (5 archivos), `Persistence/DatabaseInitializer.cs`, su invocación y los `using` en `Program.cs`, y el `ConfigureConventions` de `AppDbContext` (que retiraba `ForeignKeyIndexConvention` solo para replicar el DDL).

**Se conserva**: `AppDbContext` (sin entidades, igual que los otros tres servicios), `GenericRepository<T>`, `UnitOfWork`, `DependencyInjection`, `Program.cs` sin migración ni espera, `HealthController`, manejo global de errores, Serilog, Dockerfile, `.config/dotnet-tools.json` y el paquete `Design` (los dos últimos son el requisito del procedimiento de ingeniería inversa, FR-029) y el pin `System.Security.Cryptography.Pkcs`. **Por qué**: cumple la instrucción explícita y la constitución §6; el modelo que haga falta se regenera con D-06 cuando una feature lo necesite. Esta decisión resuelve el pendiente "clasificación de entidades" de la spec y **precisa** FR-022 y el escenario 5 de US4: la evidencia que justifica conservar una entidad es una referencia actual o una función de Control implementada o aprobada, no una necesidad futura supuesta; la duda mantiene la entidad provisionalmente solo hasta reunir evidencia (ver nota en la spec).

**Efecto sobre `Acity.Domain` de Control**: queda sin contenido, igual que en los otros tres servicios; el proyecto se conserva por §4 (cinco proyectos por servicio). No se crean carpetas vacías.

## D-05 Orden y fallo en Compose (Dec, Plan)

**Decisión**: servicio `database-runner` (build `./database`, `platform: linux/amd64`, `restart: "no"`, entorno `DB_NAME`, `MSSQL_SA_PASSWORD`, `DB_HOST=sqlserver`, `DB_PORT=1433`), con `depends_on: sqlserver: service_healthy`. Los cuatro servicios dependen de `database-runner: condition: service_completed_successfully`; se retira la dependencia de los tres servicios de `ms-control healthy` y la de Control de `sqlserver healthy` (el runner ya la garantiza). Los servicios siguen sin migrar. **Por qué**: Compose soporta la condición `service_completed_successfully` para tareas de ejecución única; si el runner sale con código distinto de 0 los dependientes no arrancan. **Por comprobar en la implementación** (no se asume): el comportamiento exacto de `docker compose up -d` con un runner fallido y la reejecución del runner (exit 0 sin pendientes) en cada `up`. **Descartadas**: dejar que Control espere al runner (reintroduce acoplamiento); un `ms-*` de migraciones o un ejecutable .NET (prohibido por §11).

## D-06 Ingeniería inversa: procedimiento (Dec, Plan)

**Decisión**: operación manual de desarrollo, nunca en arranque ni en las imágenes. Procedimiento documentado en `data-model.md`: con la base creada por el runner, ejecutar `dotnet ef dbcontext scaffold` con la herramienta local, el proveedor SQL Server, `--table` por cada tabla que necesite el servicio, salida a una carpeta temporal fuera de las capas (`--output-dir`, `--context-dir`, sin `OnConfiguring`), revisar el resultado e integrar a mano las entidades en `Acity.Domain` y el contexto y los mapeos en `Acity.Infrastructure`, sin sobrescribir personalizaciones (se compara antes de copiar, sin `--force` sobre carpetas del repositorio) y sin CRUD ni lógica. **Por qué**: evita que el generador sobrescriba código de las capas y respeta las capas aprobadas. **Los otros tres servicios**: no se les añaden entidades, paquetes ni herramientas ahora; cuando su feature funcional las necesite, esa feature añade en su propio servicio el paquete de diseño y el manifiesto de herramientas (mismas versiones ya autorizadas) y aplica el procedimiento, ya que los servicios son independientes y no comparten herramientas. **Verificación en la implementación**: ejecutar el procedimiento contra la base nueva para una tabla, con salida a una carpeta temporal de verificación y sin integrar resultados, para comprobar que funciona; luego descartar la salida.

## D-07 Qué se conserva al cambiar `/health` y la fundación (Dec, Spec FR-019, FR-020)

`GET /health` no cambia de código; solo cambia su semántica documentada: en Control indica que el host arrancó, igual que en los demás, y el esquema lo garantiza el orden de Compose. Con `DatabaseInitializer` retirado, Control ya no espera a SQL Server por sí mismo: su arranque depende del orden de Compose.

## D-08 Secretos y salida del runner (Dec, Obs)

Se conserva el manejo actual: `MSSQL_SA_PASSWORD` por entorno, `SQLCMDPASSWORD` interno, sin contraseñas en argumentos ni en el registro. La implementación revisa que la salida del runner no contenga la contraseña (SC y FR-008). Se mantiene la nota ya conocida: si `MSSQL_SA_PASSWORD` está exportada en el shell, Compose usa esa y no la de `.env`.

## D-09 Qué documentos quedan afectados (Plan, para la implementación)

No se modifican en esta etapa. En la implementación se añaden notas de "sustituido por la Feature 003" donde corresponda, sin reescribir los documentos históricos: Feature 002 (`spec.md` FR-022 y FR-025 a FR-034; US2 y US3; SC-006 a SC-009; `plan.md`; `research.md` D-05 a D-07; `data-model.md`; `contracts/http-y-operacion.md`; `quickstart.md` escenarios 3 a 10; `tasks.md`) y Feature 001 (notas de estrategia). La constitución ya está actualizada (v3.0.0).

## D-10 Qué no cambia (Dec)

Reglas de negocio, esquema aprobado (V001), procedimientos (V002), roles, contraseñas y usuarios iniciales, `Response<T>`, Serilog y volúmenes de logs, Dockerfiles de los servicios, puertos, `restart: "no"`, nombre del proyecto Compose, `sqlserver` y su volumen. Las bases `CargaMasiva` y `CargaMasivaDb` y sus volúmenes no se tocan.

## Abiertos (resueltos en la implementación; resultados en `quickstart.md`, sección "Resultados")

1. **Resuelto**: `service_completed_successfully` funciona en este Compose: con el runner correcto arrancan los cuatro servicios; con el runner fallido `docker compose up` termina con código 1 (`service "database-runner" didn't complete successfully: exit 1`) y ningún servicio arranca (queda `Created` el recreado). El runner **se vuelve a ejecutar en cada `up`** y sale 0 sin pendientes.
2. **Resuelto**: la comprobación previa detecta `__EFMigrationsHistory` y tablas sin registro y deja la base intacta (probado solo en bases temporales).
3. **Resuelto**: catálogo de `CargaMasivaApp` idéntico al de `CargaMasiva`/`referencias/`; reglas de la Feature 001 repetidas (28 + 27 comprobaciones aprobadas; concurrencia de 40 periodos sin errores).
4. **Resuelto**: `dotnet ef dbcontext scaffold` de una tabla funciona con la herramienta local y el paquete de diseño de Control (comando en `data-model.md`).
5. **Resuelto**: salida del runner sin secretos; `CargaMasiva`, `CargaMasivaDb` y los volúmenes con huella idéntica.
6. **Parcial**: validación completa de Compose hecha en este equipo; equipo de evaluación sin validar (motor amd64 emulado sin soporte de Microsoft).
7. **Hallazgo nuevo**: con SQL Server detenido, la espera acotada del runner tardó unos 9,5 minutos o más.
