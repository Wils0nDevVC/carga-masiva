# Contrato del cliente web (fundación)

## Rutas de la SPA [research D-02]

| Ruta | Acceso | Página (provisional) |
|---|---|---|
| `/login` | pública | Login |
| `/subir` | protegida | Subida de Excel |
| `/historial` | protegida | Historial de cargas |
| `/historial/:idCarga` | protegida | Detalle de una carga |
| `/` | — | redirige a `/historial` |
| cualquier otra | pública | "No encontrada" |

Una ruta protegida sin sesión redirige a `/login`.

## Reenvío al Gateway (mismo origen) [Dec Q2] [research D-03, D-04]

| Prefijo | Destino | Entorno |
|---|---|---|
| `/auth/…` | `${GATEWAY_URL}/auth/…` | nginx (Compose) y proxy de Vite (desarrollo) |
| `/cargas` y `/cargas/…` | `${GATEWAY_URL}/cargas…` | ídem |
| resto | SPA (`index.html` si el archivo no existe) | nginx |

Se conservan método, cuerpo, cabeceras (incluida `Authorization`) y respuesta. Cuerpo máximo: 12 MiB.

## Comportamiento HTTP del cliente [Spec FR-010 a FR-015]

| Situación | Comportamiento |
|---|---|
| Petición protegida con sesión | `Authorization: Bearer <accessToken>` |
| `POST /auth/login` o `POST /auth/refresh` | Sin `Authorization`; un 401 no dispara renovación |
| 401 en una petición protegida no reintentada | Una renovación compartida (`POST /auth/refresh` con `{ refreshToken }`); se guarda el par nuevo; un reintento |
| 401 tras el reintento | Se entrega el error, sin otra renovación |
| La renovación falla | Se limpia `sessionStorage`, se rechazan las peticiones en espera y se navega a `/login` |
| 403 u otro error | `ApiError` desde `Response<T>`, sin renovación |
| Sin respuesta o cuerpo no JSON | `ApiError` genérico (`code` 0 o el estado HTTP; «No se pudo conectar con el servidor») |
