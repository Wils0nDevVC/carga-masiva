# Research: Persistencia y reglas de integridad para la carga masiva

> **Nota (Feature 003, 2026-11-02)**: el runner de scripts de `database/` **vuelve a ser el mecanismo activo** de gestión del esquema (constitución v3.0.0; ver `specs/003-runner-sql-refactor-control/`), sustituyendo a las migraciones EF Core de `ms-control` que mencionan las notas de estrategia sustituida de este documento. Las reglas de negocio, el modelo de datos y los contratos de procedimientos no cambian. El texto original se conserva como historial.

Procedencia: **Dec** = decisión del usuario; **Req** = README; **Prop** = propuesta de este plan (las aceptadas por el usuario se indican como Dec). El consumidor, las migraciones y Autenticación se implementan en sus features; aquí se diseña el soporte de persistencia.

## R-01 Una base compartida
- **Decisión** (Prop): una base SQL Server para los cuatro servicios; cada servicio mapea en su `Acity.Infrastructure` solo las tablas que usa, sin biblioteca de entidades compartida (ejecutables independientes). Base única según el diagrama del README.

## R-02 Exclusividad y recuperación de ejecuciones interrumpidas

**Aprobado (Dec)**: una sola instancia activa del consumidor, procesamiento secuencial de mensajes y grupos, recuperación antes de consumir nuevos mensajes, periodos completados conservados, sin marcar Fallido por reiniciar; sin servicios de barrido, Cronos ni Redis.

**Mecanismo propuesto (Prop)**: un **cerrojo de aplicación de SQL Server de ámbito de sesión** (contrato en `contracts/`), más las restricciones de la base como red de seguridad. Reutiliza la infraestructura existente (SQL Server).

1. **Cómo se impide que dos instancias procesen o recuperen a la vez.** Al arrancar, antes de suscribirse a RabbitMQ y antes de recuperar, la instancia pide el cerrojo exclusivo sin espera. Quien no lo obtiene no consume ni recupera. Procesar y recuperar comparten el mismo cerrojo, por lo que no pueden solaparse.
2. **Cómo se identifica una ejecución interrumpida.** Con el cerrojo en mano, ninguna otra instancia procesa. Por tanto, toda carga **En proceso** (o **Cargado**, a la espera de Finalizado) que se encuentre en ese momento es, por construcción, una ejecución interrumpida: nadie la está ejecutando. No hace falta heartbeat ni fecha límite.
3. **Cómo se diferencia de una carga Pendiente que espera su mensaje.** Por el estado: Pendiente significa "registrada, sin iniciar" y no tiene periodos reservados (los periodos se conocen y reservan solo al empezar a procesar, en En proceso). La recuperación nunca toca cargas Pendiente; siguen esperando su mensaje.
4. **Cómo se recuperan o liberan las reservas.** Se **reanuda** la carga (no se libera a ciegas): los periodos Completado se omiten; un Admitido propio se retoma (su transacción nunca se confirmó, no hay datos parciales); los pendientes se procesan. Las reservas solo se liberan por Fallido: si la reanudación termina en un fallo técnico terminal (p. ej. archivo inaccesible tras los reintentos, o intentos agotados), `usp_CambiarEstadoCarga(Fallido)` marca los periodos Admitido como NoProcesado (el periodo que falló, si se identifica, como Fallido) y libera sus reservas.
5. **Si el proceso vuelve a caer durante la recuperación.** La recuperación es idempotente: vuelve a ejecutarse en el siguiente arranque con el mismo resultado. Para no entrar en un ciclo de caídas por una carga que nunca puede completarse, `CargaArchivo.IntentosProceso` cuenta las ejecuciones iniciadas (la aplicación lo incrementa con una actualización de una sola sentencia al iniciar o reanudar cada ejecución, no un procedimiento); al superar el límite (valor fijado en la feature de Carga Masiva) la carga pasa a Fallido y libera sus reservas. La columna `IntentosProceso` está aceptada por el usuario con esa única responsabilidad (acotar la recuperación).
6. **Coordinación con la reentrega de RabbitMQ.** El consumidor confirma (ack) un mensaje solo después de persistir el resultado de la carga. Si cae antes, RabbitMQ lo reentrega. Como la recuperación se hace antes de suscribirse, el mensaje reentregado llega a una carga ya recuperada: el consumidor consulta su estado y, si no es Pendiente, lo confirma y descarta sin reprocesar (FR-022). Si la carga sigue Pendiente, se procesa con normalidad.

