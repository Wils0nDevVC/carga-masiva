# Specification Quality Checklist: Autenticación con JWT y refresh tokens en ms-autenticacion

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-11-03
**Updated**: 2026-11-03 (v2: Q1–Q3 resueltas)
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain (Q1–Q3 resueltas por el usuario el 2026-11-03)
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
| Login correcto → token de acceso y refresh token | US1-1 | FR-001 a FR-003 | SC-001 | — |
| Claims, firma, emisor, audiencia y vigencia de 60 min | US1-2 | FR-006, FR-007, FR-009 | SC-002 | — |
| Compatibilidad con Control (202 en carga) | US1-3 | FR-008 | SC-001 | — |
| 401 genérico, sin revelar si el usuario existe | US1-4 | FR-004 | SC-003 | — |
| Solicitud incompleta → 400 | US1-5 | FR-005 | — | — |
| Renovación válida → nuevo token aceptado por Control | US2-1 | FR-010, FR-015 | SC-004 | Q3 |
| Refresh alterado, desconocido o de usuario inexistente → 401 | US2-2, US2-7 | FR-011 | SC-004 | — |
| Refresh vencido → 401 | US2-3 | FR-011, FR-013 | SC-004 | Q1 (1 hora) |
| Refresh ya usado → 401 | US2-4 | FR-014, FR-015 | SC-004 | Q2, Q3 |
| Refresh no sirve como acceso, ni el acceso como refresh | US2-5, US2-6 | FR-012 | SC-005 | — |
| Usuarios iniciales: creación, repetibilidad, sin sobrescribir | US3-1 a US3-3 | FR-016 a FR-018 | SC-006 | — |
| Configuración ausente o inválida → no arranca | US3-4 | FR-019 | — | — |
| Logs sin contraseñas, tokens ni clave | — | FR-020 | SC-007 | — |

## Notes

- La spec nombra artefactos (`PasswordHasher`, HS256, `Response<T>`, variables JWT) porque los fijan el README, la constitución y la F005, como en las features 002 a 007.
- La constitución se enmendó a la v3.5.0 (MINOR) con autorización expresa del usuario. Registra los refresh tokens como incluidos, la vigencia de 60 minutos del token de acceso y el 401 genérico. Q1–Q3 resueltas el 2026-11-03: 1 hora configurable, hash en base y rotación.
- Las referencias GitHub fueron accesibles (árbol de archivos); no contienen nada aplicable que copiar.
- Q2 implica un script de esquema V005, que se definirá en el plan. En esta etapa no se altera la base.
