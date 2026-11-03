# Implementation Plan: Pantalla de Historial de cargas

**Branch**: `014-historial-frontend` (trabajo en `main`) | **Date**: 2026-11-03 | **Spec**: [spec.md](spec.md)

## Summary

- `listarCargas()` en `api/cargas.ts` hace `GET /cargas` con el cliente común.
- El hook `useHistorialCargas` consulta con *polling* cada 5 s:
  - en cadena, sin solapamiento;
  - solo con la pestaña visible;
  - se detiene al desmontar;
  - conserva los datos si hay un error.
- `HistorialPage` muestra la tabla (6 columnas, enlaces al detalle), el estado vacío, el indicador de carga y los errores.
- Solo hay un modelo nuevo, `CargaResumenDto`. Sin dependencias. [Spec] [research D-01 a D-03]

## Technical Context

**Language/Version**: TypeScript 7.0.2 y React 19.3.1 (Feature 011).

**Primary Dependencies**: las de la 011; ninguna nueva.

**Storage**: ninguno.

**Testing**: manual en el navegador; sin tests (§11).

**Project Type**: frontend. Toca `cargamasiva_front/src/{models/Carga.ts, api/cargas.ts, hooks/useHistorialCargas.ts, pages/HistorialPage.tsx}`.

**Constraints**: sin paginación, filtros, ordenación ni WebSockets.

## Constitution Check

Constitución v3.8.1.

| Principio | Verificación | Resultado |
|---|---|---|
| §1 Alcance | Solo la pantalla 2 y el *polling* del README | Pasa |
| §5 Contrato | `Response<T>` y `ApiError`; DTO según F010 | Pasa |
| §9 Rate Limiting | Las consultas no lo consumen (F010) | Pasa |
| §11 Cliente web | `models`, `api`, `hooks` (el hook coordina estado, consultas y efectos) y `pages`; solo el Gateway | Pasa |
| §12 Dependencias | Ninguna nueva | Pasa |

## Project Structure

```text
cargamasiva_front/src/
├── models/Carga.ts                 # + CargaResumenDto
├── api/cargas.ts                   # + listarCargas()
├── hooks/useHistorialCargas.ts     # polling con visibilidad (research D-02)
└── pages/HistorialPage.tsx         # tabla funcional
```

## Complexity Tracking

| Elemento | Por qué | Alternativa descartada |
|---|---|---|
| Hook `useHistorialCargas` | El *polling* coordina estado, consulta y efectos (temporizador y visibilidad) | Lógica dentro de la página (mezcla presentación y efectos) |