**Límites (honestos)**:
- Una sola réplica configurada **no** es garantía; el cerrojo sí es la garantía principal, pero depende de la conexión: si SQL Server reinicia o la conexión cae, el cerrojo se pierde. Por eso el consumidor comprueba antes de cada grupo que su conexión del cerrojo sigue viva y, si no, se detiene sin confirmar.
- Un proceso "zombi" (pausa larga, partición de red) podría seguir un instante tras perder el cerrojo. Aun así la base lo contiene: `UNIQUE (CargaId, Periodo)`, el índice filtrado de periodo y la unicidad de `CodigoProducto` impiden duplicados; el peor caso es trabajo repetido que la base rechaza (se trata como colisión, R-06).
- No cubre la pérdida de un mensaje de una carga Pendiente (corresponde a la durabilidad de RabbitMQ y a la feature de Control).
- Efecto sobre la regla "Pendiente/En proceso bloquea" del README: con un único consumidor secuencial y recuperación previa, el bloqueo solo se observaría si falla la exclusividad; se conserva como protección (usp_ReservarPeriodo lo implementa), pero no será el camino habitual.
- Verificado en documentación oficial (sp_getapplock, 2-nov-2026): `@LockOwner = 'Session'` libera el cerrojo al cerrar la sesión y todos los cerrojos se liberan si el servidor se apaga; `@LockTimeout = 0` devuelve -1 si no se puede conceder de inmediato; el recurso se califica por la base de datos actual (el consumidor y el runner deben usar la misma base); `@Resource` distingue mayúsculas. **Sigue sin verificar** cómo comprobar la vigencia del cerrojo desde la aplicación (feature de Carga Masiva) y el uso de una conexión dedicada sin pool.

## R-03 Estados y transiciones; transitorio frente a terminal
- **Matriz** (Dec): ver `data-model.md`. Pendiente y En proceso → Fallido; Cargado solo → Finalizado; Notificado y Fallido terminales; sin estados nuevos. Historial: cada transición aplicada inserta una fila; repetir una transición no duplica.
- **Error transitorio recuperable** (red, tiempo de espera, SeaweedFS o SQL temporalmente inaccesibles, deadlock): no cambia el estado; lo reintenta quien opera (Polly) y, si se resuelve, el flujo continúa.
- **Fallo técnico terminal** (Dec): se agotaron los reintentos de esa operación, o el error no es reintentable. Se inserta la incidencia ErrorTecnico y se pasa a Fallido (Pendiente o En proceso). El archivo **se detiene**: los periodos Completado se conservan; los periodos pendientes (Admitido) pasan a **NoProcesado** con una incidencia “No procesado por interrupción de la carga” (no se afirma que fallaron) y sus reservas se liberan; solo el periodo que falló queda Fallido. Un rechazo o bloqueo de negocio no detiene los demás periodos.
- **Caída del proceso**: no es por sí misma Fallido; se recupera (R-02) y cuenta un intento.
- **Cargado → Finalizado falla**: se conserva Cargado; la recuperación completa solo la finalización (no reprocesa productos). **Correo falla**: se conserva Finalizado, se registra ErrorNotificacion y se reintenta solo la notificación.
- **Archivo ilegible** (Dec): formato o contenido inválido = incidencia de entrada, sin reintentos técnicos; falla de descarga o almacenamiento = técnica. El tratamiento operativo se define en la feature de procesamiento (Carga Masiva).
  - **Precisado por la Feature 006 (2026-11-03, decisión autorizada del usuario)**: archivo inválido → incidencia `ArchivoInvalido`, sin productos ni reintentos, recorrido En proceso → Cargado → Finalizado y notificación; archivo inexistente en SeaweedFS → fallo técnico de recuperación (`ErrorTecnico`, Fallido), sin reintentar una ausencia confirmada. Notificación no confirmada → Finalizado + `ErrorTecnico`, sin recuperación automática. Ver `specs/006-procesamiento-cargas/spec.md`.

## R-04 Disponibilidad de periodo y reserva
- **Decisión** (Dec + Prop): `CargaPeriodo` con `Resultado` y la columna `PeriodoReservado` con índice único filtrado `IS NOT NULL` (condición y transacciones en `data-model.md`). Equivalencia con el README §3: una carga previa Cargado/Finalizado/Notificado ⇔ periodo Completado → Rechazado; Pendiente/En proceso ⇔ Admitido → Bloqueado; un periodo Completado rechaza aunque su archivo termine Fallido (Dec).
- **Verificado en documentación**: los índices filtrados admiten solo comparaciones simples, pueden ser UNIQUE y no admiten columnas calculadas en el filtro. **No confirmado**: `IN`/`OR` en el filtro, por lo que se evita.
- Rechazado, Bloqueado, Fallido y NoProcesado no reservan; una carga Fallido libera sus Admitido (los pasa a NoProcesado, salvo el periodo que falló).

