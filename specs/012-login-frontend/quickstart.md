# Quickstart: validación manual del Login

Verificación manual en el navegador (`http://localhost:3000` con Compose o `npm run dev`). Credenciales: `ADMIN_*` y `OPERADOR_*` de `.env`. **Ningún escenario se ha ejecutado todavía.**

| # | Escenario | Resultado esperado | Spec |
|---|---|---|---|
| 1 | Login correcto con el Operador y con el Administrador | Navega a `/historial`; la cabecera muestra «Cerrar sesión» | US1-1, SC-001 |
| 2 | Sin sesión, abrir `/historial/5` y luego iniciar sesión | Vuelve a `/historial/5` | US1-2, SC-003 |
| 3 | Contraseña errónea y correo inexistente | «Credenciales inválidas»; sigue en `/login`; contraseña vacía; correo conservado | US1-3, SC-002 |
| 4 | Gateway detenido | «No se pudo conectar con el servidor» | US1-5 |
| 5 | Pestaña de red con conexión lenta (*throttling*) | Botón deshabilitado con «Ingresando…»; un solo `POST /auth/login` | US1-6 |
| 6 | Con sesión, abrir `/login` | Navega a `/historial` | US1-7, SC-004 |
| 7 | Consola del navegador | Sin contraseñas ni tokens | SC-005 |

El escenario 4 de la spec (400 con `errors`) solo se alcanza saltándose `required` (por ejemplo, desde las herramientas de desarrollo); su lógica es la misma que la del 401.

## Resultados (implementación, 2026-11-03)

- **Hecho**: compilación sin errores; `frontend` reconstruido y `healthy`; `http://localhost:3000/login` sirve la SPA (200).
- **No ejecutado**: los escenarios 1 a 7 (requieren un navegador; no había ninguno disponible en la sesión). El backend que usan ya se verificó en las features 008, 009 y 011 (login, 401 genérico, mismo origen).
