# Implementation Plan: Fundación del frontend React

**Branch**: `011-fundacion-frontend` (trabajo en `main`) | **Date**: 2026-11-03 | **Spec**: [spec.md](spec.md)

**Input**: `specs/011-fundacion-frontend/spec.md`

**Procedencia**: `Dec` = decisión del usuario o constitución v3.8.0; `Spec`; `F005/F008/F009/F010`; `Plan` = decisión técnica (detalle en `research.md`); `Verif` = comprobado en esta etapa.

## Summary

SPA en `frontend/` con React 19, TypeScript 7, Vite 8, React Router 8, Axios 1.20 y Tailwind CSS 4.

- **Páginas**: provisionales en `/login`, `/subir`, `/historial` y `/historial/:idCarga`, con un layout común y una página "no encontrada". Las rutas protegidas exigen sesión.
- **Sesión**: `AuthContext` y `useAuth`, con los tokens de la 008 en `sessionStorage`.
- **HTTP**: una instancia común de Axios con `baseURL` vacía (mismo origen) que:
  - desempaqueta `Response<T>` y normaliza `ApiError`;
  - adjunta el Bearer, salvo en login y refresh;
  - hace una renovación compartida con un único reintento;
  - si la renovación falla, limpia la sesión y lleva a `/login`.
- **Despliegue**: nginx sirve la SPA y reenvía `/auth` y `/cargas` al Gateway (`GATEWAY_URL`); en desarrollo lo hace el proxy de Vite. Servicio `frontend` en Compose (puerto 3000).
- Sin pantallas funcionales.

[Spec] [Dec Q1–Q3] [Plan]

## Technical Context

**Language/Version**: TypeScript 7.0.2 sobre Node 24.22.0 (compilación). La salida es estática (HTML, JS y CSS).

**Primary Dependencies**: `react`/`react-dom` 19.3.1, `react-router` 8.5.0, `axios` 1.20.0 y `tailwindcss`/`@tailwindcss/vite` 4.3.3. Desarrollo: `vite` 8.3.4, `@vitejs/plugin-react` 6.1.2, `typescript` 7.0.2 y `@types/react`/`@types/react-dom` 19.3.0. Todas con licencia MIT o Apache-2.0 y publicadas hace más de dos semanas. [Verif]

**Storage**: `sessionStorage` (clave `carga-masiva.sesion`).

**Testing**: manual, con el navegador (`quickstart.md`). No hay tests automatizados (§11).

**Target Platform**: navegadores modernos; contenedor `nginx:1.30.6-alpine` en Compose.

**Project Type**: aplicación web (frontend). Toca `frontend/` (nuevo) y `compose.yaml` (servicio `frontend`). El backend, los contratos y `.env` no cambian.

**Constraints**:
- Sin Redux, Zustand, React Query, Next.js, bibliotecas de componentes, ESLint, Prettier, pruebas, repositorios ni clases base.
- Sin CORS en el Gateway.
- Sin pantallas funcionales ni hooks de funcionalidades.

## Constitution Check

Constitución v3.8.0.

| Principio | Verificación | Resultado |
|---|---|---|
| §1 Alcance | Solo la fundación de la spec; sin formularios, tablas, subida ni *polling*. La referencia GitHub no aplica (es backend) | Pasa |
| §2 Organización | `frontend/` en la raíz (3.8.0) | Pasa |
| §5 Contrato | `Response<T>` y `ApiError` derivado sin campos nuevos | Pasa |
| §9 Gateway | Solo se consume el Gateway; sin CORS; mismo origen por reenvío | Pasa |
| §10 Logs | Sin tokens ni contraseñas en la consola | Pasa |
| §11 Cliente web | React, TypeScript, Vite, React Router, Axios y Tailwind sin biblioteca de componentes; pages, components, models, api, context, hooks y routes; `sessionStorage`; mismo origen; Dockerfile y Compose; Postman se conserva; sin tests | Pasa |
| §12 Dependencias | Solo las autorizadas, con versión exacta y licencia verificadas | Pasa |

Tras la Phase 1: sin violaciones.

## Project Structure

```text
specs/011-fundacion-frontend/
├── plan.md, research.md, data-model.md, quickstart.md
├── contracts/frontend.md
├── checklists/requirements.md
└── tasks.md            # /speckit-tasks (no creado aquí)

compose.yaml                         # + servicio frontend (build ./frontend, GATEWAY_URL, 3000:80, depends_on api-gateway healthy)

frontend/
├── Dockerfile                       # node:24.22.0-alpine (npm ci, build) → nginx:1.30.6-alpine
├── nginx/default.conf.template      # SPA (try_files) + proxy /auth/ y /cargas → ${GATEWAY_URL}; client_max_body_size 12m
├── .dockerignore                    # node_modules, dist
├── package.json, package-lock.json  # versiones exactas (research D-01)
├── tsconfig.json, tsconfig.app.json, tsconfig.node.json
├── vite.config.ts                   # react(), tailwindcss(), server.proxy /auth y /cargas → GATEWAY_URL (por defecto http://localhost:8080)
├── index.html
└── src/
    ├── main.tsx                     # BrowserRouter + AuthProvider + AppRoutes
    ├── index.css                    # @import "tailwindcss";
    ├── models/{Response, Auth, ApiError}.ts
    ├── api/
    │   ├── cliente.ts               # instancia axios, interceptores, desempaquetar<T>, normalización de ApiError
    │   ├── sesion.ts                # leer, guardar y limpiar en sessionStorage
    │   └── auth.ts                  # login(), refresh()
    ├── context/AuthContext.tsx      # estaAutenticado, iniciarSesion, cerrarSesion
    ├── hooks/useAuth.ts
    ├── routes/{AppRoutes, RutaProtegida}.tsx
    ├── components/Layout.tsx
    └── pages/{LoginPage, SubirPage, HistorialPage, DetallePage, NoEncontradaPage}.tsx
```

El `.gitignore` de la raíz no ignora `node_modules/` ni `dist/` [Verif]; en Setup se añaden `frontend/node_modules/` y `frontend/dist/`.

## Complexity Tracking

| Elemento | Por qué | Alternativa descartada |
|---|---|---|
| nginx en la imagen del cliente | Servir la SPA con recarga en rutas internas y reenviar al Gateway desde el mismo origen (Q2) sin tocar el Gateway | CORS en el Gateway (descartado en Q2); servidor de desarrollo de Vite en producción |
| Plantilla nginx con `GATEWAY_URL` | URL del Gateway configurable sin recompilar (FR-003) | Variable de compilación de Vite (obliga a recompilar y la SPA no la necesita con mismo origen) |
| Rutas de la SPA sin `/cargas` ni `/auth` | Esos prefijos se reenvían al Gateway; al recargar se pediría la API | Prefijo `/app` para la SPA (más complejo y sin requisito) |
| `window.location.assign('/login')` tras fallar la renovación | Desacopla el cliente HTTP del router; el contexto se reinicia vacío | Callback o evento entre Axios y el contexto (más código) |

## Pendientes tras el plan

Ninguno que requiera decisión.

- **Abiertos 1 y 2 de `research.md`**: TypeScript 7 con la plantilla de Vite y la carga de módulos desde la consola. Se verifican en la implementación.
- **Validación en el equipo de evaluación**: no realizada.
