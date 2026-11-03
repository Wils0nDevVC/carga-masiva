# Specification Quality Checklist: Consultas y seguimiento de cargas en ms-control

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-11-03
**Updated**: 2026-11-03 (v3: Q1 = B y Q2 resueltas)
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain (Q1 = B y Q2 resueltas por el usuario el 2026-11-03)
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Criterios verificables (trazabilidad)

| Caso | Escenarios | Requisitos | Criterio | Decisión |
|---|---|---|---|---|
| Historial: Administrador ve todas; Operador solo las propias | US1-1, US1-2 | FR-001, FR-007 | SC-001 | — |
| Historial vacío → 200 con lista vacía | US1-3 | FR-002 | SC-004 | — |
| Sin token o con token inválido → 401 | US1-4 | FR-006 | SC-007 | — |
| *Polling* sin consumir el Rate Limiting de carga | US1-5 | FR-010 | SC-005 | — |
| Detalle completo (estados, periodos e incidencias) | US2-1, US2-6 | FR-003 | SC-003 | — |
| Detalle sin datos → listas vacías | US2-2 | FR-004 | SC-004 | — |
| Carga ajena = inexistente (404 «La carga no existe») | US2-3, US3-4 | FR-008 | SC-002 | Q2 |
| Administrador ve cualquier detalle | US2-4 | FR-007 | SC-001 | — |
| Identificador inválido o inexistente | US2-5 | FR-003 | — | — |
| Contenido = filas originales sin normalizar | US3-1, US3-2, US3-3 | FR-005, FR-005a | SC-001a | Q1 = B |
| Contenido de carga ajena → 404 sin leer el archivo | US3-4 | FR-005c, FR-008 | SC-002 | Q1 = B |
| Ilegible → 422; SeaweedFS inaccesible → 503; archivo ausente → 500; nunca vacío | US3-5, US3-6, US3-6a | FR-005d | SC-001b | Q2 |
| Consulta de contenido sin efectos | US3-7 | FR-005b | SC-006 | Q1 = B |
| Solo lectura | — | FR-009 | SC-006 | — |
| Consultas a través del Gateway | — | FR-012 | SC-007 | — |
| Logs sin tokens ni contenido | — | FR-014 | — | — |

## Notes

- La spec nombra JWT, `Response<T>`, SeaweedFS y las entidades porque los fijan el README, la constitución y las features anteriores.
- No se cambia la constitución, el código ni el esquema.
- Mapeos adicionales: solo `CargaPeriodo` (resultados por periodo). `Producto` no se mapea (Q1 = B).
- Documentos aprobados afectados por Q1 = B: la 005 (FR-016 y FR-002, y el acceso de solo escritura a SeaweedFS) se amplía para la consulta, sin cambiar la recepción. La constitución no cambia (ClosedXML ya está aprobado, §12).
- Observación para el plan: el Rate Limiting está declarado a nivel de controlador en `CargasController`. FR-010 exige que no afecte a las consultas.
