# Quickstart: validación manual de la autenticación

Verificación manual (constitución §11). Los contratos están en `contracts/`. **Ningún escenario se ha ejecutado todavía.**

## Reglas

- Los datos de prueba van solo en una base temporal `CargaMasivaApp_F008` creada por el runner (V001–V005). `CargaMasiva`, `CargaMasivaDb` y los volúmenes ajenos no se tocan. Nunca se usa `docker compose down -v`.
- Los secretos solo se pasan por entorno: de `.env` se cargan únicamente las variables necesarias (no `MSSQL_SA_PASSWORD`). No se imprimen contraseñas, tokens ni la clave.
- Las contraseñas iniciales las define el usuario en `.env` (`ADMIN_*`, `OPERADOR_*`).

## Preparación

1. Añadir a `.env` (lo hace el usuario o el agente con autorización) las variables `ADMIN_EMAIL`, `ADMIN_CLAVE`, `OPERADOR_EMAIL` y `OPERADOR_CLAVE`. Opcionalmente, `JWT_VIGENCIA_ACCESO_MINUTOS` y `JWT_VIGENCIA_REFRESH_MINUTOS`.
2. Arrancar con `DB_NAME=CargaMasivaApp_F008 docker compose up -d --build ms-autenticacion ms-control`.
3. Comprobar que `SchemaMigraciones` tiene 5 registros y que existe la tabla `RefreshToken`.

## Escenarios

| # | Escenario | Pasos | Resultado esperado | Spec |
|---|---|---|---|---|
| 1 | Usuarios iniciales | Primer arranque | 2 usuarios (uno por rol); `PasswordHash` en formato `PasswordHasher` (no el texto plano) | US3-1, FR-018 |
| 2 | Repetibilidad | Reiniciar 2 veces; luego cambiar `OPERADOR_CLAVE` y reiniciar | Siguen 2 usuarios y los hashes no cambian; el login solo funciona con la contraseña original | US3-2, US3-3, SC-006 |
| 3 | Configuración ausente | Arrancar sin `ADMIN_CLAVE` (Compose lo exige) o con `Jwt__VigenciaAccesoMinutos=0` | No arranca; el log no muestra secretos | US3-4, FR-019 |
| 4 | Login correcto | `POST /auth/login` con cada usuario inicial | 200 con los tokens; el JWT decodificado tiene `sub` = Id, `role`, `iss`, `aud` y `exp` = `iat` + 3600 | US1-1, US1-2, SC-002 |
| 5 | Compatibilidad con Control | `POST /cargas` con el Excel de ejemplo y `Authorization: Bearer <accessToken>` | 202 | US1-3, SC-001 |
| 6 | Credenciales inválidas | Correo inexistente; correo correcto con contraseña errónea | 401 con cuerpos idénticos | US1-4, SC-003 |
| 7 | Solicitud incompleta | Sin `password`; cuerpo vacío; JSON inválido | 400 con `Response<T>` (sin `ProblemDetails`) | US1-5 |
| 8 | Renovación | `POST /auth/refresh` con el refresh token del escenario 4; usar el nuevo token de acceso en Control | 200 con un par nuevo; Control responde 202 | US2-1, SC-004 |
| 9 | Reutilización | Repetir el refresh del escenario 8 con el mismo valor | 401 | US2-4 |
| 10 | Rechazos | Refresh alterado; token de acceso enviado a `/auth/refresh`; refresh token enviado a Control | 401 en los tres casos | US2-2, US2-5, US2-6, SC-005 |
| 11 | Vencimiento | Con `JWT_VIGENCIA_REFRESH_MINUTOS=1`, esperar más de 1 minuto y renovar | 401 | US2-3 |
| 12 | Usuario inexistente | No es reproducible: la FK `RefreshToken → Usuario` impide borrar un usuario con refresh tokens, y no hay borrado de usuarios | Se verifica revisando el código (el handler responde 401 si no encuentra el usuario) | US2-7 |
| 13 | Logs | Buscar con `grep -F` las contraseñas, los tokens emitidos y `JWT_CLAVE` en los logs de ms-autenticacion | 0 coincidencias | SC-007 |

## Cierre

