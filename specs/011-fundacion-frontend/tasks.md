# Tasks: Fundación del frontend React

**Input**: `specs/011-fundacion-frontend/` (plan.md, spec.md, research.md, data-model.md, contracts/frontend.md, quickstart.md)

**Alcance**:
- Se tocan `cargamasiva_front/` (nuevo), `compose.yaml` (servicio `frontend`) y `.gitignore`.
- No se tocan los servicios .NET, el Gateway, `.env`, `postman/`, `referencias/` ni la constitución.
- No hay pruebas automatizadas (constitución §11).

**Reglas de ejecución**
- **Dependencias**: solo las de research D-01, con versiones **exactas**: `react`/`react-dom` 19.3.1, `react-router` 8.5.0, `axios` 1.20.0, `tailwindcss`/`@tailwindcss/vite` 4.3.3, `vite` 8.3.4, `@vitejs/plugin-react` 6.1.2, `typescript` 7.0.2 y `@types/react`/`@types/react-dom` 19.3.0.
  - Sin ESLint, Prettier, bibliotecas de componentes, de estado o de pruebas.
  - No se usa `create-vite` para evitar dependencias extra; los archivos se crean a mano.
- **Archivos**: no se crean archivos, hooks ni modelos sin uso, ni respuestas simuladas (FR-002).
- **Datos de prueba**: las pruebas con backend usan `CargaMasivaApp_F011`, con toda la pila apuntando a esa base. Nunca se usa `docker compose down -v`; no se tocan `CargaMasiva` ni `CargaMasivaDb`.
- **Secretos**: los tokens se obtienen del Gateway y no se imprimen.
- **Marcado**: `[x]` solo para lo ejecutado y verificado.
- **Cierre**: entregar el comando de commit en español, sin `Co-Authored-By`.

## Phase 1: Setup

- [x] T001 Crear `cargamasiva_front/package.json`:
  - `"private": true`, `"type": "module"`.
  - Scripts `dev` (`vite`), `build` (`tsc -b && vite build`) y `preview`.
  - `dependencies` y `devDependencies` con las versiones exactas de las Reglas.
  - Ejecutar `npm install` en `cargamasiva_front/` para generar `cargamasiva_front/package-lock.json`, que se versiona.
- [x] T002 [P] Crear `cargamasiva_front/tsconfig.json` (referencias), `cargamasiva_front/tsconfig.app.json` (`strict`, `jsx: react-jsx`, `moduleResolution: bundler`, `noEmit`, `include: ["src"]`) y `cargamasiva_front/tsconfig.node.json` (para `vite.config.ts`). Abierto 1: ajustar las opciones que TypeScript 7 no admita.
- [x] T003 [P] Crear `cargamasiva_front/vite.config.ts` (research D-04):
  - Plugins `react()` y `tailwindcss()`.
  - `server.port` 5173.
  - `server.proxy` con `/auth` y `/cargas` hacia `process.env.GATEWAY_URL ?? 'http://localhost:8080'`, con `changeOrigin: true`.
  - Mensaje en consola con el destino del proxy al arrancar; nunca muestra tokens.
- [x] T004 [P] Crear `cargamasiva_front/index.html` (contenedor `#root` y `src/main.tsx`) y `cargamasiva_front/src/index.css` (`@import "tailwindcss";`).
- [x] T005 [P] Añadir `cargamasiva_front/node_modules/` y `cargamasiva_front/dist/` a `.gitignore` y crear `cargamasiva_front/.dockerignore` (`node_modules`, `dist`).

## Phase 2: Foundational (bloquea todas las historias)

- [x] T006 [P] Crear los modelos en `cargamasiva_front/src/models/` (data-model.md):
  - `Response.ts`: `Response<T> { data: T | null; isSuccess: boolean; message: string | null; code: number; errors: string[] | null }`.
  - `Auth.ts`: `TokensDto { accessToken: string; expiraEn: string; refreshToken: string; refreshExpiraEn: string }`, `LoginRequest { email: string; password: string }` y `RefreshRequest { refreshToken: string }`.
  - `ApiError.ts`: `ApiError { code: number; message: string; errors: string[] }`.
