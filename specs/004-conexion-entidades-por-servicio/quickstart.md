# Quickstart: validación manual de conexión, entidades y mapeos

Guía de verificación manual (sin pruebas automatizadas) para cuando exista la implementación. **Ningún escenario se ha ejecutado**; no se declara Docker validado. Contratos: [contracts/ingenieria-inversa-y-comprobacion.md](contracts/ingenieria-inversa-y-comprobacion.md); modelo: [data-model.md](data-model.md); decisiones: [research.md](research.md).

**Limitación vigente**: el motor SQL Server amd64 corre emulado en Apple silicon, sin soporte oficial de Microsoft. La validación vale solo para este equipo; el equipo de evaluación queda sin validar.

## Prerrequisitos
- Compose levantado y `CargaMasivaApp` creada por el runner (`docker compose up -d`); `.env` con `DB_NAME=CargaMasivaApp`. Si `MSSQL_SA_PASSWORD` está exportada en el shell, Compose usa esa y no la de `.env`; SQL Server conserva la contraseña con la que se inicializó el volumen.
- Antes de empezar, huella de solo lectura de `CargaMasiva`, `CargaMasivaDb` y de `CargaMasivaApp` (objetos, fecha máxima de modificación y filas de `SchemaMigraciones`) y de los volúmenes, para compararlas al final. Nunca `docker compose down -v` ni borrar volúmenes.
- Las bases de prueba llevan el prefijo `CargaMasivaApp_` y se eliminan al terminar.

## Escenarios

| # | Acción | Resultado esperado | Spec |
|---|---|---|---|
| 1 | Ejecutar los cuatro comandos de ingeniería inversa del contrato (salida a carpeta temporal fuera del repositorio) | Por servicio, una clase por tabla solicitada más `ScaffoldContext.cs`; 2, 3, 4 y 1 tablas; `git status` sin archivos nuevos en el repositorio; las claves foráneas hacia tablas no incluidas aparecen solo como columnas | US3, FR-012, FR-018 |
| 2 | Integrar manualmente cada servicio según las reglas del contrato y compilar `dotnet build ms-<s>/src/Acity.<S>.slnx` | Los 4 compilan sin advertencias; entidades en `Acity.Domain/Entities` y un mapeo por entidad en `Persistence/Configurations`; sin `DbSet`, navegaciones, `HasIndex`, nombres de restricciones ni valores por defecto SQL | US4, FR-014, FR-017 |
| 3 | Comprobar que cada servicio tiene exactamente las entidades de la matriz | ms-autenticacion: `Rol`, `Usuario`; ms-control: `CargaArchivo`, `CargaEstadoHistorial`, `Incidencia`; ms-carga-masiva: `CargaArchivo`, `CargaPeriodo`, `Producto`, `Incidencia`; ms-notificaciones: `Incidencia`; 0 fuera de la matriz; 0 servicios con las siete | US2, SC-003 |
| 4 | Revisar la justificación y la procedencia de cada mapeo en `research.md` D-02/D-03 | El 100 % con función y fuente; 0 por necesidades futuras supuestas; `Usuario` y `Producto`/`CargaPeriodo` en Control y `CargaArchivo` en Notificaciones ausentes | US2, SC-004 |
| 5 | Con el `.cs` de comprobación de cada servicio (fuera del repositorio): conexión, `DB_NAME`, entidades y lectura LINQ | `CanConnect=True`; `DB_NAME=CargaMasivaApp`; 2, 3, 4 y 1 entidades; lectura sin error (`Rol` con 2 filas, el resto vacío) | US1, SC-001 |
| 6 | Volcado del modelo de cada servicio frente al catálogo de `CargaMasivaApp` | 0 diferencias en tabla, columna, tipo, longitud, nulabilidad, clave, generación de valores y claves foráneas | US3, SC-005 |
| 7 | En una base temporal creada por el runner: insertar por SQL filas de prueba y consultar con LINQ `CodigoProducto` ("p0117" frente a "P0117"; "á" frente a "a") | "p0117" encuentra "P0117"; "á" no iguala "a"; si falla, registrar el error y quitar `UseCollation` del mapeo | SC-007, FR-015 |
| 8 | Huella del esquema y de `SchemaMigraciones` de `CargaMasivaApp` antes y después de arrancar los cuatro servicios y de ejecutar la comprobación | Idéntica; sin `__EFMigrationsHistory`; sin operaciones de esquema | US1-4, SC-006, FR-004 |
| 9 | `GET /health` de cada servicio y revisión de `git diff` | 200 en 8081–8084; sin endpoints, controladores, handlers, repositorios por entidad, `BaseEntity` ni cambios en `.csproj` o paquetes; 0 referencias entre servicios | SC-008, FR-019, FR-020 |
| 10 | Revisar logs de los servicios y la salida de los scripts de comprobación | 0 coincidencias de la contraseña y de la cadena de conexión | SC-002, FR-002 |
| 11 | Huella de `CargaMasiva`, `CargaMasivaDb` y los volúmenes frente a la inicial | Idéntica | SC-010, FR-023 |
| 12 | Revisar constitución y README en `git diff` | Sin cambios | SC-010 |

Fallo de conexión con cuerpo `Response<T>`: no ejercitable sin un punto de ejecución de datos; se indica como no ejecutado y solo se comprueba que la cadena no aparece en logs (escenario 10).

Los escenarios 1, 5, 7 y 8 requieren Docker con SQL Server en el equipo de ejecución. Al implementar, los resultados se anotan en una sección "Resultados" de este archivo, indicando también los no ejecutados.
