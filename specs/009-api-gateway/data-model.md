# Data Model: API Gateway mínimo con Ocelot

El Gateway no persiste datos ni accede a la base. Sin entidades.

La única estructura de datos que define es la configuración de rutas (research D-03):

| Elemento | Valores |
|---|---|
| Ruta | plantilla upstream, método (`POST`), destino (`host:puerto` de Compose + ruta), autenticación (sí o no) |
| Rutas | `/auth/login` → ms-autenticacion (sin token); `/auth/refresh` → ms-autenticacion (sin token); `/cargas` → ms-control (JWT) |

Respuesta de error propia: `Response<object>` con `data` null, `isSuccess` false, `message` y `code` (= estado HTTP), según `contracts/gateway-api.md`.