## R-05 Orden original del Excel frente a atomicidad por periodo
Agrupar por periodo no cambia qué fila gana si la decisión se toma **antes** de persistir y sobre **todo el archivo**:
1. **Evaluar**: se lee el archivo en orden, se normalizan y validan las filas y se reserva cada periodo (`usp_ReservarPeriodo`), con resultado Admitido, Rechazado o Bloqueado.
2. **Decidir ganadores** (en memoria): entre las filas válidas de los periodos Admitido, para cada código (comparación R-07) gana la de menor número de fila del Excel, aunque los periodos estén intercalados. Las demás candidatas son **provisionalmente** duplicadas; esa decisión aún no es Existente. Además, los códigos ya presentes en la base provenientes de **otras** cargas confirmadas son Existente (se excluyen los productos de la propia carga).
3. **Persistir por grupo**: una transacción por periodo con los productos ganadores, las incidencias de negocio y el paso a Completado. **Existente solo se registra con un producto confirmado**: la incidencia Existente de un duplicado se inserta en la **misma transacción que el producto ganador** (aunque pertenezca a otro periodo, asociada al periodo del duplicado) o, si el producto ya existía, en el grupo de la fila. El orden en que se procesan los grupos ya no altera el resultado.
- **Reentrega/recuperación**: los periodos Completado se omiten; los restantes recalculan el mismo resultado de forma determinista (las filas perdedoras ante un ganador ya persistido de la misma carga siguen siendo Existente, igual que en la primera ejecución). Un ganador no persistido no aparece en la base, así que nunca se reporta Existente por reentrega.
- **Fallo del grupo ganador** (Dec): si el grupo ganador se revierte, también se revierten sus resultados provisionales (incluidos los Existente que insertó); no queda ningún Existente sin producto. Las filas duplicadas que no llegaron a resolverse se reportan **no procesadas** (incidencia `NoProcesado`, motivo “ganador no persistido”). En una nueva carga se evalúan de nuevo contra los datos confirmados.

## R-06 Colisión concurrente de CodigoProducto
- Distinguir la causa: se trata como colisión de código solo un error de SQL Server de violación de unicidad (números 2601 o 2627, según sea índice o restricción) **cuyo nombre de índice corresponde al de `CodigoProducto`**; cualquier otro error (otro índice único, FK, CHECK) es un error distinto y sigue su tratamiento técnico. Verificación pendiente de los números y del texto durante la implementación.
- Flujo: revertir la transacción del grupo → releer los códigos existentes (otras cargas) → recalcular qué filas son Existente → reintentar la transacción. Una colisión de una fila **nunca** rechaza el grupo.
- No se asume que una segunda colisión sea imposible: el bucle está acotado por el límite de reintentos técnicos de la feature de Carga Masiva.
- **Agotado** (Prop): se inserta la incidencia ErrorTecnico del periodo (tras revertir), el periodo pasa a Fallido, la carga pasa a Fallido (Dec: fallo terminal) y `usp_CambiarEstadoCarga` libera las demás reservas. Sin bloqueo permanente.
- Con un consumidor único y secuencial la colisión es improbable; se mantiene por la red de seguridad (R-02, procesos zombi) y por la diferencia entre la comparación en memoria y la collation (R-07).

## R-07 Comparación de CodigoProducto
- **Aprobado (Dec)**: sin distinguir mayúsculas; blancos iniciales y finales eliminados con `Trim()` de .NET antes de comprobar longitud y unicidad; vacío tras limpiar = ausente; internos conservados; sin otras transformaciones ni equivalencias adicionales (p. ej. acentos).
- **Estrategia** (Prop, sujeta a la condición aprobada): la aplicación hace `Trim()` y compara con `OrdinalIgnoreCase`; la columna usa collation explícita `Latin1_General_100_CI_AS_WS`: insensible a mayúsculas, sensible a acentos y, por el sufijo `_WS`, sensible al ancho de caracteres (sin `_WS`, SQL Server trataría como iguales caracteres de ancho completo y medio). El significado de las banderas de collation proviene de conocimiento previo; **verificar el nombre exacto contra la documentación al escribir el script**. Se almacena el código limpio con la capitalización de la primera fila aceptada.
- **Efectos relevantes**: (a) SQL Server ignora espacios finales al comparar; tras `Trim()` no afecta; (b) no hay equivalencia de acentos (á ≠ a) ni de ancho completo; (c) `OrdinalIgnoreCase` y la collation pueden discrepar en caracteres excepcionales, y la base manda: la discrepancia se manifiesta como colisión y termina en Existente por R-06; (d) el nombre por defecto usa el código ya limpio.
- "Espacios" (Dec): los que elimina `Trim()` de .NET (blancos Unicode iniciales y finales).

