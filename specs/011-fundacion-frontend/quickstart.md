# Quickstart: validación manual de la fundación del frontend

Verificación manual (constitución §11). Contrato en `contracts/frontend.md`. **Ningún escenario se ha ejecutado todavía.**

## Reglas

- Las pruebas que necesitan backend usan una base temporal `CargaMasivaApp_F011`, con toda la pila apuntando a ella. `CargaMasiva`, `CargaMasivaDb` y los volúmenes ajenos no se tocan; nunca se usa `docker compose down -v`.
- Los tokens se obtienen del Gateway y no se imprimen. Las vigencias cortas (`JWT_VIGENCIA_*=1`) se usan solo en el entorno del comando.
- La parte HTTP se verifica sin pantallas funcionales (research D-08).

## Escenarios

| # | Escenario | Resultado esperado | Spec |
|---|---|---|---|
| 1 | `npm ci && npm run build` en `cargamasiva_front/` | 0 errores de TypeScript y de Vite | US1-1, SC-001 |
| 2 | `npm run dev`; abrir `/login`, `/subir`, `/historial`, `/historial/1` y `/xyz` | Layout común. Sin sesión, `/subir`, `/historial` y `/historial/1` redirigen a `/login`; `/xyz` muestra "no encontrada"; sin errores en la consola | US1-2, US1-3, US2-1, SC-001, SC-002 |
| 3 | Guardar tokens reales en `sessionStorage` y abrir las rutas protegidas; recargar | Se muestran las páginas provisionales; tras recargar, la sesión sigue | US2-2, US2-4 |
| 4 | "Cerrar sesión" | Se elimina la clave y se navega a `/login` | US2-3 |
| 5 | Desde la consola: `GET /cargas` con la instancia común y sesión | Pestaña de red: `Authorization: Bearer …`; resultado = `data` | US3-1, US3-4 |
| 6 | `POST /auth/login` con credenciales erróneas desde la instancia | 401 → `ApiError` con «Credenciales inválidas»; sin llamada a `/auth/refresh` | US3-2, US3-5, SC-005 |
| 7 | Gateway detenido; `GET /cargas` | `ApiError` 502 del proxy o de red; sin renovación ni cierre de sesión | US3-3, Edge Cases |
| 8 | Acceso de 1 minuto vencido y refresh vigente; 3 `GET /cargas` simultáneos | Exactamente 1 `/auth/refresh` y 3 reintentos con 200; tokens nuevos en `sessionStorage` | US3-6, US3-7, SC-003 |
| 9 | Refresh vencido o ya usado (o borrado del registro en la base temporal); `GET /cargas` | 1 intento de refresh con 401; sesión limpia; navegación a `/login`; 0 reintentos posteriores | US3-8, US3-9, SC-004, SC-005 |
| 10 | Rol o 403 (si se puede provocar); otros errores (400, 404) | Sin renovación | US3-10 |
| 11 | `docker compose up -d --build frontend` | `frontend` `healthy`; `http://localhost:3000/historial` recargado sirve la SPA; `/auth/login` y `/cargas` llegan al Gateway | US1-4, SC-006 |
| 12 | Revisar la consola del navegador durante 1–11 | Ningún token ni contraseña | SC-007 |

## Cierre

Eliminar `CargaMasivaApp_F011`, volver al arranque normal sobre `CargaMasivaApp` (con `frontend`) y comprobar que todo está `healthy`. Registrar en "Resultados" lo ejecutado y lo no ejecutado.

## Resultados (implementación, 2026-11-03)

**Estado: implementada; infraestructura y despliegue verificados; interfaz sin verificar en un navegador** (no había navegador disponible en la sesión).
- Pila completa sobre `CargaMasivaApp_F011`, con `JWT_VIGENCIA_ACCESO_MINUTOS=1` solo en el entorno del comando.
- La parte HTTP se verificó ejecutando los **módulos reales** `cargamasiva_front/src/api/{cliente,auth,sesion}.ts` con `tsx` desde el scratchpad (fuera del repositorio), contra nginx en `:3000` (mismo origen) y con `sessionStorage` y `window.location` simulados. El registro de llamadas se hizo en el adaptador de Axios, que ve la petición final.
- Es equivalente a la consola del navegador de research D-08, pero **no** es el navegador.

| # | Resultado |
|---|---|
| 1 | `npm ci` y `npm run build`: 0 errores (98 módulos, 259 kB de JS). Abierto 1 resuelto |
| 2 | **Parcial**. nginx devuelve la SPA (`index.html` con `#root`) con 200 en `/`, `/historial`, `/historial/1`, `/subir`, `/login` y `/xyz`. **No verificado en un navegador**: el layout, la página "no encontrada", las redirecciones de `RutaProtegida` y la consola |
| 3 y 4 | **No verificados en un navegador** (persistencia al recargar y cierre de sesión en la interfaz). `sesion.ts` guarda, lee y limpia la clave, como se comprobó en el script (escenarios 8 y 9) |
| 5 | `GET /cargas` con sesión: la petición final lleva `Authorization: Bearer`; 200 y `isSuccess` true |
| 6 | Login erróneo: `ApiError {code 401, message "Credenciales inválidas"}`; una sola llamada (`/auth/login`, sin Bearer) y 0 refresh |
| 7 | Gateway detenido: `ApiError {code 504, "No se pudo conectar con el servidor"}` (nginx sin destino); la sesión se conserva; sin refresh ni redirección |
| 8 | Acceso vencido (más de 65 s) y 3 `GET /cargas` simultáneos: **1** `/auth/refresh` (sin Bearer), 6 `GET /cargas` (3 originales y 3 reintentos), los 3 con 200; token de acceso y refresh token rotados en `sessionStorage` |
| 9 | Par anterior (refresh ya usado) y acceso vencido: 1 `/auth/refresh` con 401 «Refresh token inválido»; 1 `GET /cargas` (sin reintento); sesión limpia; `window.location.assign('/login')`. Sin sesión: 401 «No autorizado» del Gateway y 0 refresh |
| 10 | 404 «La carga no existe.» y 400 «Error de validación» con sus `errors`: 0 renovaciones. El 403 no se provocó |
| 11 | `frontend` `healthy` en Compose. Reenvío por el mismo origen: `/auth/login` → 401 «Credenciales inválidas» y `/cargas` → 401 «No autorizado» (`Response<T>` del Gateway, sin cambios). Una subida de 5 MiB por `:3000` → 202 (`client_max_body_size 12m`; el valor por defecto de nginx, 1 MiB, la habría rechazado) |
| 12 | **Parcial**: `grep -rn "console\." cargamasiva_front/src` → 0 coincidencias. La consola del navegador no se revisó |

**Cierre**:
- Se borró el archivo de la carga de prueba de SeaweedFS (204); `CargaMasivaApp_F011` está eliminada.
- Arranque normal sobre `CargaMasivaApp` (vigencia de nuevo en 60) con todos los servicios `healthy`, `frontend` incluido.
- **Incidencia**: el borrado de los correos de Mailpit (`DELETE /api/v1/messages`) eliminó **todos** los mensajes, incluidas las notificaciones de 2 cargas reales hechas en `CargaMasivaApp` a las 21:01 y 21:02 UTC, ajenas a esta prueba. Sus cargas y archivos siguen intactos.
- **Observación**: el volumen `carga-masiva_sqlserver-data` se recreó el 2026-11-03T20:59:44Z, antes de esta verificación y fuera de ella; `CargaMasiva` y `CargaMasivaDb` ya no existen.
