# Contrato de mensajería: cola `notificaciones` (publicador ms-carga-masiva)

Procedencia: README §2 (Cola 2), §3 paso 3, §4 (intercambio directo); constitución §8 (v3.2.0); spec 006 FR-020–022; `research.md` D-13.

## Topología (declarada de forma idempotente por el publicador)

| Elemento | Valor |
|---|---|
| Intercambio | `notificaciones`, `direct`, durable |
| Cola | `notificaciones`, durable, no exclusiva, sin autoborrado, sin argumentos |
| Enlace | clave `notificaciones` |

El consumidor (feature de Notificaciones) debe declarar la misma topología con los mismos argumentos.

## Mensaje

Propiedades: persistente, `content-type: application/json`, `mandatory = true`. Cuerpo (formato del README, sin campos adicionales):

```json
{ "idCarga": 123, "usuario": "user@example.com", "fechaFin": "2026-11-03T10:20:00.0000000Z" }
```

| Campo | Origen |
|---|---|
| `idCarga` | `CargaArchivo.Id` |
| `usuario` | `Usuario.Email` del responsable |
| `fechaFin` | `CargaArchivo.FechaFin` (la fija `usp_CambiarEstadoCarga` al pasar a Finalizado), UTC en ISO 8601 con `Z` |

Se publica una vez por carga que llega a Finalizado, incluidas las de archivo inválido o con todos los periodos rechazados.

## Garantías y límites

- Publicado solo con confirmación del intermediario (publisher confirms con seguimiento); 2 reintentos.
- Sin confirmación tras los reintentos: la carga **conserva Finalizado** y se registra `ErrorTecnico` describiendo la falta de confirmación; el mensaje pudo llegar.
- Un reintento tras una confirmación perdida puede duplicar el mensaje. **Obligación del consumidor de Notificaciones**: enviar y marcar Notificado solo si la carga está Finalizado; descartar en otro caso.
- Sin recuperación automática de notificaciones no confirmadas ni republicación de cargas Finalizado al arrancar (limitación aceptada por el usuario).
