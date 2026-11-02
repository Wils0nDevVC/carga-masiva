# Specification Quality Checklist: Recepción y registro de cargas en ms-control

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-11-02
**Updated**: 2026-11-02 (v2: aclaraciones Q1–Q3 y pendientes resueltos)
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
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

## Criterios verificables del flujo y sus fallos (trazabilidad)

| Caso | Escenarios | Requisitos | Criterio |
|---|---|---|---|
| Envío válido por rol → 202 con `idCarga` | US1-1, US1-2 | FR-003, FR-004, FR-007 | SC-001 |
| Carga + historial Pendiente, sin binario | US1-3 | FR-018, FR-019 | SC-001 |
| Archivo en SeaweedFS igual al enviado | US1-4 | FR-018 | SC-001 |
| Mensaje `carga_masiva` con correo persistido | US1-5 | FR-008, FR-021, FR-022 | SC-001 |
| Límite exacto de 10 MiB y multipart | US1-7, US3-7, US3-8 | FR-014, FR-015 | SC-006 |
| Sin token / token inválido → 401 | US2-1, US2-2 | FR-005, FR-007 | SC-002, SC-004 |
| Rol sin permiso → 403 | US2-3 | FR-007 | SC-002, SC-004 |
| Usuario inexistente | US2-4 | FR-008 | SC-002 |
| Sin archivo, varios, 0 bytes, extensión, nombre largo → 400 | US3-1 a US3-6 | FR-010 a FR-013, FR-017 | SC-002, SC-004 |
| Excel ilegible aceptado | US3-9 | FR-016 | — |
| Rate Limiting → 429 | US4-1, US4-2 | FR-027 | SC-002, SC-004 |
| Fallo de subida / registro | US5-1, US5-2 | FR-018, FR-020 | SC-005 |
| Fallo definitivo de publicación → Fallido + historial + incidencia | US5-3 | FR-023 | SC-005 |
| Timeout ≠ no entregado; duplicados | US5-4, US5-6 | FR-022 | SC-005 |
| Carga ya tomada nunca pasa a Fallido por Control (sin historial ni incidencia) | US5-5 | FR-023, FR-024 | SC-005 |
| Script con origen esperado opcional, compatible y atómico | US5-5 | FR-032 | SC-008 |
| Transitorio sin duplicar efectos | US5-7 | FR-025, FR-026 | SC-001 |
| Fallido no registrable | US5-8 | Limitaciones | SC-005 |
| `Response<T>` camelCase, `data` null | todas | FR-004, FR-028 | SC-003 |
| Logs sin secretos | US2-5, US5-9 | FR-029 | SC-007 |

## Notes

- Igual que las features 002 a 004, la spec nombra artefactos del repositorio (`Response<T>`, `Acity.Api`, SeaweedFS, RabbitMQ, Polly, Compose, claims `sub`/`role`) porque los fijan el README, la constitución y las decisiones del usuario; los detalles técnicos quedan para el plan.
- Aclaraciones resueltas por el usuario (2026-11-02): Q1 = A (Control valida el JWT; `sub` + `role`; correo del usuario persistido), Q2 = A (10 MiB configurable; límites multipart), Q3 = A precisado (subir → registrar → publicar; Fallido ante fallo definitivo de publicación). Pendientes del plan resueltos: 0 bytes, varios archivos, nombre largo, 202, `data` null y camelCase, Rate Limiting en Control, criterios de Polly.
- Constitución actualizada a v3.1.0 solo en las cláusulas afectadas (§5, §9 y Pendientes).
- Referencias GitHub: acceso web parcial; así consta en la spec.
- Mecanismo de FR-023 resuelto por el usuario (opción A): script versionado nuevo que añade a `usp_CambiarEstadoCarga` un estado de origen esperado opcional, atómico y compatible (FR-032, SC-008). Consecuencia documentada: si el cambio no se aplica porque la carga ya salió de Pendiente, Control responde 202 (FR-024) y no registra incidencia. Nota de precisión añadida en `specs/001-persistencia-integridad-carga/contracts/procedimientos-almacenados.md`.
- Sin pendientes que requieran decisión del usuario. Lista para `/speckit-plan`.
