# Contrato de la pantalla de Login

| Situación | Comportamiento |
|---|---|
| `/login` sin sesión | Formulario: correo (`type=email`, requerido), contraseña (`type=password`, requerida) y botón «Ingresar» |
| `/login` con sesión | `Navigate` a `/historial` |
| Envío | Botón deshabilitado con el texto «Ingresando…»; `iniciarSesion(email, password)` |
| 200 | Navega a `state.desde` (ruta interna distinta de `/login`) o a `/historial` |
| 401 | Muestra «Credenciales inválidas» (mensaje del servidor); vacía la contraseña; se queda en `/login` |
| 400 | Muestra `message` y la lista `errors` |
| Sin conexión | Muestra «No se pudo conectar con el servidor» |
| `RutaProtegida` sin sesión | `Navigate` a `/login` con `state.desde` = ruta solicitada |
