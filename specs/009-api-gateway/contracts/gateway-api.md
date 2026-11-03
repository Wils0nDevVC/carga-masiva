# Contrato HTTP: API Gateway

Base: `http://localhost:8080` (Compose). Las respuestas de los servicios se reenvían sin cambios. Los errores que genera el propio Gateway usan `Response<T>` en camelCase, con `code` igual al estado HTTP real y sin detalles internos. [Dec Q1–Q3]

## Rutas

| Método y ruta | Destino | Token de acceso | Contrato del destino |
|---|---|---|---|
| `POST /auth/login` | ms-autenticacion `POST /auth/login` | no | `specs/008-autenticacion-jwt/contracts/auth-api.md` |
| `POST /auth/refresh` | ms-autenticacion `POST /auth/refresh` | no (Autenticación valida el refresh token) | ídem |
| `POST /cargas` | ms-control `POST /cargas` | sí: JWT válido (sin comprobación de rol en el Gateway) | `specs/005-recepcion-registro-cargas/contracts/http-recepcion-carga.md` |
| `GET /health` | el propio Gateway | no | `Response<string>` `data` "Healthy" |

Ninguna otra ruta o método se reenvía. Los `/health` de los servicios no se exponen.

## Validación del JWT en `/cargas`

HS256 con `JWT_CLAVE`, `iss` = `JWT_EMISOR`, `aud` = `JWT_AUDIENCIA`, `exp` obligatorio, sin tolerancia de reloj y `sub` entero > 0, igual que Control (F005). Control vuelve a validar el token y el rol.

## Errores propios del Gateway

| Caso | Estado | `message` |
|---|---|---|
| Sin token, token alterado, vencido, de otro emisor o audiencia, o refresh token como acceso | 401 | `No autorizado` |
| Ruta o método no configurado | 404 | `No encontrado` |
| Servicio de destino inaccesible | 502 | `Servicio no disponible` |
| Error inesperado del Gateway | 500 | `Error interno del servidor` |

Ejemplo:
```json
{ "data": null, "isSuccess": false, "message": "No autorizado", "code": 401, "errors": null }
```