- [x] T007 [P] Crear `cargamasiva_front/src/api/sesion.ts` (research D-05):
  - `leerSesion(): TokensDto | null`, `guardarSesion(t: TokensDto)` y `limpiarSesion()`.
  - Una sola clave `sessionStorage` `carga-masiva.sesion` con el JSON de `TokensDto`.
  - Cada acceso va en `try/catch` (si falla, se trata como sin sesión).
  - Sin otros almacenes.
- [x] T008 Crear `cargamasiva_front/src/api/cliente.ts` (research D-06), sin interceptores todavía:
  - Instancia `axios.create({ baseURL: '' })`.
  - `aApiError(error)`: convierte un error de Axios a `ApiError`. Con un `Response<T>` en el cuerpo, usa `code`, `message` y `errors ?? []`; si no, `code` = estado HTTP o 0 y `message` «No se pudo conectar con el servidor».
  - `desempaquetar<T>(promesa)`: devuelve `data` o lanza `ApiError`.
- [x] T009 Crear `cargamasiva_front/src/api/auth.ts`:
  - `login(req: LoginRequest)` hace `POST /auth/login`, y `refresh(req: RefreshRequest)` hace `POST /auth/refresh`; ambas devuelven `TokensDto` con `desempaquetar`.
  - Exportar las rutas `RUTA_LOGIN` y `RUTA_REFRESH` para la exclusión de los interceptores.

**Checkpoint**: `npm run build` compila (con un `main.tsx` mínimo, que se completa en US1).

## Phase 3: US1 - Arrancar el cliente y navegar (P1) 🎯 MVP

**Objetivo**: la SPA compila, arranca y navega entre las páginas provisionales; Compose la sirve con recarga en rutas internas.

**Prueba independiente**: quickstart, escenarios 1, 2 (sin la parte de redirección) y 11.

- [x] T010 [P] [US1] Crear las páginas provisionales en `cargamasiva_front/src/pages/`, cada una con un título y un texto «Pendiente de implementar en su feature», sin lógica ni llamadas:
  - `LoginPage.tsx`.
  - `SubirPage.tsx` (Subida de Excel).
  - `HistorialPage.tsx`.
  - `DetallePage.tsx`, que muestra el `idCarga` de `useParams`.
  - `NoEncontradaPage.tsx`.
- [x] T011 [P] [US1] Crear `cargamasiva_front/src/components/Layout.tsx`: cabecera con el nombre de la aplicación, enlaces a `/subir` y `/historial` (y `/login`), y `<Outlet />`. Solo clases de Tailwind.
- [x] T012 [US1] Crear `cargamasiva_front/src/routes/AppRoutes.tsx` con React Router (research D-02):
  - Dentro del `Layout`: `/login`, `/subir`, `/historial` y `/historial/:idCarga`.
  - `/` redirige a `/historial` y `*` muestra `NoEncontradaPage`.
  - Ninguna ruta empieza por `/cargas` ni por `/auth`.
- [x] T013 [US1] Crear `cargamasiva_front/src/main.tsx`: `createRoot`, `BrowserRouter` y `AppRoutes`, e importar `index.css`.
- [x] T014 [P] [US1] Crear `cargamasiva_front/nginx/default.conf.template` (research D-02, D-03):
  - `listen 80`, `root /usr/share/nginx/html` y `client_max_body_size 12m`.
  - `location /auth/` y `location = /cargas` con `location /cargas/`: `proxy_pass ${GATEWAY_URL}`, conservando ruta, método, cuerpo y cabeceras.
  - `location /`: `try_files $uri $uri/ /index.html`.
