# Feature Specification: API Gateway mínimo con Ocelot

**Feature Branch**: `009-api-gateway` (no se crea rama; se trabaja en `main`)

**Created**: 2026-11-03

**Status**: Draft (v2: Q1–Q3 resueltas por el usuario, 2026-11-03)

**Input**: API Gateway mínimo con Ocelot en .NET 10, en `api-gateway/`. Es el punto de entrada: valida el JWT emitido por ms-autenticacion y reenvía las solicitudes a Autenticación y Control. Tiene Dockerfile propio y servicio en Compose, y logs sin tokens. Alcance mínimo para el reto. Esta etapa genera solo la especificación.

**Procedencia**: `[README §n]` = `referencias/README.md`; `[Constitución §n]` = `.specify/memory/constitution.md` v3.5.0; `[F005]`, `[F008]` = features anteriores; `[Decisión]` = decisión explícita del usuario (incluye el prompt de esta feature); `[Observado]` = comprobado leyendo el repositorio. **Pendiente** = no decidido; no es requisito.

## Contexto

**Estado observado** [Observado]:
- `api-gateway/` existe y está vacío; no hay servicio Gateway en Compose.
- Endpoints de negocio actuales:
  - ms-autenticacion: `POST /auth/login` y `POST /auth/refresh`, sin token [F008].
  - ms-control: `POST /cargas`, con JWT y roles Administrador u Operador, y Rate Limiting [F005].
  - Cada servicio expone además `GET /health`.
  - No hay endpoints de consulta de cargas.
- Puertos publicados en el host: 8081 (Autenticación), 8082 (Control), 8083 y 8084 (Carga Masiva y Notificaciones), 8888, 8025, 15672 y 14333. El 8080 del host está libre.
- La configuración JWT (`JWT_CLAVE`, `JWT_EMISOR`, `JWT_AUDIENCIA`) ya existe en `.env` y la usan Autenticación y Control.

**Referencia GitHub** `api_gateway_net7`: accesible, consultada el 2026-11-03 (árbol de archivos). Orienta la estructura: un proyecto de host, la configuración de rutas en un archivo por entorno y un manejador para cuerpos ausentes. No se copian Happy, `HeaderToken`, `ValidationHeaderMiddleware` ni los perfiles de publicación [Constitución §1, §9].

**Exigido por el README, decisiones aprobadas y pendientes**:

| Tema | README | Constitución / decisión | Pendiente |
|---|---|---|---|
| Punto de entrada | Recibe todas las solicitudes del cliente web [README §2 Gateway] | Gateway mínimo con Ocelot en .NET 10 [Constitución §3, §9]. Q1: solo las rutas existentes [Decisión 2026-11-03] | — |
| JWT | Valida el JWT [README §2, §3 paso 1] | El mismo contrato que valida Control: HS256 con `JWT_CLAVE`, emisor y audiencia, `sub` y `role` [Constitución §9]. Q2: el rol lo autoriza Control [Decisión 2026-11-03] | — |
| Reenvío | Reenvía al microservicio correspondiente [README §2] | Control vuelve a validar el JWT; no hay cabeceras propias ni modos que eviten la seguridad [Constitución §9] | — |
| Rate Limiting | Obligatorio [README §4] | Ya está en Control; no se duplica en el Gateway [Constitución §9] | — |
| Errores | Manejo global [README §4] | Un solo formato de error, `Response<T>` [Constitución §5]. Q3: los errores propios del Gateway usan `Response<T>`; las respuestas de los servicios no se envuelven [Decisión 2026-11-03] | — |
| Exclusiones | — | Sin Happy, Code/Header/IdentityPool, Reec, AWS, SignalR, WebSockets, agregación de Swagger, Emissary ni Kubernetes [Constitución §9] | — |
| Despliegue y logs | Dockerfile y Compose (valorados) [README §4] | Un Dockerfile propio y servicio en Compose; Serilog con archivo por servicio y sin tokens [Constitución §10, §11] | — |

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Subir una carga a través del Gateway (Priority: P1)

Un usuario autenticado envía el Excel al Gateway con su JWT. El Gateway valida el token y reenvía la solicitud a Control, que la registra. [README §3 pasos 1–2]

**Why this priority**: Es el flujo principal del README a través del punto de entrada.

**Independent Test**:
1. Obtener un token de Operador.
2. `POST /cargas` al Gateway con el Excel de ejemplo: debe responder 202.
3. Repetir sin token, con un token alterado o con uno vencido: debe responder 401 y Control no debe recibir la solicitud.

**Acceptance Scenarios**:

