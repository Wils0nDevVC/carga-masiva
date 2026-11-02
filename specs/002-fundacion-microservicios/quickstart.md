# Quickstart: validación manual de la fundación

Guía de verificación manual (sin pruebas automatizadas) para cuando exista la implementación. El 2-nov-2026 se implementó la fundación y se ejecutaron los escenarios que indica la sección **Resultados** al final; los no ejecutados se señalan como tales. Contratos: [contracts/http-y-operacion.md](contracts/http-y-operacion.md); modelo: [data-model.md](data-model.md); decisiones: [research.md](research.md).

**Limitación vigente**: el motor SQL Server es amd64 y corre emulado en Apple silicon, sin soporte oficial de Microsoft. La validación de abajo se hizo **solo en este equipo**; no se generaliza al equipo de evaluación.

## Prerrequisitos
- Docker; `.env` con `MSSQL_SA_PASSWORD` (nunca versionado) y `DB_NAME=CargaMasivaDb` (o sin definir). Si `MSSQL_SA_PASSWORD` está exportada en el shell, Compose usa esa y no la de `.env`; SQL Server conserva la contraseña con la que se inicializó el volumen. Para `dotnet ef` basta cualquier `ConnectionStrings__Default` (no se conecta). Se reutiliza la instancia SQL Server y su volumen existentes; la base anterior `CargaMasiva` no se toca.
- Para ejecutar sin Docker: SDK .NET 10 y una instancia SQL Server accesible.

## Escenarios

| # | Acción | Resultado esperado | Spec |
|---|---|---|---|
| 1 | Compilar cada servicio por su ruta (`dotnet build ms-<servicio>/src/Acity.<Servicio>.slnx`) | Los 4 compilan sin compilar los otros | US1, FR-005 |
| 2 | Revisar referencias de proyectos y paquetes | Ninguna referencia entre servicios; sin paquetes no autorizados ni de pruebas | FR-004, FR-044 |
| 3 | `docker compose up` sobre `DB_NAME` nuevo | SQL Server → Control (migra y queda healthy) → los otros tres | US3, FR-039 |
| 4 | Revisar la base nueva con consultas de catálogo | 7 tablas de negocio + `__EFMigrationsHistory`; índices, CHECK, FK, collation, roles base y 2 procedimientos equivalentes a V001/V002; sin `SchemaMigraciones` | FR-026–028 |
| 5 | Repetir los escenarios de base de la Feature 001 (reserva, índice filtrado, comparación de códigos, matriz de estados, fallo terminal) sobre la base migrada | Mismos resultados que con el runner anterior | Feature 001 |
| 6 | Reiniciar Control | No reaplica migraciones; `__EFMigrationsHistory` sin filas nuevas | FR-030 |
| 7 | Revisar la base tras arrancar los otros 3 servicios | Esquema sin cambios; no ejecutan migraciones | FR-031 |
| 8 | Forzar un fallo de migración en una base de pruebas (p. ej., objeto en conflicto) | Control registra el error sin secretos, sale con error, no escucha; los dependientes no arrancan | US2-3, FR-039 |
| 9 | Apuntar Control a la base anterior `CargaMasiva` (objetos sin historial EF) | Control se niega a migrar con un mensaje claro; la base anterior queda intacta | FR-034 |
| 10 | Detener SQL Server y arrancar Control sin Compose | Espera acotada y falla sin quedar listo | US2-4 |
| 11 | `GET /health` de cada servicio | 200 con `Response<string>`; Control solo tras migrar | FR-022 |
| 12 | `GET` a una ruta inexistente y `POST /health` | 404 y 405 como `Response<T>` con `code` igual al estado | US4, FR-014 |
| 13 | Revisar los logs de cada servicio y los volúmenes | Archivos `.log` propios en el volumen de cada servicio; sin contraseñas ni cadenas de conexión | US5, FR-019–021 |
| 14 | Ejecutar sin `ConnectionStrings__Default` | Control falla con mensaje claro sin imprimir secretos | US5-4 |
| 15 | Reconstruir las imágenes y revisar su contenido | Un Dockerfile por servicio; no incluyen proyectos de otros servicios | FR-036 |

