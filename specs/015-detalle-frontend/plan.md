# Implementation Plan: Pantalla de Detalle de una carga

**Branch**: `015-detalle-frontend` (trabajo en `main`) | **Date**: 2026-11-03 | **Spec**: [spec.md](spec.md)

## Summary

- **Modelos** (`models/Carga.ts`): `CargaDetalleDto`, `ContenidoArchivoDto` y sus tipos auxiliares.
- **API** (`api/cargas.ts`): `obtenerDetalleCarga(id)` y `obtenerContenidoCarga(id)`.
- **Hook** `useDetalleCarga(id)`: *polling* de 5 s con el patrón de la 014, que se detiene en Notificado, Fallido o 404.
- **`DetallePage`**:
  - muestra los datos, el historial, los periodos y las incidencias;
  - carga el contenido bajo demanda, con estado local y reintento;
  - un identificador no válido se trata como inexistente.
- **Componentes** `EstadoCarga` y `FechaLocal`, compartidos con el Historial, que pasa a usarlos.
- Sin dependencias.

[Spec] [research D-01 a D-04]

## Technical Context

**Language/Version**: TypeScript 7.0.2 y React 19.3.1.

**Primary Dependencies**: las de la 011; ninguna nueva.

**Storage**: ninguno.

**Testing**: manual en el navegador; sin tests (§11).

**Project Type**: frontend. Toca `cargamasiva_front/src/{models/Carga.ts, api/cargas.ts, hooks/useDetalleCarga.ts, components/{EstadoCarga,FechaLocal}.tsx, pages/{DetallePage,HistorialPage}.tsx}`.

**Constraints**: sin paginación, filtros ni descarga.

## Constitution Check

Constitución v3.8.1.

| Principio | Verificación | Resultado |
|---|---|---|
| §1 Alcance | Pantalla 3 y contenido del README; sin extras | Pasa |
| §5 Contrato | DTO y errores según F010; los errores del contenido no se presentan como vacío | Pasa |
| §11 Cliente web | `models`, `api`, `hooks` (el *polling* coordina estado, consultas y efectos), `components` y `pages`; solo el Gateway | Pasa |
| §12 Dependencias | Ninguna nueva; sin abstracción de *polling* anticipada | Pasa |

## Project Structure

```text
cargamasiva_front/src/
├── models/Carga.ts                    # + tipos del detalle y del contenido
├── api/cargas.ts                      # + obtenerDetalleCarga, obtenerContenidoCarga
├── hooks/useDetalleCarga.ts           # polling hasta el estado terminal o 404
├── components/EstadoCarga.tsx         # insignia de estado (compartida)
├── components/FechaLocal.tsx          # fecha local (compartida)
└── pages/DetallePage.tsx, HistorialPage.tsx
```

## Complexity Tracking

| Elemento | Por qué | Alternativa descartada |
|---|---|---|
| Hook `useDetalleCarga` separado del de historial | Condiciones de parada distintas (estado terminal o 404) | Hook genérico de *polling* (abstracción anticipada para dos usos) |
| `EstadoCarga` y `FechaLocal` | Se usan en dos pantallas | Duplicar el formato en cada página |
