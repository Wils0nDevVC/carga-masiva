# Feature Specification: Fundación del frontend React

**Feature Branch**: `011-fundacion-frontend` (no se crea rama; se trabaja en `main`)

**Created**: 2026-11-03

**Status**: Draft (v2: Q1–Q3 resueltas por el usuario, 2026-11-03)

**Input**: Base común del cliente web opcional del README. Incluye:
- un proyecto compilable y ejecutable que consume solo el Gateway;
- layout y navegación con páginas provisionales;
- la infraestructura HTTP: instancia común, manejo de `Response<T>` e interceptores de token y renovación;
- la base de sesión: contexto de autenticación y rutas protegidas;
- la integración en Compose.

No implementa las pantallas funcionales. Esta etapa genera solo la especificación y la enmienda autorizada de la constitución.

**Procedencia**: `[README §n]` = `referencias/README.md`; `[Constitución §n]` = `.specify/memory/constitution.md` v3.8.0; `[F008]`, `[F009]`, `[F010]` = features de Autenticación, Gateway y consultas; `[Decisión]` = decisión explícita del usuario (incluye el prompt de esta feature); `[Observado]` = comprobado leyendo el repositorio. **Pendiente** = no decidido; no es requisito.

## Contexto

**Exigido por el README, decisiones del usuario y pendientes**:

| Tema | README | Decisión del usuario / constitución | Pendiente |
|---|---|---|---|
| Cliente web | Opcional valorado: React 16+, uso de componentes; pantallas Login, Subida de Excel, Historial de cargas (tabla) y Detalle del estado; en su defecto, colecciones Postman [README §2, Frontend] | Se implementa. SPA con React, TypeScript y Vite; React Router; Axios con interceptores; Tailwind CSS sin biblioteca de componentes. Postman se conserva [Constitución §11] [Decisión] | — |
| Organización | — | pages, components, models (contratos TypeScript de la API, no copias de entidades de la base), api, context, hooks (por funcionalidad cuando coordinen estado, consultas o efectos; no uno por endpoint) y routes [Decisión] | — |
| Origen de los datos | El cliente envía todo al Gateway [README §3] | Solo el Gateway, desde el mismo origen: el cliente reenvía `/auth` y `/cargas` al Gateway (Vite en desarrollo) [Constitución §11] [F009] [Decisión Q2] | — |
| Sesión | El JWT se usa en las solicitudes posteriores [README §3 paso 0] | Contrato de F008: login y refresh con rotación; el refresh token viaja en el cuerpo JSON de `POST /auth/refresh` y la respuesta trae un par nuevo [Observado: `specs/008-autenticacion-jwt/contracts/auth-api.md`]. Tokens en `sessionStorage` [Decisión Q1] | — |
| Despliegue | Dockerfile y Compose valorados [README §4] | Dockerfile propio y servicio en Compose, en `frontend/` [Constitución §2, §11] [Decisión Q3] | — |
| Pruebas | — | Sin pruebas automatizadas; verificación manual [Constitución §11] | — |

**Contratos que se consumen** (sin cambios) [Observado]:
- `POST /auth/login` y `POST /auth/refresh`: devuelven `accessToken`, `expiraEn`, `refreshToken` y `refreshExpiraEn`; las credenciales o el refresh inválidos dan 401 genérico [F008].
- `POST /cargas` y `GET /cargas`, `/cargas/{id}` y `/cargas/{id}/contenido`, con JWT [F005] [F010].
- Errores propios del Gateway (401, 404 y 502) en `Response<T>` [F009].
- Todas las respuestas: `Response<T>` (`data`, `isSuccess`, `message`, `code`, `errors`) [Constitución §5].

**Fuera de esta feature** (se especifican después): formularios funcionales (login incluido), subida del Excel, tablas, detalle, lectura del contenido, *polling* y sus hooks.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Arrancar el cliente y navegar entre las secciones (Priority: P1)

Un evaluador arranca el cliente web (en local o con Compose) y navega entre Login, Subida, Historial y Detalle, que muestran contenido provisional. [Decisión]

**Why this priority**: Es la base sobre la que se construyen las pantallas del README.

**Independent Test**:
1. Compilar el proyecto sin errores.
2. Arrancarlo y abrir cada ruta.
3. Comprobar que el layout es común y que las rutas protegidas exigen sesión.