Los escenarios de excepción no controlada (500) y de validación (400) con cuerpo real se completan cuando exista un endpoint funcional o un validador; en esta fundación solo se verifican 404/405 y el diseño del manejador. Escenarios 3–10 y 15 requieren Docker con SQL Server en el equipo de ejecución.

## Resultados (2-nov-2026, este equipo: Apple M4 Pro, Docker Desktop; SQL Server 2022 amd64 emulado, servicios .NET arm64 nativos)

| # | Resultado |
|---|---|
| 1 | **Ejecutado**: los 4 compilan por su ruta (`dotnet build ms-<s>/src/Acity.<Servicio>.slnx`) sin advertencias, tras fijar `System.Security.Cryptography.Pkcs` 10.0.14 (ver research D-02) |
| 2 | **Ejecutado**: todas las `ProjectReference` apuntan a proyectos del mismo servicio; paquetes: solo los autorizados más el pin de seguridad; sin paquetes de pruebas |
| 3 | **Ejecutado**: `docker compose up -d --build` sobre `CargaMasivaDb`: SQL Server (healthy) → Control (healthy en ~5 s) → los otros tres (healthy ~20 s) |
| 4 | **Ejecutado**: 7 tablas + `__EFMigrationsHistory` (sin `SchemaMigraciones`), 2 roles, 2 procedimientos. Comparación de catálogo contra la base anterior `CargaMasiva` (idéntica a `referencias/`): solo difieren los 3 `UQ_*` (índice único en vez de restricción), el historial y el comentario inicial de los procedimientos; el texto desde `CREATE PROCEDURE` tiene el mismo hash |
| 5 | **Ejecutado** sobre una base migrada de prueba: 28 comprobaciones de reserva, CHECK, índice filtrado, unicidad y comparación de códigos + 27 de matriz de estados y fallo terminal, todas aprobadas; concurrencia de 40 periodos desde dos sesiones: un solo Admitido por periodo, 0 errores |
| 6 | **Ejecutado** (local y en Docker): reiniciar Control → "No migrations were applied", mismos objetos e historial |
| 7 | **Ejecutado**: los otros tres arrancaron sin modificar el esquema (mismos objetos e historial) |
| 8 | **Ejecutado**: con un procedimiento preexistente, `InitialSchema` se aplicó, `StoredProcedures` se revirtió, Control salió con código 1 sin escuchar y, en Compose, los tres dependientes quedaron sin arrancar |
| 9 | **Ejecutado**: Control apuntando a `CargaMasiva` se negó a migrar con mensaje claro; la base quedó intacta (35 objetos, sin historial de EF, misma fecha de modificación) |
| 10 | **Ejecutado**: puerto cerrado → 30 intentos (~78 s), código de salida 1 y sin escuchar |
| 11 | **Ejecutado**: `/health` → 200 `Response<string>` en los 4 (8081–8084) |
| 12 | **Ejecutado**: ruta inexistente → 404 y `POST /health` → 405 como `Response<T>` con `code` igual al estado |
| 13 | **Ejecutado**: cada servicio escribe su `.log` en su volumen; 0 coincidencias con la contraseña o `Password=` |
| 14 | **Ejecutado** para Control (mensaje claro sin imprimir secretos); los otros tres no la exigen al arrancar |
| 15 | **Ejecutado**: cada imagen contiene solo su servicio; contenedor no root; volumen de logs escribible |

**No ejecutado**: excepción no controlada (500) y validación (400) con cuerpo real: no hay endpoint funcional ni validadores en esta fundación; solo se verificó el diseño del manejador y los 404/405. Validación en el equipo de evaluación: no realizada.
