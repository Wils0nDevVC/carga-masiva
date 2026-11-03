# Tasks: Pantalla de Login en el cliente web

**Input**: `specs/012-login-frontend/` (plan.md, spec.md, research.md, contracts/login-ui.md, quickstart.md)

**Alcance**: se tocan solo `cargamasiva_front/src/routes/RutaProtegida.tsx` y `cargamasiva_front/src/pages/LoginPage.tsx`.

**Reglas**:
- Sin dependencias, modelos ni hooks nuevos, y sin pruebas automatizadas.
- `[x]` solo para lo ejecutado y verificado.
- Al cerrar, entregar el comando de commit en español, sin `Co-Authored-By`.

## Phase 1: US1 - Iniciar sesión (P1) 🎯 MVP

**Objetivo**: un formulario de login funcional con retorno a la ruta de origen y mensajes de `Response<T>`.

**Prueba independiente**: los escenarios 1 a 7 del quickstart.

- [x] T001 [P] [US1] En `cargamasiva_front/src/routes/RutaProtegida.tsx`, redirigir sin sesión con `<Navigate to="/login" replace state={{ desde: location.pathname + location.search }} />`, usando `useLocation()` (research D-02).
- [x] T002 [P] [US1] Reescribir `cargamasiva_front/src/pages/LoginPage.tsx` según research D-02 a D-04 y `contracts/login-ui.md`:
  - **Con sesión activa**: si `useAuth().estaAutenticado`, devolver `<Navigate to="/historial" replace />`.
  - **Estado**: `email`, `password`, `enviando` y `error: ApiError | null`.
  - **Formulario**:
    - correo (`type="email"`, `required`, `autoComplete="username"`);
    - contraseña (`type="password"`, `required`, `autoComplete="current-password"`);
    - botón «Ingresar», que muestra «Ingresando…» y queda `disabled` mientras `enviando`.
  - **Envío** (`onSubmit`): `preventDefault`; si ya se está enviando, no hacer nada; `await iniciarSesion(email, password)`.
    - **Si va bien**: navegar a `state.desde` (solo si empieza por `/` y no es `/login`) o a `/historial`, con `replace`.
    - **Si falla**: `setError(error as ApiError)` y `setPassword('')`.
    - **Al terminar**: `enviando = false`.
  - **Error**: mostrar `error.message` y, si `error.errors` no está vacío, la lista.
  - **Estilos**: Tailwind; formulario centrado.
  - Sin `console`.
- [x] T003 [US1] `npm run build` en `cargamasiva_front/` con 0 errores. Reconstruir y levantar `frontend` con `docker compose up -d --build frontend`.
- [ ] T004 [US1] Verificar los escenarios 1 a 7 del quickstart en el navegador (`http://localhost:3000`) y registrarlos en `specs/012-login-frontend/quickstart.md`.

## Estado (2026-11-03)

- **T001–T003 hechas**: `npm run build` con 0 errores; 0 usos de `console` en `src`; `frontend` reconstruido, `healthy`; `GET /login` → 200.
- **T004 pendiente**: requiere un navegador, no disponible en la sesión de implementación. Se verifica en `http://localhost:3000` con los escenarios del quickstart.

## Dependencias y paralelismo

- T001 y T002 en paralelo (archivos distintos) → T003 → T004.

## Estrategia

Una sola historia: es en sí el MVP.