**Acceptance Scenarios**:

1. **Given** el código del cliente, **When** se compila para producción, **Then** termina sin errores ni advertencias del compilador de tipos.
2. **Given** el cliente en ejecución, **When** se abre en el navegador, **Then** muestra un layout común con navegación hacia Login, Subida de Excel, Historial y Detalle.
3. **Given** una ruta inexistente, **When** se abre, **Then** se muestra una página "no encontrada" del propio cliente, sin error del navegador.
4. **Given** el despliegue con Compose, **When** se levanta la solución, **Then** el cliente queda disponible junto con los demás servicios y sirve las rutas de la aplicación también al recargar la página en una ruta interna.
5. **Given** la URL del Gateway configurada, **When** cambia entre entornos, **Then** no hace falta modificar código, solo configuración.

---

### User Story 2 - Sesión y rutas protegidas (Priority: P1)

Las secciones Subida, Historial y Detalle solo se muestran con una sesión activa. Sin sesión, se redirige a Login. [README §3] [Decisión]

**Why this priority**: Todas las pantallas funcionales dependen de la sesión.

**Independent Test**: abrir una ruta protegida sin sesión y con una sesión establecida, es decir, tokens válidos obtenidos del Gateway y guardados en `sessionStorage` (Q1).

**Acceptance Scenarios**:

1. **Given** que no hay sesión, **When** se abre Subida, Historial o Detalle, **Then** se redirige a Login.
2. **Given** una sesión con tokens válidos, **When** se abre una ruta protegida, **Then** se muestra su página provisional.
3. **Given** una sesión activa, **When** se cierra la sesión desde el contexto, **Then** se eliminan los tokens y se vuelve a Login.
4. **Given** una sesión activa, **When** se recarga la pestaña, **Then** la sesión se conserva (`sessionStorage`). Al cerrar la pestaña se pierde, y otra pestaña no la comparte (Q1).

---

### User Story 3 - Infraestructura HTTP: Response<T>, token y renovación (Priority: P1)

Toda llamada al Gateway pasa por una instancia común. Esa instancia interpreta `Response<T>`, adjunta el token de acceso a las peticiones protegidas y, ante un 401, renueva la sesión una sola vez, aunque haya varias solicitudes simultáneas, y reintenta la solicitud original una única vez. [Decisión] [F008]

**Why this priority**: Evita que cada pantalla futura reimplemente la sesión y el manejo de errores.

**Independent Test**:
- Se verifica **sin pantallas funcionales**, con las herramientas de desarrollo del navegador, invocando el módulo de API del cliente en modo desarrollo contra el Gateway real.
- Se observa la pestaña de red: cabeceras, número de llamadas a `/auth/refresh` y reintentos.
- Se usan tokens obtenidos del Gateway y, para forzar el vencimiento, la vigencia configurable de F008.

**Acceptance Scenarios**:

1. **Given** una respuesta correcta del Gateway, **When** la recibe la instancia común, **Then** entrega `data` al llamador.
2. **Given** una respuesta de error en `Response<T>` (400, 401, 403, 404, 422, 429, 500, 502 o 503), **When** la recibe, **Then** entrega un error con `code`, `message` y `errors` del contrato, sin perder información.
3. **Given** un fallo sin `Response<T>` (red caída o cuerpo no JSON), **When** ocurre, **Then** entrega un error genérico con un mensaje comprensible.
4. **Given** una sesión activa, **When** se envía una petición protegida, **Then** lleva `Authorization: Bearer <token de acceso>`.
5. **Given** `POST /auth/login` o `POST /auth/refresh`, **When** se envían, **Then** no llevan el token de acceso y un 401 suyo **no** dispara una renovación.
6. **Given** un token de acceso vencido y un refresh token vigente, **When** una petición protegida recibe 401, **Then**:
   - se llama a `POST /auth/refresh` una vez con el refresh token, en el cuerpo según el contrato;
   - se guardan el token de acceso y el refresh token nuevos (la rotación de F008);
   - se reintenta la petición original una única vez con el token nuevo.