## R-08 Tipos, longitudes y precisión
- Tabla completa en `data-model.md`. Longitudes: propuestas del plan, no deducidas del Excel; sin `MAX`.
- **Precio DECIMAL(18,2)** (Dec). SQL Server **redondea sin avisar** al convertir un decimal con más escala a (18,2) y falla solo por desbordamiento: la base no protege contra el redondeo. Por tanto la validación debe estar en la aplicación.
- **Aprobado (Dec)**: un precio que no pueda representarse exactamente en DECIMAL(18,2) (más de dos decimales con valor, o más de 16 dígitos enteros) omite la fila con su motivo (incidencia FilaOmitida); no se redondea ni se trunca.
- Riesgo funcional: Excel guarda números como binario de coma flotante (418.27 puede representarse con ruido). La lectura debe convertir a decimal por su representación decimal más corta, no por el valor binario exacto; de lo contrario precios válidos parecerían tener más de dos decimales. Se resuelve en la feature de Carga Masiva.
- **Textos que exceden la longitud definida** (código 100, nombre 200; Dec: mismo tratamiento): fila omitida con motivo; nunca truncar el dato de negocio. La longitud se comprueba tras el `Trim()`. En campos de auditoría (`Incidencia`) el valor se trunca de forma explícita y el Motivo lo indica.

## R-09 Polly y responsabilidades por feature
Los parámetros (cantidad, intervalos, errores reintentables, operaciones protegidas) se definen en las features de Control, Carga Masiva y Notificaciones (Dec). Esta feature fija lo siguiente:
- **Límites transaccionales**: una transacción EF por grupo de periodo; los procedimientos almacenados son transacciones propias, fuera de las transacciones de la aplicación; la reserva de periodo se confirma antes del grupo.
- **Operaciones repetibles sin duplicar**: reservar un periodo (devuelve el resultado existente); cambiar estado a uno ya alcanzado; crear usuarios iniciales; aplicar migraciones (por el historial de EF Core); reprocesar una carga (omite periodos Completado); reintentar solo la notificación.
- **Resultados que deben persistir ante un fallo**: periodos Completado; incidencia ErrorTecnico tras revertir; periodos pendientes NoProcesado; estado Fallido con su historial; ErrorNotificacion con la carga en Finalizado; IntentosProceso.
- **Responsabilidades futuras**: Control (crea la carga y el historial inicial, publica el mensaje); Carga Masiva (consumidor único, cerrojo, recuperación, reintentos, fases R-05, transición a Cargado/Finalizado/Fallido); Notificaciones (envío y Notificado, ErrorNotificacion); Autenticación (usuarios iniciales y hash).

## R-10 Identidad y usuarios iniciales
- Dec: identidad propia en SQL Server; roles Administrador y Operador; hash; usuarios iniciales por configuración externa, creados por ms-autenticacion al arrancar, repetible, sin sobrescribir contraseñas. Contrato de almacenamiento en `contracts/`.
- Dec (confirmado): asociación como `Usuario.RolId`, un solo rol por usuario, sin tabla puente.
- **Hash (Dec)**: `PasswordHasher` de ASP.NET Core Identity en ms-autenticacion, sin implementar PBKDF2 manualmente. Esta feature solo define el almacenamiento: una cadena autodescriptiva en `Usuario.PasswordHash`. La longitud de salida y la disponibilidad en .NET 10 se verifican en la feature de Autenticación; `VARCHAR(255)` es la cota propuesta.

## R-11 SQL Server y Docker: comprobaciones realizadas
Solo lecturas; no se instaló, descargó ni ejecutó nada.

