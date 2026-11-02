# Contrato HTTP: recepción de una carga (ms-control)

Descripción de contrato, no código. Procedencia: spec 005 y `research.md` (D-02, D-04, D-05, D-11, D-12).

## Operación

| | |
|---|---|
| Ruta | `POST /cargas` (sin prefijo; el Gateway enrutará en su feature) |
| Tipo de contenido | `multipart/form-data`, un único archivo en el campo `archivo` (se cuentan todos los archivos del formulario) |
| Autenticación | `Authorization: Bearer <JWT>` validado por Control (firma HS256, emisor, audiencia, vigencia, `ClockSkew` 0) |
| Autorización | `role` ∈ {`Administrador`, `Operador`} |
| Rate Limiting | política `carga`: 10 solicitudes / 60 s por `sub` (o IP), sin cola; configurable |

## Contrato del token (lo respetará Autenticación)

| Claim | Tipo | Regla |
|---|---|---|
| `sub` | texto con entero positivo | `Usuario.Id`; si no es un entero positivo → 401 |
| `role` | texto | exactamente `Administrador` u `Operador`; ausente o distinto → 403 |
| `iss` | texto | = `Jwt__Emisor` |
| `aud` | texto | = `Jwt__Audiencia` |
| `exp` | fecha numérica | obligatoria; vencida → 401 |

El correo no es claim requerido; Control lo toma de `Usuario.Email`. Clave simétrica compartida por configuración externa (`Jwt__Clave`, ≥ 32 bytes).

## Respuestas (`Response<T>`, JSON camelCase, cinco campos siempre presentes)

| Caso | HTTP | `isSuccess` | `message` | `data` | `errors` |
|---|---|---|---|---|---|
| Aceptada | 202 | true | "Carga registrada" | `{ "idCarga": <long>, "estado": "Pendiente" }` | null |
| Sin archivo, varios, 0 bytes, extensión ≠ `.xlsx`, nombre > 260, tamaño > máximo (hasta máximo + 1 MiB) | 400 | false | "Error de validación" | null | lista de motivos |
| Sin token, token inválido/caducado, `sub` no numérico, usuario inexistente | 401 | false | "No autenticado" / "Usuario no reconocido" | null | null |
| Rol sin permiso | 403 | false | "Sin permiso" | null | null |
| Cuerpo mayor que máximo + 1 MiB (corte del servidor) | 413 | false | "El archivo excede el tamaño permitido" | null | null |
| Límite de frecuencia excedido | 429 | false | "Demasiadas solicitudes" | null | null |
| Fallo técnico definitivo (SeaweedFS, base, RabbitMQ) | 500 | false | mensaje genérico | null | null |

Ejemplo de éxito:

```json
{ "data": { "idCarga": 123, "estado": "Pendiente" }, "isSuccess": true, "message": "Carga registrada", "code": 202, "errors": null }
```

Reglas: `code` = estado HTTP real; sin trazas, rutas internas, cadenas de conexión ni secretos en el cuerpo. Los textos de `message` son provisionales (no son requisito de negocio). Un 202 implica archivo almacenado, carga + historial Pendiente confirmados y publicación confirmada (o evidenciada porque la carga ya salió de Pendiente).

## Orden de evaluación

Rate Limiting → autenticación (401) → autorización (403) → validación del archivo (400/413) → usuario persistido (401) → subida → registro → publicación.
