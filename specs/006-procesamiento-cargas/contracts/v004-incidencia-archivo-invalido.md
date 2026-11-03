# Contrato: script V004 (tipo de incidencia `ArchivoInvalido`)

Procedencia: decisión del usuario Q1 (autorizada 2026-11-03); constitución §6, §7 (v3.2.0); spec 006 FR-008a; `research.md` D-15.

## Script

`database/scripts/V004__incidencia_archivo_invalido.sql`, aplicado por el runner existente en su transacción y registrado en `SchemaMigraciones`. V001–V003 no se modifican.

## Cambio

| Antes (V001) | Después |
|---|---|
| `CK_Incidencia_Tipo CHECK (Tipo IN ('PeriodoRechazado','PeriodoBloqueado','FilaOmitida','Existente','NoProcesado','ErrorTecnico','ErrorNotificacion'))` | misma restricción y nombre, con `'ArchivoInvalido'` añadido |

`ALTER TABLE dbo.Incidencia DROP CONSTRAINT CK_Incidencia_Tipo;` seguido de `ALTER TABLE dbo.Incidencia ADD CONSTRAINT CK_Incidencia_Tipo CHECK (...)`. Las filas existentes cumplen la nueva restricción (es un superconjunto). Sin otros cambios de esquema, índices ni procedimientos.

## Comprobación (quickstart, escenario 1)

En una base temporal creada por el runner: `SchemaMigraciones` = 4; insertar una incidencia `ArchivoInvalido` → aceptada; insertar un tipo inexistente → rechazado por el `CHECK`; los demás tipos siguen aceptados.
