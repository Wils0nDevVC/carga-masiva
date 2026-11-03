# Specification Quality Checklist: Fundación del frontend React

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-11-03
**Updated**: 2026-11-03 (v2: Q1–Q3 resueltas)
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs). El stack lo fijan la constitución §11 y el prompt; las versiones, puertos y estructura quedan para el plan
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

**Arranque, configuración y navegación**

| Caso | Escenarios | Requisitos | Criterio | Depende de |
|---|---|---|---|---|
| Compila sin errores | US1-1 | FR-004 | SC-001 | — |
| Layout y 4 secciones provisionales y página "no encontrada" | US1-2, US1-3 | FR-006 | SC-001 | — |
| URL del Gateway configurable sin cambiar código (es la configuración del reenvío) | US1-5 | FR-003, FR-005a | — | Q2 |
| En Compose (`frontend/`), mismo origen y recarga de rutas internas | US1-4 | FR-005, FR-005a | SC-006 | Q2, Q3 |

**Sesión**

| Caso | Escenarios | Requisitos | Criterio | Depende de |
|---|---|---|---|---|
| Rutas protegidas sin sesión → Login | US2-1 | FR-007 | SC-002 | — |
| Sesión válida muestra la ruta; cierre de sesión limpia los tokens | US2-2, US2-3 | FR-008 | — | — |
| Sesión en `sessionStorage`: se conserva al recargar y se elimina al cerrar sesión o si falla la renovación | US2-3, US2-4, US3-9 | FR-009, FR-014 | SC-005 | Q1 |

**Infraestructura HTTP** (se verifica sin pantallas funcionales; ver Assumptions)

| Caso | Escenarios | Requisitos | Criterio | Depende de |
|---|---|---|---|---|
| `Response<T>`: `data` en el éxito; `code`, `message` y `errors` en el error; error genérico sin contrato | US3-1 a US3-3 | FR-011 | — | — |
| Bearer en las peticiones protegidas; no en login ni refresh | US3-4, US3-5 | FR-012 | SC-005 | — |
| Una sola renovación para peticiones simultáneas y un reintento | US3-6, US3-7 | FR-013 | SC-003 | — |
| Sin bucles: 401 tras el reintento → error | US3-8 | FR-013 | SC-004 | — |
| Fallo del refresh → limpiar sesión y Login | US3-9 | FR-014 | SC-005 | — |
| 403 u otros errores sin renovación | US3-10 | FR-013 | — | — |
| Sin tokens en la consola | — | FR-015 | SC-007 | — |

**Fuera de esta feature**: las pantallas funcionales (formulario de login, subida, tabla de historial, detalle, contenido y *polling*) no se verifican aquí.

## Notes

- Constitución enmendada a 3.7.0 (React incluido, stack y organización) y a 3.8.0 (Q1–Q3: `sessionStorage`, mismo origen y `frontend/` en §2), ambas con autorización expresa del usuario. §9 no cambia (Gateway sin CORS).
- El contrato de refresh de F008 se respeta: el refresh token va en el cuerpo JSON y la respuesta trae un par nuevo. No se inventan cookies.