7. **Given** tres peticiones protegidas simultáneas que reciben 401, **When** se procesan, **Then** se hace **una sola** llamada a `/auth/refresh` y las tres se reintentan con el token nuevo.
8. **Given** una petición ya reintentada que vuelve a recibir 401, **When** ocurre, **Then** no se renueva otra vez: el error se entrega sin bucles.
9. **Given** un refresh que falla (401 por refresh vencido o ya usado, o un error de red), **When** ocurre, **Then** se limpia la sesión, se rechazan las peticiones en espera y se dirige a Login.
10. **Given** un 403 o cualquier error distinto de 401, **When** ocurre en una petición protegida, **Then** no se intenta renovar.

---

### Edge Cases

- **Varias pestañas**: con `sessionStorage` cada pestaña tiene su propia sesión, así que la rotación de F008 en una pestaña no invalida el refresh token de otra (Q1).
- **Gateway caído**: las peticiones dan un error genérico (502 `Response<T>` del Gateway, o un fallo de red). No se renueva ni se cierra la sesión.
- **Sin URL del Gateway configurada**: el cliente indica claramente la falta de configuración en lugar de llamar a un destino incorrecto. El mecanismo se fija en el plan.
- **Logs y consola**: el cliente no escribe tokens ni contraseñas en la consola.

## Requirements *(mandatory)*

### Functional Requirements

**Proyecto y despliegue**
- **FR-001**: El cliente MUST ser una SPA con React, TypeScript y Vite, con React Router, Axios y Tailwind CSS, sin biblioteca de componentes ni otras dependencias de estado, datos o framework (Redux, Zustand, React Query, Next.js). [Constitución §11] [Decisión]
- **FR-002**: El código MUST organizarse en pages, components, models, api, context, hooks y routes. Los models son contratos TypeScript de la API (`Response<T>` y los DTO de F008/F010), no entidades de la base. No se crean carpetas, hooks ni archivos vacíos, ni respuestas simuladas. [Decisión]
- **FR-003**: La URL del Gateway MUST ser configurable sin cambiar código. El cliente MUST consumir solo el Gateway. [Constitución §11]
- **FR-004**: El cliente MUST compilar para producción sin errores y ejecutarse en desarrollo. [Decisión]
- **FR-005**: El cliente MUST integrarse en el Compose de la raíz con su propio Dockerfile, conforme a §11. Los detalles técnicos (imagen, puerto, servidor de archivos estáticos) se fijan en el plan. El cliente vive en `frontend/` (Q3). [Constitución §2, §11] [Decisión]
- **FR-005a**: El navegador MUST acceder al Gateway desde el **mismo origen** del cliente:
  - el servicio del cliente reenvía `/auth` y `/cargas` al Gateway, y en desarrollo el servidor de Vite hace lo equivalente;
  - se conservan rutas, métodos, cabeceras y contratos;
  - el Gateway no cambia (sin CORS);
  - la URL del Gateway es la configuración del reenvío (FR-003). [Decisión Q2] [Constitución §9, §11]

**Navegación**
- **FR-006**: MUST existir un layout común y rutas para Login (pública), Subida de Excel, Historial y Detalle (protegidas; Detalle con identificador de carga), además de una página "no encontrada". Las páginas son provisionales y no tienen lógica de negocio. [README §2 Frontend] [Decisión]
- **FR-007**: Las rutas protegidas sin sesión MUST redirigir a Login. [Decisión]

**Sesión**
- **FR-008**: MUST existir un contexto de autenticación y un hook `useAuth` que expongan el estado de la sesión, el establecimiento de la sesión a partir de la respuesta de login y su cierre. El formulario de login queda fuera. [Decisión]
- **FR-009**: Los tokens MUST guardarse en `sessionStorage`, aceptando su exposición ante XSS, sin otros mecanismos de almacenamiento. Se usa solo lo que entrega el contrato de F008 (`accessToken`, `expiraEn`, `refreshToken` y `refreshExpiraEn`); no se inventan cookies ni otro transporte. Al cerrar la sesión o si falla la renovación, se eliminan de `sessionStorage`. [F008] [Decisión Q1]

