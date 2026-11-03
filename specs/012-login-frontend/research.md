# Research: Pantalla de Login

**Procedencia**: `Dec` = decisión del usuario o constitución v3.8.1; `Spec` = spec 012; `F008/F011`; `Plan` = decisión técnica de este plan (alternativa más sencilla).

## D-01 Sin dependencias ni hooks nuevos [Spec FR-008] [Dec §11]

- El formulario usa estado local de React (`useState`) en `LoginPage.tsx` y el `useAuth()` existente.
- No se crea un hook `useLogin`: el estado es de una sola página y no se comparte (constitución §11: los hooks se crean por funcionalidad cuando coordinen estado, consultas o efectos que lo justifiquen).

## D-02 Ruta de retorno [Spec FR-003, FR-004]

- `RutaProtegida` redirige con `<Navigate to="/login" replace state={{ desde: ubicacion.pathname + ubicacion.search }} />`.
- `LoginPage` lee `location.state?.desde`. Solo se acepta si empieza por `/` y no es `/login`; en otro caso se usa `/historial`.
- Tras el login: `navigate(destino, { replace: true })`. [Plan]

## D-03 Con sesión activa [Spec FR-005]

Si `estaAutenticado`, `LoginPage` devuelve `<Navigate to="/historial" replace />`. No hace falta una ruta "solo invitados" adicional. [Plan]

## D-04 Errores y envío [Spec FR-006, FR-007] [F008] [F011]

- `iniciarSesion` lanza un `ApiError` (`code`, `message` y `errors`), normalizado por `cliente.ts`. La página muestra `message` y la lista `errors` si no está vacía.
- En `catch`: `setPassword('')`; el correo se conserva.
- `enviando` deshabilita el botón, que muestra «Ingresando…».
- Los campos usan `required`, `type="email"` y `type="password"` (validación nativa mínima). `autoComplete` es `username` y `current-password`.
- No se registra nada en la consola.
