# Quickstart: validación manual de la fundación

Guía de verificación manual (sin pruebas automatizadas) para cuando exista la implementación. A la fecha **no existe** código de la fundación y **ningún escenario de este documento se ha ejecutado**. Contratos: [contracts/http-y-operacion.md](contracts/http-y-operacion.md); modelo: [data-model.md](data-model.md); decisiones: [research.md](research.md).

**Limitación vigente**: el motor SQL Server es amd64 y corre emulado en Apple silicon, sin soporte oficial de Microsoft. Lo comprobado hasta ahora (arranque de SQL Server con el runner anterior) **no valida** el flujo con EF Core Migrations ni las imágenes .NET; el flujo no se considera validado hasta ejecutar estos escenarios.

## Prerrequisitos
- Docker; `.env` con `MSSQL_SA_PASSWORD` (nunca versionado) y `DB_NAME=CargaMasivaDb` (o sin definir). Se reutiliza la instancia SQL Server y su volumen existentes; la base anterior `CargaMasiva` no se toca.
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
