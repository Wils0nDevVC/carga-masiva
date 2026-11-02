# Contrato: `usp_CambiarEstadoCarga` con estado de origen esperado (V003)

Procedencia: decisión del usuario (opción A); spec 005 FR-023, FR-032; F001 `contracts/procedimientos-almacenados.md`; `research.md` D-08.

## Script

`database/scripts/V003__estado_origen_esperado.sql`: `ALTER PROCEDURE dbo.usp_CambiarEstadoCarga`, aplicado por el runner existente en su transacción y registrado en `SchemaMigraciones`. V001 y V002 no se modifican.

## Parámetros

| Parámetro | Dirección | Cambio |
|---|---|---|
| `@CargaId BIGINT` | entrada | sin cambio |
| `@NuevoEstado VARCHAR(20)` | entrada | sin cambio |
| `@PeriodoFallido CHAR(7) = NULL` | entrada | sin cambio |
| `@Cambiado BIT OUTPUT` | salida | sin cambio |
| `@EstadoEsperado VARCHAR(20) = NULL` | entrada | **nuevo, opcional, al final** |

## Comportamiento

1. Sin cambios: rechazo dentro de una transacción abierta (50020), estado de destino inválido (50021), carga inexistente (50022).
2. Nuevo: si `@EstadoEsperado` no es NULL y no es uno de los seis estados → `THROW 50023`.
3. Dentro de la transacción, tras leer `@actual` con `UPDLOCK, ROWLOCK`: si `@EstadoEsperado IS NOT NULL AND @actual <> @EstadoEsperado` → revierte y devuelve `@Cambiado = 0` **sin** actualizar la carga, sin historial y sin tocar periodos ni incidencias.
4. En otro caso, idéntico a V002: matriz, terminales, `FechaFin`, historial, periodos e incidencias `NoProcesado` al pasar a Fallido, idempotencia.

La lectura, la comparación y el cambio ocurren en la misma transacción con el bloqueo de fila: no hay ventana entre comprobar y cambiar.

## Compatibilidad

- Una llamada sin `@EstadoEsperado` (con nombre o posicional, porque el parámetro nuevo va al final) produce exactamente el resultado de V002 para cada transición.
- Usos: ms-control lo llama con `@NuevoEstado = 'Fallido', @EstadoEsperado = 'Pendiente'`. Carga Masiva y Notificaciones pueden seguir llamándolo sin el parámetro.

## Comprobación (quickstart, escenario 2)

Sobre una base temporal creada por el runner: transiciones de la matriz sin el parámetro = mismo resultado que V002; con origen esperado Pendiente sobre una carga En proceso → `@Cambiado = 0` y carga, historial y periodos sin cambios; sobre una carga Pendiente → Fallido con su historial.
