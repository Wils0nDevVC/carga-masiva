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

**Estado: implementada y compilada; NO verificada en ejecución.** Ningún escenario (1–13) se ha ejecutado.

| Comprobación | Estado |
|---|---|
| Compilación (`dotnet build Acity.Autenticacion.slnx`) | **Hecha**: 0 advertencias, 0 errores |
| `docker compose config -q` | **Hecha**, con valores ficticios solo en el entorno del comando; sin `ADMIN_*` y `OPERADOR_*` falla, como estaba previsto |
| V005 aplicado e ingeniería inversa (T005) | **No ejecutado**: pendiente de autorización |
| Escenarios 1–13 | **No ejecutados**: faltan los valores de `ADMIN_*` y `OPERADOR_*` en `.env` |