Eliminar `CargaMasivaApp_F008`, volver al arranque normal sobre `CargaMasivaApp` (el runner aplica V005) y comprobar que todo queda `healthy`. Registrar en "Resultados" lo ejecutado y lo no ejecutado.

## Resultados (implementación, 2026-11-03)

**Estado: implementada y verificada en ejecución sobre `CargaMasivaApp_F008`** (escenarios 1–11 y 13; el 12 se verificó revisando el código). T005: ingeniería inversa autorizada y ejecutada; coincide con el mapeo. Cierre (T030) hecho.

| # | Resultado |
|---|---|
| Compilación | 0 advertencias, 0 errores |
| V005 | Aplicado por el runner (`SchemaMigraciones` = 5). El catálogo coincide con la entidad y el mapeo: `Id` bigint identity, `UsuarioId` int, `TokenHash` binary(32), `FechaCreacion` y `FechaExpiracion` datetime2 NOT NULL, `FechaUso` datetime2 NULL; PK, FK, UQ y CHECK presentes |
| 1 | 2 usuarios (Administrador Id 1, Operador Id 2) con hash `AQAAAA…` de 84 caracteres |
| 2 | 2 reinicios y un arranque con `OPERADOR_CLAVE` distinta (solo en el entorno del comando): 2 usuarios con la misma suma de comprobación de los hashes; log "ya existe; no se modifica"; el login con la contraseña cambiada da 401 |
| 3 | Sin `ADMIN_CLAVE`, Compose no arranca. Con `JWT_VIGENCIA_ACCESO_MINUTOS=0`, el servicio termina con código 1 y "Configuración Jwt inválida…", sin valores |
| 4 | 200 para los dos usuarios; HS256; `sub` 1 y 2; `role` correcto; `iss` ms-autenticacion; `aud` carga-masiva; `exp - iat` = 3600; refresh de 43 caracteres, no JWT |
| 5 | Control: 202 (`idCarga` 1) con el token de Operador |
| 6 | Correo inexistente y contraseña errónea: 401 con cuerpos idénticos ("Credenciales inválidas", `data` null) |
| 7 | Sin `password`, cuerpo vacío o JSON inválido: 400 con `Response<T>`, sin `ProblemDetails`. **Observación** (Abierto 1): los mensajes los genera el enlace de modelos, en inglés, con el nombre del parámetro (`comando`) y el detalle del analizador JSON |
| 8 | Refresh 200 con un refresh nuevo distinto del anterior; el nuevo acceso es aceptado por Control (202) |
| 9 | Reutilizar el refresh ya usado: 401 "Refresh token inválido" |
| 10 | Refresh alterado: 401. Token de acceso enviado como refresh: 401. Refresh enviado a Control: 401. El refresh vigente sigue funcionando después de esos rechazos (200). `{}` → 400 |
| 11 | Con `JWT_VIGENCIA_REFRESH_MINUTOS=1`, renovar a los 65 s da 401 y el log registra el motivo `Vencido` |
| 12 | No reproducible (la FK impide borrar el usuario); en el código, el handler responde 401 cuando el usuario no existe |
| 13 | 126 líneas de log (contenedor y archivo): 0 coincidencias de contraseñas, `JWT_CLAVE`, 10 tokens emitidos ni `Password=` |

### Cierre (T030)

- Las 2 cargas de prueba llegaron a Notificado: punta a punta con el token emitido por ms-autenticacion, y Mailpit capturó 2 correos para `operador@carga-masiva.test`.
- Se borraron sus 2 archivos de SeaweedFS (204) y los correos de Mailpit. La base principal no tenía cargas, así que no se borró nada ajeno.
- Colas `carga_masiva` y `notificaciones` con 0 mensajes.
- `CargaMasivaApp_F008` eliminada; quedan `CargaMasiva`, `CargaMasivaApp` y `CargaMasivaDb` sin cambios ajenos.
- Arranque normal sobre `CargaMasivaApp`: el runner aplicó V005 (`SchemaMigraciones` = 5), se crearon los usuarios iniciales (admin Id 1, operador Id 2), `RefreshToken` sin filas y todos los servicios `healthy`.
- No ejecutado: validación en el equipo de evaluación.
