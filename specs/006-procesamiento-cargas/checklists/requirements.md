# Specification Quality Checklist: Procesamiento de cargas en ms-carga-masiva

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-11-03
**Updated**: 2026-11-03 (v2: Q1 y Q2 resueltas)
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain (Q1 y Q2 resueltas por el usuario el 2026-11-03)
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

## Criterios verificables del flujo, fallos y recuperación (trazabilidad)

| Caso | Escenarios | Requisitos | Criterio |
|---|---|---|---|
| Flujo completo con el Excel de ejemplo | US1-1 | FR-001, FR-005–007, FR-009–012, FR-015, FR-020 | SC-001 |
| Rechazo y bloqueo por periodo | US1-2, US1-6 | FR-010 | SC-002 |
| Existente y ganador por orden original | US1-3, US1-4 | FR-011, FR-012 | SC-002 |
| Ausentes, inválidos, normalización, longitudes | US1-5 | FR-009 | SC-002 |
| Exclusividad del consumidor | US2-1, US2-5 | FR-003 | SC-004 |
| Reentregas, duplicados, mensajes inválidos | US2-2 a US2-4 | FR-002, FR-004 | SC-003 |
| Recuperación En proceso / Cargado / intentos | US3-1 a US3-6 | FR-018, FR-019 | SC-003, SC-005 |
| Fallo de descarga y fallo terminal de un periodo | US4-1, US4-2 | FR-016, FR-017 | SC-006 |
| Transitorio y caída | US4-3, US4-5 | FR-016, FR-019 | SC-005 |
| Colisión de código | US4-4 | FR-013 | SC-003 |
| Ganador no persistido | Edge Cases | FR-014 | SC-002 |
| Carrera de la 005 con el consumidor real | US5-1 a US5-3 | FR-005, FR-023 | SC-007 |
| Archivo inválido (corrupto, sin hojas, sin encabezados) → `ArchivoInvalido`, Finalizado, notificación | US4-7 | FR-008, FR-008a | SC-009 |
| Archivo inexistente → `ErrorTecnico`, Fallido, sin reintentos | US4-6 | FR-008b | SC-009 |
| Notificación no confirmada → Finalizado + `ErrorTecnico`, sin recuperación | US4-8 | FR-022 | SC-009 |
| Logs sin secretos ni contenido | — | FR-006 | SC-008 |

## Notes

- Igual que las features 002 a 005, la spec nombra artefactos (BackgroundService, ClosedXML, SeaweedFS, RabbitMQ, procedimientos, Polly) porque los fijan el README, la constitución y el prompt; los parámetros y el diseño quedan para el plan.
- Las reglas de negocio se aplican por referencia a la constitución y a F001 (tabla "Reglas aprobadas"), sin reinterpretarlas.
- Referencias GitHub: acceso web parcial; no muestran consumidores ni lectura de Excel.
- Verificaciones de la 005: distinguidas en la tabla "Verificaciones de la feature 005" (realizadas frente a pendientes), sin declarar resultados.
- Q1 (archivo inválido; script mínimo para el `CHECK`) y Q2 (notificación no confirmada, sin recuperación automática) resueltas por decisión autorizada del usuario; constitución actualizada a v3.2.0 solo en §7, §8 y Pendientes; nota de precisión en F001 (data-model y research R-03). Limitación aceptada: no se garantiza la notificación posterior de una carga cuya notificación no se confirmó.
- Sin pendientes que requieran decisión. Para el plan: límite de intentos, Polly, topología de `notificaciones`, formato de `fechaFin`, número del script.
