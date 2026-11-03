# Implementation Plan: Pantalla de Subida de Excel

**Branch**: `013-subida-excel-frontend` (trabajo en `main`) | **Date**: 2026-11-03 | **Spec**: [spec.md](spec.md)

## Summary

`SubirPage` deja de ser provisional:
- selector de un único `.xlsx` (requerido);
- `subirCarga(archivo)` en `api/cargas.ts` (`POST /cargas` multipart, con el cliente común de la 011);
- con 202, muestra el resultado con un enlace al detalle; con error, el `message` y los `errors` del `ApiError`;
- botón deshabilitado durante el envío.

Solo hay un modelo nuevo, `CargaRegistradaDto`. Sin dependencias ni hooks nuevos. [Spec] [research D-01 a D-03]

## Technical Context

**Language/Version**: TypeScript 7.0.2 y React 19.3.1 (Feature 011).

**Primary Dependencies**: las de la 011; ninguna nueva.

**Storage**: ninguno.

**Testing**: manual en el navegador; sin tests (§11).

**Project Type**: frontend. Toca `cargamasiva_front/src/{models/Carga.ts, api/cargas.ts, pages/SubirPage.tsx}`.

**Constraints**: sin validaciones de tamaño o nombre en el cliente, sin progreso de subida, sin arrastrar y soltar ni subidas múltiples.

## Constitution Check

Constitución v3.8.1.

| Principio | Verificación | Resultado |
|---|---|---|
| §1 Alcance | Solo la pantalla 1 del README | Pasa |
| §4 Validaciones | Solo `accept` y `required`; el resto lo valida Control | Pasa |
| §5 Contrato | `Response<T>` y `ApiError`; 202 según F005 | Pasa |
| §11 Cliente web | `models` (contrato), `api` (llamada) y `pages`; sin hook (estado de una sola página); solo el Gateway por el mismo origen | Pasa |
| §12 Dependencias | Ninguna nueva | Pasa |

## Project Structure

```text
cargamasiva_front/src/
├── models/Carga.ts        # CargaRegistradaDto
├── api/cargas.ts          # subirCarga(archivo)
└── pages/SubirPage.tsx    # formulario funcional
```

## Complexity Tracking

Sin complejidad añadida.
