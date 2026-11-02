# Quickstart: validación manual del runner y de Control refactorizado

Guía de verificación manual (sin pruebas automatizadas) para cuando exista la implementación. **Ningún escenario se ha ejecutado**; no se declara Docker validado. Contratos: [contracts/runner-y-compose.md](contracts/runner-y-compose.md); modelo: [data-model.md](data-model.md); decisiones: [research.md](research.md).

**Limitación vigente**: el motor SQL Server y la imagen del runner son amd64 y corren emulados en Apple silicon, sin soporte oficial de Microsoft. La validación vale solo para este equipo; el equipo de evaluación queda sin validar.

## Prerrequisitos
- Docker; `.env` con `MSSQL_SA_PASSWORD` (nunca versionado) y `DB_NAME=CargaMasivaApp`. Si `MSSQL_SA_PASSWORD` está exportada en el shell, Compose usa esa y no la de `.env`; SQL Server conserva la contraseña con la que se inicializó el volumen.
- Se reutiliza la instancia SQL Server y su volumen. `CargaMasiva` y `CargaMasivaDb` no se tocan: tomar antes y después una huella de catálogo (conteo de objetos y fecha de modificación) para comprobarlo.
- Las bases de pruebas llevan el prefijo `CargaMasivaApp_` y se eliminan al terminar; nunca se eliminan las bases existentes ni volúmenes ni se usa `docker compose down -v`.

## Escenarios

| # | Acción | Resultado esperado | Spec |
|---|---|---|---|
| 1 | `docker compose up -d --build` con `DB_NAME=CargaMasivaApp` (base inexistente) | SQL Server healthy → `database-runner` termina con código 0 → los 4 servicios arrancan y quedan healthy | US2, FR-010/011 |
| 2 | Catálogo de `CargaMasivaApp` contra `referencias/V001`/`V002` | 7 tablas, índices, CHECK, FK, collation, 2 roles, 2 procedimientos sin diferencias; `SchemaMigraciones` con 2 filas y checksums | US1, SC-001 |
| 3 | `docker compose up -d` de nuevo, sin scripts nuevos | El runner informa "Sin scripts pendientes", sale 0 y no cambia el esquema ni el registro; los servicios siguen sanos | US1-3, SC-002 |
| 4 | Añadir un script de prueba de número mayor (copia temporal fuera del repositorio o en una base temporal) y ejecutar el runner | Solo ese script se aplica y se registra; los anteriores no se reejecutan | US1-4, SC-003 |
| 5 | Modificar un script aplicado y, aparte, añadir uno de número menor (en una base temporal) | El runner falla sin aplicar nada | US1-5, SC-004 |
| 6 | Forzar un fallo de script en una base temporal (p. ej., objeto en conflicto) | El script se revierte y no se registra; el runner sale ≠ 0; con Compose, ningún microservicio arranca | US1-6, US2-2, SC-005 |
| 7 | Dos ejecuciones simultáneas del runner sobre una base temporal | Solo una aplica; sin duplicados ni corrupción | US1-7 |
| 8 | Apuntar el runner a una base temporal con `__EFMigrationsHistory` y a otra con tablas sin `SchemaMigraciones` | Error claro y base intacta en ambos casos; los servicios no arrancan | US6-2, FR-013 |
| 9 | Detener SQL Server y ejecutar el runner | Espera acotada y error sin crear nada | US1-1 |
| 10 | Revisar salida y registros del runner | Sin contraseña ni cadenas de conexión | US1-8, FR-008 |
| 11 | Revisar `ms-control`: repetir la búsqueda de referencias por entidad antes de retirar y registrar el resultado final por entidad (uso o ausencia; conservada solo con referencia actual o función implementada/aprobada) | 0 migraciones, 0 inicializador, 0 entidades ni configuraciones salvo las que la evidencia justifique y quede documentada; compila sin advertencias; sin referencias entre servicios ni paquetes no autorizados | US3, US4, SC-008/009 |
| 12 | Arrancar `ms-control` contra la base del runner | Escucha sin operaciones de esquema; `GET /health` 200; no crea `__EFMigrationsHistory` | US3-2 |
| 13 | Comparar esquema y registro antes y después de arrancar los cuatro servicios | Idénticos | US2-4, SC-007 |
| 14 | Repetir las reglas de la Feature 001 sobre `CargaMasivaApp` | Mismos resultados que con el runner y con EF anteriores | US6-4 |
| 15 | Ejecutar la ingeniería inversa de una tabla contra `CargaMasivaApp` con salida a una carpeta temporal | Genera solo esa tabla; no se integra ni se añade al repositorio; no se ejecuta en arranque ni en imágenes | US5, SC-010 |
| 16 | Revisar `CargaMasiva`, `CargaMasivaDb` y los volúmenes | Sin cambios respecto de la huella inicial | US6-1, SC-011 |
| 17 | Revisar dependencias y proyectos | 0 paquetes nuevos y 0 proyectos o dependencias de pruebas | SC-012 |

Comprobar y registrar en la implementación (no se asumen): comportamiento de `docker compose up -d` con un runner fallido y la reejecución del runner en cada `up` (research "Abiertos").

Escenarios 1, 3, 6, 7, 8, 9 y 13 requieren Docker con SQL Server en el equipo de ejecución. El resultado de cada uno se anota en una sección "Resultados" de este archivo al implementar, indicando también los no ejecutados.
