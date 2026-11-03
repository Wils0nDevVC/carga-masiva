# Feature Specification: Pantalla de Login en el cliente web

**Feature Branch**: `012-login-frontend` (no se crea rama; se trabaja en `main`)

**Created**: 2026-11-03

**Status**: Draft (v1)

**Input**: Pantalla funcional de Login en `cargamasiva_front`. Formulario de correo y contraseña que usa el contexto de sesión de la Feature 011 (login por el Gateway, contrato F008). Muestra el 401 genérico y los errores de validación de `Response<T>`. Tras el login lleva a Historial, o a la ruta protegida que se pidió antes. Con sesión activa, `/login` lleva a Historial. Alcance mínimo; esta etapa genera solo la especificación.

**Procedencia**: `[README §n]` = `referencias/README.md`; `[Constitución §n]` = v3.8.1; `[F008]` = contrato de Autenticación; `[F011]` = fundación del frontend (contexto de sesión, rutas protegidas, cliente HTTP); `[Decisión]` = decisión del usuario (incluye el prompt).

## Contexto

- **Exigido por el README**: pantalla "0. Login" del frontend opcional; el cliente envía las credenciales a `/auth/login` y usa el JWT en las solicitudes posteriores [README §2 Frontend, §3 paso 0].
- **Ya disponible** [F011]:
  - `iniciarSesion(email, password)` guarda los tokens en `sessionStorage`.
  - `RutaProtegida` redirige a `/login` cuando no hay sesión.
  - El cliente HTTP normaliza los errores a `ApiError {code, message, errors}`.
  - La página `/login` es provisional.
- **Contrato** [F008]:
  - 200 con tokens.
  - 401 «Credenciales inválidas», idéntico para un correo inexistente y para una contraseña errónea.
  - 400 «Error de validación» con `errors` si falta el correo o la contraseña.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Iniciar sesión (Priority: P1)

El usuario escribe su correo y su contraseña, envía el formulario y entra a la aplicación. [README §2, §3 paso 0]

**Why this priority**: Sin login, el resto de pantallas no es utilizable.

**Independent Test**: Abrir `/login` con el usuario inicial Operador, enviar credenciales correctas y erróneas, y observar la navegación y los mensajes.

**Acceptance Scenarios**:

1. **Given** credenciales correctas, **When** se envía el formulario, **Then** se inicia la sesión y se navega a `/historial`.
2. **Given** que el usuario llegó a `/login` redirigido desde una ruta protegida (por ejemplo, `/historial/5`), **When** inicia sesión, **Then** vuelve a esa ruta.
3. **Given** credenciales incorrectas (correo inexistente o contraseña errónea), **When** se envía, **Then** se muestra el mensaje del servidor «Credenciales inválidas», el usuario permanece en `/login` y la contraseña se vacía.
4. **Given** un error de validación del servidor (400 con `errors`), **When** se recibe, **Then** se muestran el mensaje y cada error de la lista.
5. **Given** un fallo de conexión (Gateway caído), **When** se envía, **Then** se muestra el mensaje genérico del cliente («No se pudo conectar con el servidor»).
6. **Given** el envío en curso, **When** se espera la respuesta, **Then** el botón queda deshabilitado, indica que se está enviando y no se permite un segundo envío.
7. **Given** una sesión activa, **When** se abre `/login`, **Then** se navega a `/historial`.

### Edge Cases

- **Campos vacíos**: el formulario exige correo y contraseña antes de enviar (validación del navegador). Si aun así llega un 400, se aplica el escenario 4.
- **Formato del correo**: no se valida más allá de que el campo no esté vacío; la validación la hace el servidor. No se inventan reglas [Constitución §4].
- **Ruta de retorno**: solo se vuelve a rutas internas de la aplicación. Si el origen es `/login` o no existe, se usa `/historial`.
- **Logs**: la contraseña ni los tokens se escriben en la consola [Constitución §10].

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: La página `/login` MUST mostrar un formulario con correo y contraseña (campo de contraseña oculto) y un botón de envío. [README §2] [Decisión]
- **FR-002**: El envío MUST usar el contexto de sesión existente (`iniciarSesion`), sin llamadas HTTP directas desde la página. [F011] [Constitución §11]
- **FR-003**: Tras un login correcto MUST navegarse a la ruta protegida de origen, si la hay, o a `/historial`. [Decisión]
- **FR-004**: `RutaProtegida` MUST conservar la ruta solicitada al redirigir a `/login`, para el retorno de FR-003. [Decisión]
- **FR-005**: Con sesión activa, `/login` MUST redirigir a `/historial`. [Decisión]
- **FR-006**: Los errores MUST mostrarse con el `message` del `ApiError` y, si existen, sus `errors`, sin textos inventados para el 401 (se muestra «Credenciales inválidas» del servidor). [F008] [Constitución §5]
- **FR-007**: Durante el envío, el botón MUST deshabilitarse; tras un error, la contraseña MUST vaciarse y el correo conservarse. [Decisión]
- **FR-008**: MUST NOT añadirse dependencias, registro, recuperación de contraseña, "recordarme" ni bibliotecas de formularios o validación. [Decisión] [Constitución §12]

### Key Entities

Sin entidades nuevas: se usan `LoginRequest`, `TokensDto` y `ApiError` de la Feature 011.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Con credenciales válidas, el usuario llega a Historial en un único envío, en el 100 % de los intentos con el usuario inicial Administrador y con el Operador.
- **SC-002**: Con credenciales inválidas, el 100 % de los intentos muestra «Credenciales inválidas» sin salir de Login, con la contraseña vacía.
- **SC-003**: Tras una redirección desde una ruta protegida, el login devuelve a esa misma ruta.
- **SC-004**: Con sesión activa, abrir Login lleva siempre a Historial.
- **SC-005**: Ninguna contraseña ni token aparece en la consola del navegador.

## Assumptions

- **Estilo**: formulario centrado con clases de Tailwind, coherente con el layout de la Feature 011.
- **Mensaje de envío**: el texto del botón durante el envío («Ingresando…») es una precisión de diseño, no un requisito del README.
- **Verificación**: manual en el navegador, en `http://localhost:3000` (Compose) o con `npm run dev`. Sin pruebas automatizadas [Constitución §11].