- [x] T015 [P] [US1] Crear `cargamasiva_front/Dockerfile`:
  - Etapa `node:24.22.0-alpine`: `npm ci` y `npm run build`.
  - Etapa `nginx:1.30.6-alpine`: copia `dist/` a `/usr/share/nginx/html` y la plantilla a `/etc/nginx/templates/default.conf.template`.
- [x] T016 [US1] Añadir el servicio `frontend` a `compose.yaml` (research D-03):
  - build `./frontend`, imagen `carga-masiva-frontend:local`.
  - `environment: GATEWAY_URL: http://api-gateway:8080`.
  - `depends_on: api-gateway: condition: service_healthy`.
  - `ports: "3000:80"`.
  - healthcheck `["CMD", "wget", "-q", "-O", "/dev/null", "http://127.0.0.1/"]` (intervalo 5 s, 30 reintentos) y `restart: "no"`.
  - Comprobar con `docker compose config -q`.
- [ ] T017 [US1] Verificar los escenarios 1, 2 (navegación y página "no encontrada") y 11 del quickstart, y registrarlos en `specs/011-fundacion-frontend/quickstart.md`.

## Phase 4: US2 - Sesión y rutas protegidas (P1)

**Objetivo**: sin sesión, las rutas protegidas redirigen a `/login`; la sesión persiste en `sessionStorage` y se cierra limpiándola.

**Prueba independiente**: quickstart, escenarios 2 (redirecciones), 3 y 4.

- [x] T018 [US2] Crear `cargamasiva_front/src/context/AuthContext.tsx` (research D-05):
  - Estado `tokens`, iniciado con `leerSesion()`.
  - `estaAutenticado = tokens !== null`.
  - `iniciarSesion(email, password)`: `auth.login`, `guardarSesion` y actualización del estado.
  - `cerrarSesion()`: `limpiarSesion`, estado null y navegación a `/login`.
  - Exportar `AuthProvider`. No decodifica el JWT.
- [x] T019 [P] [US2] Crear `cargamasiva_front/src/hooks/useAuth.ts`: devuelve el contexto y lanza un error si se usa fuera de `AuthProvider`.
- [x] T020 [US2] Crear `cargamasiva_front/src/routes/RutaProtegida.tsx`: si `!estaAutenticado`, redirige a `/login` (`Navigate replace`); si no, `<Outlet />`. En `AppRoutes.tsx`, envolver `/subir`, `/historial` y `/historial/:idCarga`.
- [x] T021 [US2] Integrar la sesión en la interfaz:
  - En `main.tsx`, `AuthProvider` dentro de `BrowserRouter`.
  - En `Layout.tsx`, el botón «Cerrar sesión» (con `useAuth().cerrarSesion`) si hay sesión, y el enlace «Iniciar sesión» si no.
- [ ] T022 [US2] Verificar los escenarios 2 (redirecciones), 3 y 4 del quickstart con tokens reales del Gateway en `sessionStorage`, y registrarlos.

## Phase 5: US3 - Infraestructura HTTP: Response<T>, token y renovación (P1)

**Objetivo**: Bearer en las peticiones protegidas, una renovación compartida con un reintento, exclusión de login y refresh, limpieza de la sesión y `/login` si la renovación falla.

**Prueba independiente**: quickstart, escenarios 5 a 10, desde la consola en `npm run dev` (research D-08).

- [x] T023 [US3] Añadir a `cargamasiva_front/src/api/cliente.ts` el interceptor de **petición**: si `config.url` no es `RUTA_LOGIN` ni `RUTA_REFRESH` y `leerSesion()` tiene token, añade `Authorization: Bearer <accessToken>`.
- [x] T024 [US3] Añadir a `cargamasiva_front/src/api/cliente.ts` el interceptor de **respuesta** (research D-06):
  1. Si el estado es 401, la URL no es de login ni de refresh, `!config._reintentada` y hay `refreshToken`:
     - marcar `_reintentada`;
     - obtener o crear la promesa compartida `renovacionEnCurso = auth.refresh({ refreshToken }).then(guardarSesion).finally(() => renovacionEnCurso = null)`;
     - tras resolverse, reintentar `cliente(config)` una vez con el token nuevo.
  2. Si la renovación falla: `limpiarSesion()`, `window.location.assign('/login')` y rechazar con `ApiError`.
  3. En cualquier otro caso: rechazar con `aApiError(error)`.
  - La llamada de refresh no pasa por la renovación: queda excluida por su ruta (T023 y T024).
  - Sin `console.log` de tokens.