| Hecho | Evidencia | Estado |
|---|---|---|
| Equipo de esta sesión: Apple M4 Pro, `arm64`, proceso nativo (no bajo traducción) | `uname -m`, `sysctl` | Comprobado |
| Docker Desktop 29.8.1 con servidor `linux/arm64` (aarch64), 14 CPU, ~8,3 GB de RAM para la VM | `docker version`, `docker info` | Comprobado |
| Este es el equipo de desarrollo; el equipo donde se evaluará **no se conoce** | — | Pendiente |
| Imágenes locales `mcr.microsoft.com/mssql/server:2022-latest` y `2022-CU27-ubuntu-22.04` son `linux/amd64` | `docker image inspect` | Comprobado |
| Existe un contenedor SQL Server 2022 (otro proyecto) en ejecución desde el 31-oct-2026 sobre este Docker Desktop | `docker inspect` | Comprobado como evidencia de viabilidad, **no** valida nuestro esquema |
| Microsoft: las imágenes de contenedor de SQL Server solo se soportan en hosts Linux x86-64 (Intel/AMD); la emulación o traducción (Rosetta 2, Prism, QEMU) **no está probada ni soportada** | learn.microsoft.com, "Docker: Run Containers for SQL Server on Linux" (actualizada el 14-oct-2026) | Documentación oficial |
| Las imágenes de SQL Server 2022 (CU14+) incluyen `mssql-tools18` en `/opt/mssql-tools18/bin` (sqlcmd ODBC); requisito mínimo de 2 GB de RAM | misma página | Documentación oficial |
| Existen dos `sqlcmd`: Go (independiente; corre en Windows, macOS, Linux y contenedores) y ODBC. Ambos admiten `-b` (salir con código de error) e `-i`; en ODBC `QUOTED_IDENTIFIER` está en OFF salvo `-I` | learn.microsoft.com, "sqlcmd utility" | Documentación oficial |
| Disponibilidad de `sqlcmd` Go para linux/arm64; disponibilidad de una imagen SQL Server nativa arm64; si Rosetta está activo en Docker Desktop (no consta clave en `settings-store.json`) | — | **No comprobado** |
| `sqlcmd` no está instalado en el equipo | `which sqlcmd` | Comprobado |

**Compatibilidad del motor frente a la del ejecutor**: son independientes. El motor (amd64, emulado en Apple silicon) es el riesgo principal y no se resuelve con otro ejecutor; un runner .NET tampoco lo arregla. Para el ejecutor, la opción de menor dependencia nueva es usar la misma imagen del servidor como base del runner (ya incluye `sqlcmd` y se descarga una sola vez), que sufre la misma emulación que el motor. Esto es una propuesta, no se ha ejecutado. No se cambia el motor aprobado ni se añade un runner .NET.

**Verificación operativa realizada (2-nov-2026, este equipo)**: con `platform: linux/amd64` explícito, la imagen `mcr.microsoft.com/mssql/server:2022-CU27-ubuntu-22.04` (SQL Server 16.0.4295.3) arrancó emulada, quedó `healthy`, aceptó conexiones, y el runner (imagen construida sobre la misma base, con `sqlcmd` de `mssql-tools18`) aplicó V001 y V002. Esto es evidencia para **este** equipo y estas pruebas; Microsoft sigue sin soportar la emulación y no se ha probado el equipo de evaluación, así que no se generaliza. Notas: en el equipo hay otros contenedores SQL Server (otros proyectos, y uno llamado `carga-masiva-sqlserver` que no creó esta implementación y que no se tocó); el puerto del host es 14333 para no chocar con el 1433/1434 ya usados.

**Hallazgo del runner (implementación anterior)**: la imagen copia los scripts al construirse; tras editar un script hay que reconstruir (`docker compose build database-runner`) o el runner aplica la versión anterior.

## R-12 Runner de scripts versionados (SUSTITUIDO)
> **Estrategia de despliegue sustituida (constitución v2.0.0)**: el esquema ya no se aplica con el runner de scripts de `database/`. Lo gestionan migraciones EF Core de las que `ms-control/Acity.Infrastructure` es único propietario, aplicadas automáticamente al iniciar ms-control antes de atender solicitudes; los demás servicios usan EF Core sin migraciones. Se conservan las reglas de negocio, el modelo de datos y los contratos de procedimientos. `database/` queda como implementación anterior fuera del flujo activo; `referencias/V001__esquema_inicial.sql` y `referencias/V002__procedimientos.sql` son la referencia de los mapeos EF, de la migración inicial y de los procedimientos (que se incorporarán mediante SQL dentro de las migraciones). Los detalles de las migraciones los define la feature de ms-control. Donde este documento diga "script", "runner" o `database/`, léase como diseño anterior.

