# Contratos de persistencia

> Los procedimientos se conservan y se incorporarán mediante SQL dentro de las migraciones EF Core de ms-control (referencia: `referencias/V002__procedimientos.sql`).

Esta feature no define endpoints. Los contratos son: dos procedimientos almacenados, el cerrojo de exclusividad del consumidor y el almacenamiento de identidad. Descripción de responsabilidades, no SQL. Diseño del plan; solo el hash y las columnas `IntentosProceso`/`PeriodoReservado` están aceptados.

## Regla común sobre transacciones

- Los procedimientos MUST invocarse **fuera** de cualquier transacción abierta de la aplicación. Si encuentran una transacción activa, fallan de inmediato con un error técnico, para evitar transacciones anidadas incompatibles.
- Cada procedimiento abre y confirma su propia transacción.
- La transacción EF por grupo de periodo contiene **solo** inserciones de Producto e Incidencia de negocio y el paso del periodo a Completado; no llama a procedimientos. Responsabilidades sin duplicar: los procedimientos no insertan productos ni cierran periodos; la aplicación no decide reservas ni transiciones de estado.

## usp_ReservarPeriodo

**Responsabilidad**: evaluar y registrar de forma atómica el resultado de admisión de un periodo para una carga.

| Parámetro | Descripción |
|---|---|
| CargaId (entrada) | carga actual (debe estar En proceso) |
| Periodo (entrada) | AAAA-MM ya validado |
| Resultado (salida) | Admitido, Completado, Rechazado o Bloqueado |

**Comportamiento**:
1. Si ya existe un resultado de (CargaId, Periodo), lo devuelve sin cambios (reentrega y recuperación idempotentes; un Admitido propio se reanuda).
2. Excluyendo la carga actual: otro periodo igual Completado → registra Rechazado; otro Admitido → registra Bloqueado; en otro caso registra Admitido con PeriodoReservado.
3. Rechazado y Bloqueado inserta también la incidencia de grupo, en la misma transacción, sin reservar.
4. Si la inserción choca con el índice único filtrado, reevalúa **una vez**; si vuelve a chocar, no asume que sea imposible: falla con error técnico para que lo trate la aplicación (no se queda en bucle).

**No hace**: leer el Excel, tocar productos, cambiar el estado de la carga.

## usp_CambiarEstadoCarga

**Responsabilidad**: aplicar una transición de la matriz de `data-model.md` y registrar su historial de forma atómica.

| Parámetro | Descripción |
|---|---|
| CargaId (entrada) | carga a cambiar |
| NuevoEstado (entrada) | uno de los seis estados |
| PeriodoFallido (entrada, opcional) | AAAA-MM del periodo donde ocurrió el fallo terminal; NULL si no se identifica (solo se usa al pasar a Fallido) |
| Cambiado (salida) | 1 si se aplicó; 0 si la transición es inválida o es una repetición |

**Comportamiento**:
1. Valida el origen contra la matriz; Notificado y Fallido son terminales.
2. Actualiza `Estado` y, al llegar a Finalizado, `FechaFin`; inserta la fila de historial.
3. Al pasar a Fallido: el periodo indicado en `PeriodoFallido` (si es Admitido) queda Fallido; los demás periodos Admitido de esa carga quedan NoProcesado, y todos pasan a `PeriodoReservado` NULL (se liberan las reservas); los Completado se conservan. Los NoProcesado llevan la incidencia “No procesado por interrupción de la carga”, insertada en la misma transacción (sin afirmar que fallaron).
4. Una repetición no duplica historial.

**No hace**: registrar incidencias técnicas (las inserta la aplicación tras revertir), contar intentos ni enviar correos.

> **Precisado por la Feature 005 (2026-11-02)**: un script versionado nuevo añade un parámetro **opcional** de estado de origen esperado. Sin él, el comportamiento descrito arriba no cambia. Con él, si el estado actual no coincide, devuelve `Cambiado` = 0 sin actualizar la carga, sin historial y sin tocar periodos; la comprobación y el cambio son atómicos. Lo usa ms-control para pasar a Fallido solo desde Pendiente. Ver `specs/005-recepcion-registro-cargas/spec.md` FR-032.

## Cerrojo de exclusividad del consumidor de carga masiva

- Recurso con nombre fijo, modo exclusivo, propietario de sesión, sin espera (timeout 0), adquirido con el procedimiento de sistema de bloqueos de aplicación de SQL Server (`sp_getapplock`; mecanismo conocido, **no verificado en documentación en esta sesión**).
- Se mantiene en una **conexión dedicada, sin pool**, abierta durante toda la vida del consumidor. Si el proceso muere, la conexión se cierra y SQL Server libera el cerrojo.
- Una instancia que no lo obtiene no consume ni recupera.
- Antes de cada grupo, el consumidor comprueba que su conexión del cerrojo sigue viva; si no, se detiene sin confirmar (límites en research R-02).

## Almacenamiento de identidad (contrato para ms-autenticacion)

- `Usuario(Email, PasswordHash, RolId)`; el hash lo genera ms-autenticacion con `PasswordHasher` de ASP.NET Core Identity (sin PBKDF2 manual) y se guarda como una sola cadena autodescriptiva. Sin texto plano ni cifrado reversible. Esta feature solo define el almacenamiento.
- Inicialización de los usuarios de demostración al arrancar: lee credenciales de configuración externa; crea el usuario solo si no existe (clave: Email); **nunca sobrescribe** una contraseña existente; es repetible.
- Los roles Administrador y Operador son datos de referencia sin secretos y los inserta la migración inicial.
- La longitud de salida del hash se confirma en la feature de Autenticación (`VARCHAR(255)` es la cota propuesta).
