# Contrato de mensajería: cola `carga_masiva` (publicador ms-control)

Procedencia: README §2 (Cola 1), §3 paso 2 y §4 (intercambio directo); spec 005 FR-021, FR-022; `research.md` D-07.

## Topología (Control la declara de forma idempotente antes de publicar)

| Elemento | Valor |
|---|---|
| Intercambio | `carga_masiva`, tipo `direct`, durable |
| Cola | `carga_masiva`, durable, sin argumentos adicionales |
| Enlace | clave de enrutamiento `carga_masiva` |

El consumidor (feature de Carga Masiva) debe declarar la misma topología con los mismos argumentos.

## Mensaje

Propiedades: persistente, `content-type: application/json`, `mandatory = true`. Cuerpo (formato del README, sin campos adicionales):

```json
{ "idCarga": 123, "rutaArchivo": "seaweed://cargas/2026/11/3f2b…e1.xlsx", "usuario": "user@example.com" }
```

| Campo | Origen |
|---|---|
| `idCarga` | `CargaArchivo.Id` |
| `rutaArchivo` | `CargaArchivo.RutaArchivo` (`seaweed://cargas/{AAAA}/{MM}/{guid}.xlsx`; ruta en el filer = lo que sigue a `seaweed://`, bajo la raíz del filer) |
| `usuario` | `Usuario.Email` del responsable |

## Garantías y límites

- Control considera publicado el mensaje solo con la confirmación del intermediario (publisher confirms con seguimiento).
- Un tiempo de espera o una conexión perdida dejan el resultado **desconocido**: el mensaje pudo llegar.
- Los reintentos pueden duplicar el mensaje. **Obligación del consumidor** (F001 R-03): si la carga no está Pendiente, confirmar y descartar sin reprocesar.
- Si Control registró Fallido tras un fallo definitivo, un mensaje que haya llegado se descarta por la misma regla (Fallido es terminal).
- Sin Outbox ni recuperación de mensajes no publicados; sin atomicidad entre base y cola.