Lo siguiente documenta el diseño y la implementación anteriores, conservados como historial; no es el flujo activo.
- **Único responsable**: un runner en `database/` (contenedor de un solo uso que Compose lanza antes de los microservicios).
- **Evita ejecuciones simultáneas**: mantiene un cerrojo de aplicación de SQL Server (misma técnica que R-02, otro recurso) durante toda su sesión; las llamadas sucesivas a scripts se hacen dentro de **una sola sesión** para conservar el cerrojo.
- **Orden y registro**: scripts `Vnnn__descripcion.sql` aplicados en orden; antes de cada uno consulta `SchemaMigraciones`; lo ya aplicado se omite; si el checksum de un aplicado cambió, falla (sin reescritura silenciosa); un script nuevo con número menor que el último aplicado también falla.
- **Script fallido**: cada script corre en una transacción con `XACT_ABORT` y sale con código de error (`-b`); la transacción se revierte, no se registra como aplicado, el runner termina con error y los servicios dependientes no arrancan; reejecutar reintenta ese script desde el principio.
- Cada script empieza con `SET QUOTED_IDENTIFIER ON` (R-11).
- Implementado en `database/run-scripts.sh` con `database/bootstrap.sql` (crea la base y `SchemaMigraciones` de forma idempotente, antes de consultar lo aplicado; V001 no crea `SchemaMigraciones`). Los scripts pendientes se encadenan en un único archivo maestro con `:on error exit` y cada uno entre `BEGIN TRANSACTION`/`COMMIT`; el registro en `SchemaMigraciones` va dentro de la transacción del script. Secretos: solo `MSSQL_SA_PASSWORD` por variable de entorno (`.env` fuera de Git).

## R-13 Alcance del modelo
Justificación tabla por tabla en `data-model.md`. Cambios: se elimina `UsuarioRol` (sin requisito de varios roles); se añaden `PeriodoReservado` (necesaria para el índice filtrado) e `IntentosProceso` (acota la recuperación); ambas aceptadas por el usuario con esas responsabilidades y sin mecanismos nuevos. Sin tablas preventivas. El resultado de periodo incorpora `NoProcesado` (decisión de no afirmar que los periodos pendientes fallaron).

## R-14 Gestión del esquema con EF Core Migrations (Dec)
- **Decisión**: `ms-control/Acity.Infrastructure` es el único propietario de las migraciones de toda la base compartida y las aplica automáticamente al iniciar ms-control, antes de atender solicitudes. Los demás servicios mapean con EF Core sin generar ni aplicar migraciones.
- **Referencias**: `referencias/V001__esquema_inicial.sql` y `referencias/V002__procedimientos.sql` (no se ejecutan) orientan los mapeos y la migración inicial; los procedimientos se incorporan mediante SQL dentro de las migraciones. El historial de migraciones lo lleva EF Core, por lo que `SchemaMigraciones` deja de ser parte del modelo.
- **Pendiente para la feature de ms-control (sin verificar aquí)**: cómo expresar en migraciones el índice filtrado, la collation `Latin1_General_100_CI_AS_WS`, los CHECK y los procedimientos; el comportamiento de `GO` en SQL embebido; la concurrencia si hay más de una instancia de ms-control; y el orden de arranque respecto de los demás servicios.

## Abiertos
Resueltos por decisión del usuario: precisión y longitudes (R-08), fallo terminal (R-03), duplicados con ganador revertido (R-05), "espacios" (R-07), archivo ilegible (R-03), notificaciones no publicadas (feature del servicio que publica), hash (R-10), `IntentosProceso` y `PeriodoReservado` (R-13) y condición de la collation (R-07).

Quedan, sin bloquear la creación de la base:
1. (Resuelto) Eliminación de `UsuarioRol`: el usuario confirmó un solo rol por usuario mediante `Usuario.RolId`.
2. Equipo de evaluación: se desconoce; el motor amd64 corre emulado en Apple silicon (R-11) y su soporte no está respaldado por Microsoft.

Resuelto en la implementación: el nombre `Latin1_General_100_CI_AS_WS` está verificado en la documentación oficial ("Windows collation name": `CI` = no distingue mayúsculas, `AS` = sensible a acentos; omitir `WS` = insensible al ancho, `WS` = sensible al ancho; orden `CI_AS_[KS]_[WS]`) y SQL Server lo aceptó al crear la columna.
