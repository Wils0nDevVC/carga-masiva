# Research: Fundación del frontend React

**Procedencia**: `Dec` = decisión del usuario o constitución v3.8.0; `Spec` = spec 011; `F005/F008/F009/F010` = features anteriores; `Plan` = decisión técnica de este plan (alternativa más sencilla); `Verif` = comprobado en esta etapa; `Sin verif` = se comprueba en la implementación.

## D-01 Versiones (todas publicadas hace más de dos semanas; npm registry, Docker Hub y nginx.org consultados el 2026-11-03) [Verif]

| Paquete / imagen | Versión | Licencia | Nota |
|---|---|---|---|
| `react`, `react-dom` | 19.3.1 (2026-10-14) | MIT | |
| `react-router` | 8.5.0 (2026-10-19) | MIT | peer `react >= 19.2.7`; Node ≥ 22.22 |
| `axios` | 1.20.0 (2026-08-26) | MIT | 1.21.0 tiene menos de dos semanas |
| `vite` | 8.3.4 (2026-10-08) | MIT | Node `^20.19 \|\| >= 22.12` |
| `@vitejs/plugin-react` | 6.1.2 (2026-10-05) | MIT | peer `vite ^8` |
| `tailwindcss`, `@tailwindcss/vite` | 4.3.3 (2026-07-16) | MIT | peer `vite ^8` admitido |
| `typescript` | 7.0.2 (2026-07-08) | Apache-2.0 | *Sin verif*: compatibilidad de `tsc -b` con la configuración de Vite 8 |
| `@types/react`, `@types/react-dom` | 19.3.0 (2026-09-09) | MIT | |
| Imagen de compilación | `node:24.22.0-alpine` (Node 24.22.0, LTS, 2026-10-14) | MIT | |
| Imagen de ejecución | `nginx:1.30.6-alpine` (nginx 1.30.6 estable, 2026-10-14; imagen multiarquitectura con amd64 y arm64) | BSD-2-Clause | |

- **Versiones fijadas**: exactas en `package.json`, con `package-lock.json` versionado y `npm ci` en la imagen.
- **Sin otras dependencias**: no se añaden ESLint, Prettier, ni bibliotecas de pruebas, componentes o estado. [Dec §11, §12] [Spec FR-001]

## D-02 Rutas de la SPA frente a las rutas reenviadas [Spec FR-005a, FR-006] [Plan]

- Con el mismo origen, el servidor del cliente reenvía al Gateway **exactamente** `/auth/…` y `/cargas` o `/cargas/…`. Por eso las rutas de la SPA **no** pueden empezar por esos prefijos: al recargar en `/cargas` se pediría la API.
- **Rutas de la SPA**:
  - `/login`: pública.
  - `/subir`, `/historial` e `/historial/:idCarga` (Detalle): protegidas.
  - `/`: redirige a `/historial`.
  - `*`: página "no encontrada".
- **Reglas del servidor**:
  - `location /auth/` y `location = /cargas` con `location /cargas/`: se reenvían al Gateway.
  - Lo demás: archivos estáticos y, si no existen, `index.html` (`try_files`), para que la recarga en una ruta interna funcione (US1-4).

## D-03 Servir y reenviar en Compose [Dec Q2, Q3] [Spec FR-005, FR-005a]

- `frontend/Dockerfile` en dos etapas: `npm ci` y `npm run build` en Node, y después `nginx` con `dist/` y una plantilla de configuración.
- **URL del Gateway**: la imagen oficial de nginx procesa `/etc/nginx/templates/*.template` con `envsubst` al arrancar. `default.conf.template` usa `${GATEWAY_URL}` en los `proxy_pass`. La URL del Gateway es, por tanto, la variable de entorno `GATEWAY_URL` del contenedor, sin recompilar (FR-003).
- **Reenvío sin cambios**: rutas, métodos, cuerpos y cabeceras, incluida `Authorization` (comportamiento por defecto de `proxy_pass`).
- **`client_max_body_size 12m`**: el valor por defecto de nginx (1 MiB) rompería la subida de la futura pantalla. 12 MiB cubre el límite de Control (10 MiB más 1 MiB de margen multipart, F005), sin imponer un límite menor. [F005]
- **Compose**: servicio `frontend`:
  - build `./frontend`, imagen `carga-masiva-frontend:local`;
  - `GATEWAY_URL: http://api-gateway:8080`;
  - `depends_on` `api-gateway: service_healthy`;
  - puertos `3000:80`, comprobados libres en este equipo; 8080–8084, 8888, 8025, 15672 y 14333 ya están ocupados;
  - healthcheck `wget -q -O /dev/null http://127.0.0.1/`, con `restart: "no"`.
  - Sin volúmenes ni variables nuevas en `.env`.

## D-04 Desarrollo con Vite [Dec Q2]

