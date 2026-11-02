# Implementation Plan: Persistencia y reglas de integridad para la carga masiva

**Branch**: `001-persistencia-integridad-carga` (trabajo en `main`; sin rama dedicada) | **Date**: 2026-11-02 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-persistencia-integridad-carga/spec.md`

**Procedencia**: `Req` = README; `Dec` = decisión del usuario; `Prop` = propuesta de este plan, **no aprobada** hasta que el usuario la confirme (las aceptadas se indican como `Dec`). El README no es fuente de ninguna decisión marcada `Dec`.

## Summary

Se define el soporte de persistencia en SQL Server para los cuatro microservicios: **8 tablas** (identidad, carga, historial, periodos, productos, incidencias y registro de scripts), dos procedimientos almacenados acotados, un cerrojo de sesión para la exclusividad del consumidor, y un runner único de scripts. Enfoque:

- El **resultado de cada periodo** (`CargaPeriodo`) se separa del estado de `CargaArchivo`; la disponibilidad de un periodo se decide por ese resultado. [Dec]
- **Integridad por restricciones**: unicidad global de `CodigoProducto` con collation insensible a mayúsculas y un índice único filtrado `PeriodoReservado IS NOT NULL` que reserva exactamente los periodos Admitido y Completado. [Dec + Prop]
- **Orden original preservado** decidiendo ganadores sobre todo el archivo antes de persistir grupo a grupo; Existente solo con producto confirmado. [Dec + Prop]
- **Atomicidad por periodo** con transacción EF; los procedimientos son transacciones propias fuera de ella. [Dec + Prop]
- **Un solo consumidor activo** garantizado por un cerrojo de aplicación de SQL Server, con recuperación previa al consumo, acotada por `IntentosProceso`. [Dec + Prop]
- Matriz de transiciones con historial; error transitorio ≠ fallo terminal. [Dec]

Entrega **solo documentación de diseño**; los scripts, el runner, el consumidor y Autenticación se producen en sus features.

## Technical Context

**Language/Version**: T-SQL para `database/`; C# / .NET 10 en los consumidores del modelo (fuera de esta feature). [Dec]

**Primary Dependencies**: Microsoft.EntityFrameworkCore.SqlServer 10.x; compatibilidad y licencia **sin verificar** en esta sesión (verificar antes de implementar). Sin Dapper, sin EF Migrations, sin BulkExtensions. [Dec]

**Storage**: SQL Server. Versión propuesta (Prop): 2022, porque las imágenes ya existen en el equipo y hay un contenedor 2022 en ejecución en este Docker; la constitución no fija versión; 2025 no se evaluó.

**Testing**: Sin pruebas automatizadas; verificación manual (`quickstart.md`). [Dec]

**Target Platform**: Contenedores Docker. Hallazgos de R-11: equipo de desarrollo Apple M4 Pro (arm64) con Docker Desktop aarch64; las imágenes de SQL Server son amd64 y Microsoft declara **no soportada** la emulación (Rosetta/QEMU); no se declara Docker funcional hasta verificarlo. El equipo de evaluación es desconocido.

**Project Type**: Monorepo de microservicios; esta feature afecta a `database/` y fija el modelo que consumirán `ms-autenticacion/`, `ms-control/`, `ms-carga-masiva/` y `ms-notificaciones/`; `api-gateway/` no cambia. [Dec]

**Performance Goals**: No definidos. **Constraints**: un único responsable de esquema; sin contraseñas en scripts; sin binario del Excel en la base. **Scale/Scope**: mínimo para el examen; sin límites deducidos del Excel.

## Constitution Check

*GATE: antes de Phase 0 y tras Phase 1.* Constitución v1.3.0.

| Principio | Verificación | Resultado |
|---|---|---|
| §1 Alcance | Propuestas marcadas `Prop`; sin tablas preventivas; pendientes visibles | Pasa |
| §2 Organización | Cambios solo en `database/`; sin Git ni Spec Kit por componente | Pasa |
| §3–4 Plataforma y capas | Sin proyectos ni capas; entidades como clases cuando se implementen | Pasa |
| §6 Persistencia | SQL Server; EF Core/LINQ; scripts versionados con registro; único responsable; 2 SPs conforme al README; sin Dapper; sin contraseñas en scripts | Pasa |
| §6 Identidad | Identidad propia; roles; hash; creación repetible por ms-autenticacion | Pasa |
| §7 Reglas | Unicidad global; sin llave compuesta; atomicidad por periodo; disponibilidad por resultado; orden original; normalización de código | Pasa |
| §7 Precisión y longitudes | Fila omitida con motivo si no cabe exactamente; sin redondeo ni truncamiento | Pasa |
| §7 Fallo terminal y duplicados | Archivo detenido; periodos pendientes NoProcesado; Existente solo con producto confirmado | Pasa |
| §8 Estados | Seis estados; matriz; Cargado→Finalizado recuperable; fallo de correo conserva Finalizado | Pasa |
| §9 Consumidor único | Cerrojo de sesión + recuperación previa; sin servicios de barrido, Cronos ni Redis | Pasa |
| §9 Polly | Parámetros diferidos a las features de Control, Carga Masiva y Notificaciones | Pasa |
| §10 Logging | Auditoría persistente independiente de los `.log` | Pasa |
| §11 Pruebas | Sin proyectos de pruebas; quickstart manual | Pasa |
| §12 Dependencias | Sin paquetes nuevos (solo EF Core SQL Server) | Pasa |

Tras Phase 1: sin violaciones nuevas.

## Project Structure

### Documentation (this feature)

```text
specs/001-persistencia-integridad-carga/
├── plan.md                              # Este archivo
├── research.md                          # Decisiones, mecanismos y verificaciones
├── data-model.md                        # 8 tablas, tipos, restricciones y matriz de transiciones
├── quickstart.md                        # Validación manual
├── contracts/
│   └── procedimientos-almacenados.md    # 2 SPs, cerrojo del consumidor, identidad
└── tasks.md                             # Phase 2 (/speckit-tasks; NO creado aquí)
```

### Source Code (repository root)

Solo `database/` (se crea en `/speckit-tasks`/implementación; nombres de archivo provisionales; el runner se diseña pero no se implementa aquí):

```text
database/
├── scripts/
│   ├── V001__esquema_inicial.sql        # 8 tablas, restricciones, índices, roles de referencia
│   └── V002__procedimientos.sql         # usp_ReservarPeriodo, usp_CambiarEstadoCarga
├── run-scripts.sh                       # runner (diseño en research R-12)
└── Dockerfile                           # imagen del runner
```

`compose.yaml` (raíz, hoy vacío) declarará SQL Server y el runner. `ms-*` y `api-gateway` no cambian en esta feature.

**Structure Decision**: Un directorio `database/` con scripts numerados y un runner propio de un solo uso; sin proyecto .NET de migraciones.

## Complexity Tracking

| Elemento | Por qué es necesario | Alternativa más simple rechazada porque |
|---|---|---|
| Runner de scripts propio | Único responsable, ejecución automática y registro de aplicados [Dec] | Que cada servicio aplique scripts rompe "único responsable"; EF Migrations está prohibido |
| 2 procedimientos almacenados | Operaciones multi-sentencia atómicas (reserva; estado + historial) y requisito de SPs [Req] | Hacerlo solo en aplicación incumple el README y expone carreras |
| `CargaPeriodo` separada de `CargaArchivo` | Resultado de periodo ≠ estado de la carga [Dec] | Derivar del estado del archivo está prohibido |
| `PeriodoReservado` (columna extra, aceptada) | El índice filtrado solo admite comparaciones simples | Filtrar con `IN`/`OR` no está confirmado por la documentación |
| `IntentosProceso` (aceptada, solo para acotar la recuperación) | Acotar la recuperación para no entrar en ciclos de caídas | Marcar Fallido por reiniciar está prohibido; sin contador no hay cota |
| Cerrojo de aplicación de sesión | Exclusividad real con infraestructura existente [Dec] | Una réplica no es garantía; Redis/barrido están excluidos |

## Pendientes tras el plan

Ver `research.md` (Abiertos) y `spec.md` (Asuntos pendientes). Resumen: confirmación de la eliminación de `UsuarioRol` (un rol por usuario), verificación del nombre exacto de la collation al escribir el script y verificación operativa de Docker/SQL Server. Diferidos a otras features: tratamiento operativo del archivo ilegible, recuperación de notificaciones no publicadas, parámetros de Polly y longitud del hash. Ninguno bloquea la creación de la base.