**Infraestructura HTTP**
- **FR-010**: Toda llamada MUST pasar por una instancia común de Axios configurada con la URL del Gateway. [Decisión]
- **FR-011**: La instancia MUST interpretar `Response<T>`: devuelve `data` en el éxito y, en el error, un objeto con `code`, `message` y `errors`. Un fallo sin `Response<T>` da un error genérico. [Constitución §5]
- **FR-012**: El interceptor de petición MUST adjuntar `Authorization: Bearer` a las peticiones protegidas, y MUST NOT adjuntarlo a `/auth/login` ni a `/auth/refresh`. [F008] [F009]
- **FR-013**: El interceptor de respuesta MUST, ante un 401 de una petición protegida que aún no se ha reintentado:
  - hacer **una sola** renovación compartida por todas las peticiones simultáneas;
  - guardar el par nuevo;
  - reintentar cada petición **una única vez**.
  - El login y el refresh MUST quedar excluidos de este ciclo. [Decisión]
- **FR-014**: Si la renovación falla, el cliente MUST limpiar la sesión, rechazar las peticiones pendientes y dirigir a Login. [Decisión]
- **FR-015**: Ningún token ni contraseña MUST escribirse en la consola. [Constitución §10]

**Fuera de alcance** [Decisión]:
- Formularios funcionales (login incluido), subida, tablas, detalle, contenido del Excel, *polling* y sus hooks.
- Redux, Zustand, React Query, Next.js, bibliotecas de componentes, repositorios, clases base y capas adicionales.
- Pruebas automatizadas.
- Cambios de endpoints, reglas o contratos del backend.

### Key Entities *(include if feature involves data)*

- **Response<T>** (contrato TypeScript): `data`, `isSuccess`, `message`, `code` y `errors` [Constitución §5].
- **Tokens de sesión** (contrato TypeScript de F008): `accessToken`, `expiraEn`, `refreshToken` y `refreshExpiraEn`.
- **Error de API** (forma normalizada del cliente): `code`, `message` y `errors`, derivados de `Response<T>` o genéricos.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: La compilación de producción termina con 0 errores, y las 4 secciones y la página "no encontrada" se abren sin errores en la consola del navegador.
- **SC-002**: El 100 % de las rutas protegidas abiertas sin sesión redirige a Login.
- **SC-003**: Con 3 peticiones protegidas simultáneas y un token de acceso vencido, se observa exactamente 1 llamada a `/auth/refresh` y 3 reintentos que terminan correctamente.
- **SC-004**: Una petición que recibe 401 tras su reintento no produce más llamadas: como máximo 1 refresh y 1 reintento por petición original.
- **SC-005**: Si el refresh falla, la sesión queda vacía y se muestra Login en el 100 % de los casos. Ni `/auth/login` ni `/auth/refresh` disparan renovaciones.
- **SC-006**: El cliente arranca con la solución completa en Compose y sus rutas internas se sirven también al recargar.
- **SC-007**: En la consola del navegador no aparece ningún token ni contraseña durante la verificación.

## Assumptions

- **Verificación de la infraestructura HTTP**: con las herramientas de desarrollo del navegador sobre el cliente en modo desarrollo. Se colocan en `sessionStorage` tokens reales obtenidos del Gateway y se invocan los módulos de API desde la consola. Sin código de prueba, páginas de diagnóstico ni respuestas simuladas.
- **Sesiones cortas**: el vencimiento se fuerza con las vigencias configurables de F008 (`JWT_VIGENCIA_*`) solo en el entorno de la prueba.
- **Rol del usuario**: el contexto puede leer el claim `role` del token de acceso para usos futuros. Esta feature no autoriza por rol en el cliente: la autorización la decide Control [F009 Q2].
- **Versiones, puertos y estructura de archivos**: se fijan en el plan.

## Decisiones del usuario (2026-11-03)

| # | Pregunta | Decisión | Requisitos |
|---|---|---|---|
| Q1 | Almacenamiento de los tokens | **`sessionStorage`**, aceptando su exposición ante XSS; sin otros mecanismos de almacenamiento | FR-009, US2-4, Edge Cases |
| Q2 | Acceso del navegador al Gateway | **Mismo origen**: el cliente reenvía `/auth` y `/cargas` al Gateway (Vite lo equivalente en desarrollo); se conservan rutas, métodos y contratos; el Gateway no cambia | FR-003, FR-005a, US1-4/5 |
| Q3 | Directorio contenedor | **`frontend/`** en la raíz (constitución §2 actualizada) | FR-005 |

## Asuntos pendientes (no son requisitos aprobados)

Ninguno que requiera decisión. Se fijan en el plan: versiones, puertos, estructura detallada, el componente que sirve la SPA y hace el reenvío en Compose, y la forma concreta de configurar la URL del Gateway.