1. **Given** un token de acceso válido, **When** se envía `POST /cargas` al Gateway con el Excel, **Then** la solicitud llega a Control con el archivo y la cabecera `Authorization` intactos, y el cliente recibe la misma respuesta que daría Control (202 con `idCarga`).
2. **Given** una solicitud sin token, o con un token mal firmado, vencido o de otro emisor o audiencia, **When** se envía al Gateway, **Then** el Gateway responde 401 con `Response<T>` (`code` 401) sin reenviarla a Control.
3. **Given** un refresh token enviado como token de acceso, **When** llega al Gateway, **Then** responde 401, porque no es un JWT [F008 FR-012].
4. **Given** una respuesta de error de Control (400, 401, 403, 413 o 429), **When** el Gateway la reenvía, **Then** el cliente recibe el mismo estado y el mismo cuerpo `Response<T>` de Control.
5. **Given** que Control no está disponible, **When** se envía la solicitud, **Then** el Gateway responde con un error de servicio no disponible en `Response<T>` (`code` igual al estado HTTP real), sin exponer detalles internos.

---

### User Story 2 - Autenticarse a través del Gateway (Priority: P1)

El cliente inicia sesión y renueva su token a través del Gateway, sin necesitar un token previo. [README §3 paso 0]

**Why this priority**: Sin login a través del Gateway, el cliente tendría que conocer la dirección de Autenticación, lo que contradice que el Gateway sea el punto de entrada.

**Independent Test**: `POST /auth/login` y `POST /auth/refresh` a través del Gateway deben dar las mismas respuestas que llamando directamente a Autenticación.

**Acceptance Scenarios**:

1. **Given** credenciales correctas, **When** se llama a `POST /auth/login` en el Gateway, **Then** se recibe la respuesta de Autenticación (200 con tokens).
2. **Given** credenciales inválidas, **When** se llama a `/auth/login` en el Gateway, **Then** se recibe el 401 genérico de Autenticación, sin alterar.
3. **Given** un refresh token válido, **When** se llama a `POST /auth/refresh` en el Gateway, **Then** se recibe el par nuevo de tokens.
4. **Given** el login y el refresh, **When** se llaman sin cabecera `Authorization`, **Then** el Gateway no exige token de acceso; Autenticación valida el refresh token según su contrato.

---

### Edge Cases

- **Ruta no configurada**: una ruta que el Gateway no expone (por ejemplo, el `/health` de un servicio interno) responde 404 con `Response<T>`. No se reenvía nada.
- **Método no configurado**: por ejemplo `GET /cargas`; responde 404 con `Response<T>`, sin reenviar.
- **Archivo grande**: el Gateway no impone un límite de tamaño menor que el de Control (10 MiB más el contenido del multipart), así que el 413 lo decide Control.
- **Rate Limiting**: el 429 lo emite Control. Su partición es el claim `sub` del token (la IP solo se usa si no hay `sub`), así que aunque todas las solicitudes lleguen desde el Gateway, los límites siguen siendo por usuario [Observado]. No hace falta cambiar Control.
- **Token válido en el límite de su vigencia**: el Gateway y Control validan sin tolerancia de reloj, igual que Control [F005].
- **Clave JWT ausente o de menos de 32 bytes, o sin emisor o audiencia**: el Gateway no arranca.
- **Salud del Gateway**: el Gateway expone su propio estado para el healthcheck de Compose, como los demás servicios.

## Requirements *(mandatory)*

### Functional Requirements

**Enrutamiento**
- **FR-001**: El Gateway MUST ser un servicio propio, en `api-gateway/`, sobre .NET 10 y con Ocelot como mecanismo de reenvío. [Constitución §2, §3, §9]
- **FR-002**: MUST reenviar exactamente estas rutas, por nombre de servicio dentro de Compose, conservando el método, el cuerpo (incluido el multipart), la cabecera `Authorization` y la respuesta del servicio (estado y cuerpo) [README §2] [Decisión Q1, 2026-11-03]:
  - `POST /auth/login` → Autenticación `POST /auth/login`, sin exigir token de acceso.
  - `POST /auth/refresh` → Autenticación `POST /auth/refresh`, sin exigir token de acceso. Autenticación valida el refresh token según su contrato [F008].
  - `POST /cargas` → Control `POST /cargas`, con JWT válido.
  - Los `/health` internos no se exponen. Las rutas de consulta se incorporarán cuando se implementen sus features.
- **FR-003**: Las rutas no configuradas MUST NOT reenviarse.

**Seguridad**
- **FR-004**: Las rutas protegidas MUST validar el JWT: firma HS256 con la clave de `JWT_CLAVE`, emisor `JWT_EMISOR`, audiencia `JWT_AUDIENCIA` y vigencia, sin tolerancia de reloj, exactamente como lo hace Control. Si el token no es válido, se responde 401 sin reenviar. [README §2] [Constitución §9] [F005]
- **FR-005**: El Gateway MUST validar el JWT conforme al contrato de Autenticación (FR-004) y MUST NOT autorizar por rol: la autorización por rol permanece en Control. Control conserva también su validación JWT ya aprobada. [Decisión Q2, 2026-11-03] [Constitución §9] [F005]
- **FR-006**: El login y el refresh MUST ser accesibles sin token. [F008]
- **FR-007**: El Gateway MUST NOT añadir cabeceras de identidad propias, modificar el token ni ofrecer modos que eviten la validación. [Constitución §9]
- **FR-008**: El Gateway MUST NOT aplicar Rate Limiting. [Constitución §9]

