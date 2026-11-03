# Quickstart: validación manual de la fundación del frontend

Verificación manual (constitución §11). Contrato en `contracts/frontend.md`. **Ningún escenario se ha ejecutado todavía.**

## Reglas

- Las pruebas que necesitan backend usan una base temporal `CargaMasivaApp_F011`, con toda la pila apuntando a ella. `CargaMasiva`, `CargaMasivaDb` y los volúmenes ajenos no se tocan; nunca se usa `docker compose down -v`.
- Los tokens se obtienen del Gateway y no se imprimen. Las vigencias cortas (`JWT_VIGENCIA_*=1`) se usan solo en el entorno del comando.
- La parte HTTP se verifica sin pantallas funcionales (research D-08).

## Escenarios

| # | Escenario | Resultado esperado | Spec |
|---|---|---|---|
| 1 | `npm ci && npm run build` en `frontend/` | 0 errores de TypeScript y de Vite | US1-1, SC-001 |
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
