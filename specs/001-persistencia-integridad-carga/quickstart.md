# Quickstart: validación manual del modelo de persistencia

Guía de verificación manual (sin pruebas automatizadas) para cuando existan los scripts, el runner y los consumidores. Modelo: [data-model.md](data-model.md); contratos: [contracts/procedimientos-almacenados.md](contracts/procedimientos-almacenados.md); mecanismos: [research.md](research.md). A la fecha **no existen** scripts ni runner, y Docker/SQL Server **no se ha verificado** de extremo a extremo (research R-11).

## Prerrequisitos
- Docker con SQL Server (imagen amd64; en Apple silicon, con emulación: ver R-11) y el runner definidos en `compose.yaml` (hoy vacío).
- Cliente SQL para consultar tablas.
- Credenciales iniciales por variables de entorno, nunca en scripts.

## Escenarios

| # | Acción | Resultado esperado | Spec |
|---|---|---|---|
| 1 | Levantar con base vacía y revisar `SchemaMigraciones` | Scripts aplicados una vez con checksum; si un script falla, queda sin registrar y los servicios no arrancan | Constitución §6 |
| 2 | Reiniciar el runner / lanzar dos runners a la vez | No reaplica; el segundo espera o falla sin ejecutar | R-12 |
| 3 | Revisar tablas | 8 tablas; sin columna binaria del Excel; sin contraseñas en `database/` | FR-007, SC-007 |
| 4 | Revisar roles y usuarios iniciales; reiniciar `ms-autenticacion` | Un usuario por rol con hash; sin duplicados ni contraseñas sobrescritas | FR-001–005 |
| 5 | Reservar periodo con previa Completado / con previa Admitido | Rechazado / Bloqueado, con incidencia y sin reserva | US2-2,3 |
| 6 | Reservar de nuevo desde la misma carga | Mismo resultado, sin filas nuevas | US2-4, US5-5 |
| 7 | Dos reservas simultáneas del mismo periodo | Solo una Admitido; la otra Bloqueado/Rechazado | US2-6 |
| 8 | Insertar un periodo Rechazado/Bloqueado/Fallido con el mismo periodo | Permitido (no reserva); Admitido/Completado duplicado: rechazado por la base | índice filtrado |
| 9 | Dos productos con el mismo código: "p0117" y "P0117"; " P0117 " | Códigos iguales; el segundo se reporta Existente; espacios internos se conservan | US3-7, FR-044 |
| 10 | Código solo con espacios | Fila omitida como ausente | US4-8 |
| 11 | Archivo con el código X en la fila 3 (periodo B) y la fila 5 (periodo A), con A procesado primero | Gana la fila 3 de B | US3-6, FR-045 |
| 12 | Colisión concurrente de un código | La fila perdedora queda Existente; el grupo no se rechaza | US3-5, FR-046 |
| 13 | Forzar que la colisión persista hasta agotar los reintentos | Incidencia ErrorTecnico, periodo y carga Fallido, reservas liberadas, periodos Completado intactos | US3-8, US5-1 |
| 14 | Fallo técnico en P2 de P1-P2-P3 (P1 ya Completado) | P1 conservado; P2 Fallido sin productos ni incidencias de negocio; P3 NoProcesado (“No procesado por interrupción de la carga”) con reserva liberada; carga Fallido; nueva carga de P1 → Rechazado; de P2 y P3 → permitido | US5-1,2,3,12; FR-048 |
| 15 | Reiniciar el consumidor con cargas Pendiente legítimas | Siguen Pendiente; ninguna pasa a Fallido | US5-7 |
| 16 | Matar el proceso con una carga En proceso (P1 Completado, P2 sin confirmar) y reiniciar | Recuperación antes de consumir; P1 conservado; P2 reanudado sin duplicados | US5-8 |
| 17 | Matar el proceso durante la recuperación y reiniciar | Mismo resultado final; sin duplicados; Fallido solo si se agota `IntentosProceso` | US5-9 |
| 18 | Arrancar dos instancias del consumidor | Solo una obtiene el cerrojo y procesa/recupera | US5-10 |
| 19 | Reentregar el mensaje de una carga ya recuperada | Se descarta; sin cambios de estado ni duplicados | US5-11 |
| 20 | Recorrer los estados; intentar transiciones fuera de la matriz | Historial ordenado Pendiente→…→Notificado; transiciones inválidas y desde terminales no escriben nada | US6-3, US7-6 |
| 21 | Fallar el paso Cargado → Finalizado y recuperar | Conserva Cargado; finaliza sin reprocesar productos | US7-5 |
| 22 | Fallar solo el envío del correo | Conserva Finalizado con ErrorNotificacion; luego Notificado tras el envío | US7-3 |
| 23 | Filas con periodo inválido, precio no numérico, vacías, sin periodo/código | Incidencias FilaOmitida con motivo; vacías sin incidencia; ausente → valor por defecto, inválido → omitida | US4 |
| 24 | Operador y Administrador consultan cargas e incidencias | Operador solo las propias; Administrador todas | US8 |
| 25 | Precio 10.005 (o con más de 16 dígitos enteros) y nombre de más de 200 caracteres | Filas omitidas con motivo; sin redondeo ni truncamiento | FR-047 |
| 26 | Código X ganador en P-B (fila 3) y duplicado en P-A (fila 5); revertir P-B | Sin Existente para la fila 5 (sin producto confirmado); reportada NoProcesado; nueva carga la evalúa de nuevo | US3-9; FR-049 |
| 27 | Código con tabulador o espacios alrededor; "á" frente a "a" | Se recortan los blancos con `Trim()`; "á" y "a" son códigos distintos | FR-044 |

Los escenarios 11–19, 21–23 dependen del código de las features de Carga Masiva, Notificaciones y Control; aquí se verifican las garantías de la base. Los escenarios 14 y 26 que dependen del consumidor se verifican cuando exista el código de Carga Masiva.
