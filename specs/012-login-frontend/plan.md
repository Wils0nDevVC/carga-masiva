# Implementation Plan: Pantalla de Login en el cliente web

**Branch**: `012-login-frontend` (trabajo en `main`) | **Date**: 2026-11-03 | **Spec**: [spec.md](spec.md)

## Summary

`LoginPage` deja de ser provisional:
- formulario controlado con estado local que llama a `useAuth().iniciarSesion`;
- muestra `ApiError.message` y `errors`;
- deshabilita el botón durante el envío y vacía la contraseña tras un error;
- navega a `state.desde` o a `/historial`, y si ya hay sesión redirige a `/historial`.

`RutaProtegida` pasa la ruta solicitada en `state.desde`. Sin dependencias, modelos ni hooks nuevos. [Spec] [research D-01 a D-04]

## Technical Context

**Language/Version**: TypeScript 7.0.2 y React 19.3.1 (Feature 011).

**Primary Dependencies**: las de la 011 (React Router 8.5.0 y Tailwind 4.3.3); ninguna nueva.

**Storage**: `sessionStorage` (vía `AuthContext`, F011).

**Testing**: manual en el navegador (`quickstart.md`); sin tests (§11).

**Target Platform**: navegador; `cargamasiva_front` en Compose.

**Project Type**: frontend. Toca solo `cargamasiva_front/src/pages/LoginPage.tsx` y `cargamasiva_front/src/routes/RutaProtegida.tsx`.

**Constraints**: sin registro, recuperación de contraseña ni bibliotecas de formularios.

## Constitution Check

Constitución v3.8.1.

| Principio | Verificación | Resultado |
|---|---|---|
| §1 Alcance | Solo la pantalla 0 del README; sin extras | Pasa |
| §4 Validaciones | Solo `required` y `type=email`; las reglas las valida el servidor | Pasa |
| §5 Contrato | Mensajes tomados de `Response<T>` a través de `ApiError` | Pasa |
| §10 Logs | Sin contraseñas ni tokens en la consola | Pasa |
| §11 Cliente web | Página en `pages`; usa el contexto y el cliente HTTP de la 011; sin hook nuevo (el estado es de una sola página); solo el Gateway | Pasa |
| §12 Dependencias | Ninguna nueva | Pasa |

## Project Structure

```text
specs/012-login-frontend/
├── plan.md, research.md, data-model.md, quickstart.md
├── contracts/login-ui.md
└── checklists/requirements.md

cargamasiva_front/src/
├── pages/LoginPage.tsx          # formulario funcional (sustituye al provisional)
└── routes/RutaProtegida.tsx     # Navigate con state.desde
```

## Complexity Tracking

Sin complejidad añadida.