**Errores y logs**
- **FR-009**: Los errores que genera el propio Gateway (401 de token, ruta inexistente, servicio no disponible) MUST usar `Response<T>` (`data` null, `isSuccess` false), con `Code` igual al estado HTTP real y sin detalles internos. Las respuestas de los microservicios se reenvían sin volver a envolverlas. [Decisión Q3, 2026-11-03] [Constitución §5]
- **FR-010**: Los logs MUST ser estructurados, con archivo propio del Gateway, y MUST NOT contener tokens, cabeceras `Authorization` ni la clave JWT. [Constitución §10]

**Despliegue**
- **FR-011**: El Gateway MUST tener un Dockerfile propio y un servicio en el Compose de la raíz. Recibe `JWT_CLAVE`, `JWT_EMISOR` y `JWT_AUDIENCIA` desde `.env`, depende de Autenticación y Control y tiene un healthcheck. [Constitución §11]
- **FR-012**: La configuración inválida (JWT) MUST impedir el arranque, sin mostrar valores secretos. [Decisión: mismo criterio que Control y Autenticación]

**Fuera de alcance** [Constitución §1, §9]:
- Happy, Code/Header/IdentityPool, Reec, AWS, SignalR, WebSockets, la agregación de Swagger, Emissary y Kubernetes.
- Rate Limiting, caché, QoS de Ocelot, descubrimiento de servicios y balanceo de carga.
- Endpoints nuevos de consulta de cargas: no existen en Control y no forman parte de esta feature.
- Cambios en Autenticación o Control.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: El flujo completo login → subida, hecho solo contra el Gateway, termina con la carga registrada (202) y la carga llega a Notificado igual que sin Gateway.
- **SC-002**: El 100 % de las solicitudes a rutas protegidas sin token, con token alterado, vencido o de otro emisor o audiencia, o con un refresh token, reciben 401 del Gateway. En los logs de Control no aparece ninguna de esas solicitudes.
- **SC-003**: Las respuestas reenviadas (200, 202, 400, 401, 413 y 429 de los servicios) llegan al cliente con el mismo estado y el mismo cuerpo que al llamar directamente al servicio.
- **SC-004**: La búsqueda de los tokens emitidos durante la prueba y de la clave JWT en los logs del Gateway da 0 coincidencias.
- **SC-005**: El Gateway arranca `healthy` en Compose junto con los demás servicios, y no arranca sin una configuración JWT válida.

## Assumptions

- **Puerto del host**: el Gateway se publica en el puerto 8080 del host. Autenticación (8081) y Control (8082) siguen publicados para pruebas directas, como hasta ahora. Cerrarlos no se pidió.
- **Estructura**: el Gateway es un único proyecto de host (`Acity.Api`), sin Application, Domain ni Infrastructure. La constitución §4 define esas capas para los cuatro microservicios y §4 prohíbe crear capas vacías.
- **Rutas iguales**: el Gateway expone las mismas rutas que los servicios (por ejemplo, `/cargas` → Control `/cargas`), sin prefijos nuevos.
- **Comunicación interna**: dentro de Compose, por HTTP y nombre de servicio, sin TLS (igual que el resto).
- **Pruebas**: con Postman o curl. No hay tests automatizados [Constitución §11].

## Decisiones del usuario (2026-11-03)

| # | Pregunta | Decisión | Requisitos |
|---|---|---|---|
| Q1 | Rutas expuestas | Solo las existentes: `POST /auth/login` y `POST /auth/refresh` sin exigir token de acceso (Autenticación valida el refresh token según su contrato), y `POST /cargas` con JWT válido. No se exponen los `/health` internos; las rutas de consulta se incorporarán con sus features | FR-002, FR-006, US1, US2 |
| Q2 | Autorización | El Gateway valida el JWT conforme al contrato de Autenticación; la autorización por rol permanece en Control, que conserva su validación JWT | FR-004, FR-005 |
| Q3 | Errores propios del Gateway | `Response<T>` con `Code` igual al estado HTTP real y sin detalles internos; las respuestas de los microservicios se reenvían sin volver a envolverlas | FR-009, US1-2, US1-5, Edge Cases |

## Asuntos pendientes (no son requisitos aprobados)

Ninguno que requiera decisión. El estado exacto de "servicio no disponible" (502 o 503) y el mecanismo concreto se fijan en el plan.