- [x] T025 [US3] Verificar los escenarios 5 a 10 del quickstart sobre `CargaMasivaApp_F011`:
  - Vigencias cortas solo en el entorno del comando.
  - Pestaña de red: número de llamadas a `/auth/refresh` y de reintentos.
  - Registrar los resultados, incluido el Abierto 2.

## Phase 6: Polish

- [ ] T026 Escenario 12: revisar que la consola del navegador no muestre tokens ni contraseñas durante las verificaciones, y que el código no tenga `console.log` con tokens (`grep -rn "console\." cargamasiva_front/src`).
- [x] T027 Cierre:
  - Eliminar `CargaMasivaApp_F011`.
  - Arranque normal sobre `CargaMasivaApp` con `frontend` incluido, todo `healthy`.
  - Completar "Resultados" en `quickstart.md`, separando lo ejecutado de lo no ejecutado.
  - Entregar el comando de commit.

## Estado de la implementación (2026-11-03)

- **24 de 27 tareas hechas.** `npm run build` termina con 0 errores (TypeScript 7.0.2 y Vite 8.3.4; Abierto 1 resuelto).
- **Sin marcar** (requieren un navegador, no disponible en la sesión de implementación):
  - **T017**: la navegación visual del escenario 2.
  - **T022**: las redirecciones y el cierre de sesión en la interfaz (escenarios 2 a 4).
  - **T026**: la consola del navegador.
  - Lo verificable sin navegador está hecho; ver `quickstart.md`.
- **Ajustes respecto a las tareas**:
  1. Se eliminó `tsconfig.node.json`: `vite.config.ts` usa `process.env` y comprobar sus tipos exigiría `@types/node`, que no está autorizado. Vite transpila la configuración sin comprobar tipos; `tsc -b` comprueba solo `src`.
  2. `cliente.ts` define su propia lista de rutas de autenticación en lugar de importar `RUTA_LOGIN` y `RUTA_REFRESH` de `auth.ts`, para evitar una importación circular (`auth.ts` importa `cliente.ts`).
  3. El interceptor de respuesta, si la petición salió con un token de acceso que otra petición ya renovó, reintenta sin una segunda renovación. Así se garantiza un solo refresh cuando los 401 llegan escalonados (SC-003).

## Dependencias y orden

- Setup (T001 a T005) → Foundational (T006 a T009) → US1 (T010 a T017) → US2 (T018 a T022) → US3 (T023 a T025) → Polish (T026 y T027).
- T008 depende de T006; T009 de T008. T018 depende de T007 y T009.
- Los archivos compartidos van en secuencia:
  - `AppRoutes.tsx`: T012 y luego T020.
  - `main.tsx`: T013 y luego T021.
  - `Layout.tsx`: T011 y luego T021.
  - `cliente.ts`: T008, luego T023 y luego T024.
- US3 se puede verificar sin US2, porque usa `sessionStorage` directamente. Se implementa después para reutilizar `auth.ts` y `sesion.ts`.

## Paralelismo

- Setup: T002, T003, T004 y T005 en paralelo, después de T001.
- Foundational: T006 y T007 en paralelo.
- US1: T010 y T011 en paralelo, y T014 y T015 en paralelo.
- US2: T019 en paralelo con T018.

## Estrategia

- **MVP**: US1, la SPA compilable y navegable, servida por Compose desde el mismo origen.
- **US2**: añade la sesión y la protección de rutas.
- **US3**: completa la infraestructura HTTP (renovación y reintento) que necesitarán las pantallas funcionales.
