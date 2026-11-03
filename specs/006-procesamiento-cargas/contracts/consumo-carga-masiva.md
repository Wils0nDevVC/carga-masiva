# Contrato: consumo de `carga_masiva` (consumidor ms-carga-masiva)

Procedencia: README §2, §3 paso 3; constitución §8, §9; F001 R-02, R-05; F005 `contracts/mensaje-carga-masiva.md`; spec 006 FR-001–005, FR-018, FR-023; `research.md` D-02–D-05, D-08.

## Topología (declarada también por el consumidor, mismos argumentos que Control)

| Elemento | Valor |
|---|---|
| Intercambio | `carga_masiva`, `direct`, durable |
| Cola | `carga_masiva`, durable, no exclusiva, sin autoborrado, sin argumentos |
| Enlace | clave `carga_masiva` |
| Consumo | `prefetchCount = 1`, confirmación manual, un mensaje a la vez |

## Mensaje de entrada (F005)

`{ "idCarga": <long>, "rutaArchivo": "seaweed://cargas/AAAA/MM/<guid>.xlsx", "usuario": "<correo>" }`. `rutaArchivo` y el correo se toman de la base (fuente de verdad); una diferencia se registra como advertencia.

## Decisión por mensaje

| Situación | Acción | Confirmación |
|---|---|---|
| JSON inválido, `idCarga` ≤ 0 o carga inexistente | log de error, sin cambios | ack (descartar) |
| Carga no Pendiente (En proceso, Cargado, Finalizado, Notificado, Fallido) | log informativo, sin cambios | ack (descartar) |
| `usp_CambiarEstadoCarga(En proceso, @EstadoEsperado='Pendiente')` devuelve 0 | log informativo, sin cambios | ack (descartar) |
| Procesada hasta Finalizado (con o sin notificación confirmada) | — | ack |
| Fallo terminal registrado (Fallido) | — | ack |
| Excepción no controlada, cerrojo perdido o fallo al registrar el fallo | log de error | **sin ack**: se cierra el canal; RabbitMQ reentrega; tras 10 s el consumidor vuelve a obtener el cerrojo y recupera antes de consumir |

## Exclusividad

Antes de recuperar o consumir: cerrojo de sesión (`contracts/cerrojo-consumidor.md`). Sin cerrojo no hay canal de consumo.

## Recuperación (antes de consumir)

`En proceso` → reanudar (cuenta un intento; límite `Procesamiento:MaxIntentos`, predeterminado 3). `Cargado` → Finalizado y notificar. `Pendiente` → no se toca. `Finalizado` sin notificación confirmada → no se recupera (limitación aceptada, constitución §8).

## Garantías frente a la feature 005

- Control solo pasa a Fallido una carga Pendiente (V003); el consumidor toma la carga con el mismo procedimiento y origen esperado Pendiente: exactamente uno de los dos cambios gana.
- Un mensaje tardío o duplicado de una carga Fallido o ya tomada se descarta sin cambios.
