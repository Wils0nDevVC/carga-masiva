# Specification Quality Checklist: Consultas y seguimiento de cargas en ms-control

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-11-03
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [ ] No [NEEDS CLARIFICATION] markers remain (Q1 pendiente: definición de "contenido del archivo")
- [x] Requirements are testable and unambiguous (salvo FR-005, que depende de Q1)
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [ ] All functional requirements have clear acceptance criteria (FR-005 y US3 dependen de Q1)
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Criterios verificables (trazabilidad)

| Caso | Escenarios | Requisitos | Criterio | ¿Depende de Q1? |
|---|---|---|---|---|
| Historial: Administrador ve todas; Operador solo las propias | US1-1, US1-2 | FR-001, FR-007 | SC-001 | No |
| Historial vacío → 200 con lista vacía | US1-3 | FR-002 | SC-004 | No |
| Sin token o con token inválido → 401 | US1-4 | FR-006 | SC-007 | No |
| *Polling* sin consumir el Rate Limiting de carga | US1-5 | FR-010 | SC-005 | No |
| Detalle completo (estados, periodos e incidencias) | US2-1, US2-6 | FR-003 | SC-003 | No |
| Detalle sin datos → listas vacías | US2-2 | FR-004 | SC-004 | No |
| Carga ajena = inexistente (404) | US2-3, US3-2 | FR-008 | SC-002 | No |
| Administrador ve cualquier detalle | US2-4 | FR-007 | SC-001 | No |
| Identificador inválido o inexistente | US2-5 | FR-003 | — | No |
| Contenido del archivo | US3-1, US3-3 | FR-005 | SC-001 | **Sí** |
| Solo lectura | — | FR-009 | SC-006 | No |
| Consultas a través del Gateway | — | FR-012 | SC-007 | No |
| Logs sin tokens ni contenido | — | FR-014 | — | No |

## Notes

- La spec nombra JWT, `Response<T>`, SeaweedFS y las entidades porque los fijan el README, la constitución y las features anteriores.
- No se cambia la constitución, el código ni el esquema.
- Mapeos adicionales: `CargaPeriodo` (necesario para los resultados por periodo) y `Producto` (solo si Q1 incluye los productos insertados).
- Observación para el plan: el Rate Limiting está declarado a nivel de controlador en `CargasController`. FR-010 exige que no afecte a las consultas.