- `vite.config.ts` con `server.proxy` para `/auth` y `/cargas` hacia `process.env.GATEWAY_URL`, por defecto `http://localhost:8080` (el Gateway de Compose publicado). Puerto de desarrollo: 5173.
- La SPA usa `baseURL` vacía (mismo origen) en desarrollo y en producción: el código nunca contiene la URL del Gateway.
- **Configuración ausente** (Spec Edge Cases):
  - En Compose, `GATEWAY_URL` tiene un valor fijo en `compose.yaml`.
  - En desarrollo, el valor por defecto es explícito y se registra al arrancar Vite.
  - Si el destino no responde, el cliente recibe el error genérico de red (FR-011).

## D-05 Sesión y almacenamiento [Dec Q1] [Spec FR-008, FR-009]

- **Una sola clave** en `sessionStorage` (`carga-masiva.sesion`) con el JSON del contrato de F008: `accessToken`, `expiraEn`, `refreshToken` y `refreshExpiraEn`. No hay otros almacenes.
- **`api/sesion.ts`**: `leerSesion()`, `guardarSesion(tokens)` y `limpiarSesion()`, con `try/catch` (si `sessionStorage` no está disponible, la sesión queda vacía).
- **`AuthContext`**: estado inicial leído de `sessionStorage`. Expone:
  - `estaAutenticado`;
  - `iniciarSesion(email, password)`: llama a `api/auth.login` y guarda el par;
  - `cerrarSesion()`: limpia la sesión y navega a `/login`.
  - `useAuth()` lo consume. No decodifica el JWT: el rol no se usa en esta feature (Spec Assumptions).
- **Contrato de F008**: `POST /auth/login` con `{ email, password }` y `POST /auth/refresh` con `{ refreshToken }` en el cuerpo JSON; las respuestas son `Response<TokensDto>`. Sin cookies.

## D-06 Instancia HTTP e interceptores [Spec FR-010 a FR-015] [Plan]

- **`api/cliente.ts`**: instancia única `axios.create({ baseURL: '' })`.
- **Interceptor de petición**: si la URL no es `/auth/login` ni `/auth/refresh` y hay sesión, añade `Authorization: Bearer <accessToken>`.
- **Interceptor de respuesta**, ante un error:
  1. Si `status === 401`, la petición no es de login ni de refresh, no está marcada `_reintentada` y hay `refreshToken`:
     - marca `_reintentada`;
     - espera la **promesa compartida** de renovación (variable de módulo `renovacionEnCurso`: la primera petición la crea y las demás la esperan; se libera con `finally`);
     - guarda el par nuevo y reintenta **una vez** con el token nuevo.
  2. Si la renovación falla: `limpiarSesion()`, se rechazan las peticiones en espera (la misma promesa rechazada) y se navega a `/login` con `window.location.assign`. Es la forma más simple y sin acoplar el cliente HTTP al router; el `AuthContext` arranca vacío tras la recarga.
  3. Cualquier otro error se normaliza a `ApiError { code, message, errors }` desde `Response<T>`. Si no hay cuerpo `Response<T>` (red caída o cuerpo no JSON), se usa `code` 0 o el estado HTTP con el mensaje «No se pudo conectar con el servidor».
- **Renovación**: se hace con una llamada **sin** los interceptores de token (una instancia aparte o la marca de exclusión) para que un 401 del refresh no entre en el ciclo.
- **Respuesta correcta**: un helper `desempaquetar<T>(promesa)` devuelve `data` de `Response<T>` o lanza `ApiError`.
- **Consola**: no se escriben tokens ni contraseñas; los errores se lanzan sin registrarlos.

## D-07 Estilos y layout

- Tailwind CSS 4 con `@tailwindcss/vite` y `@import "tailwindcss";` en `index.css`. Sin configuración propia ni biblioteca de componentes.
- **`components/Layout.tsx`**: cabecera con enlaces a Subida e Historial; "Cerrar sesión" si hay sesión y "Iniciar sesión" si no; `<Outlet />` para el contenido.

## D-08 Verificación sin pantallas funcionales [Spec Assumptions]

En `npm run dev`, desde la consola del navegador:
- `await import('/src/api/cliente.ts')` y `await import('/src/api/sesion.ts')`, cargados por Vite en desarrollo, sin código adicional.
- Se guardan en `sessionStorage` tokens reales obtenidos con `POST /auth/login` (curl o Postman, sin imprimirlos).
- Para forzar el vencimiento se usan `JWT_VIGENCIA_ACCESO_MINUTOS=1` (y la del refresh) **solo** en el entorno del comando, sobre una base temporal.

## Abiertos

1. TypeScript 7 con la plantilla de configuración de Vite 8 (D-01).
2. Que la carga de módulos desde la consola en desarrollo funcione con la configuración elegida (D-08).
3. Validación en el equipo de evaluación: no realizada.
