# Quickstart: validación manual de las consultas

Verificación manual (constitución §11). Contrato en `contracts/consultas-api.md`. **Ningún escenario se ha ejecutado todavía.**

## Reglas

- Los datos de prueba van solo en una base temporal `CargaMasivaApp_F010`, con toda la pila apuntando a ella. `CargaMasiva`, `CargaMasivaDb` y los volúmenes ajenos no se tocan; nunca se usa `docker compose down -v`.
- Los secretos solo se pasan por entorno y no se imprimen tokens ni contraseñas.
- Un segundo Operador se crea en la base temporal por SQL, con un hash generado por `PasswordHasher` fuera del repositorio.
- Antes y después de las consultas se toma una huella (recuentos y estados) de `CargaArchivo`, `CargaEstadoHistorial`, `CargaPeriodo`, `Producto` e `Incidencia` para SC-006.

## Preparación

1. `DB_NAME=CargaMasivaApp_F010 docker compose up -d --build`.
2. Subir con el Operador A el Excel de ejemplo y un Excel con filas que el procesamiento omite. Subir con el Operador B un Excel propio.
3. Esperar a que las cargas lleguen a Notificado.

## Escenarios

| # | Escenario | Resultado esperado | Spec |
|---|---|---|---|
| 1 | `GET /cargas` como Administrador, Operador A y Operador B | El Administrador ve todas; cada Operador solo las suyas; orden descendente | US1-1, US1-2, SC-001 |
| 2 | `GET /cargas` con un usuario sin cargas | 200 con `[]` | US1-3, SC-004 |
| 3 | Sin token o con token inválido (Gateway y Control) | 401 | US1-4 |
| 4 | 20 `GET /cargas` seguidos y luego `POST /cargas` | Ningún 429; la subida se acepta (202) | US1-5, SC-005 |
| 5 | `GET /cargas/{id}` de una carga procesada | Los recuentos de historial, periodos e incidencias coinciden con la base | US2-1, SC-003 |
| 6 | Detalle de una carga Pendiente (insertada por SQL en la base temporal, sin mensaje) | `periodos` e `incidencias` vacíos; historial con Pendiente | US2-2, SC-004 |
| 7 | El Operador B pide el detalle y el contenido de una carga del Operador A; cualquiera pide `id` 999999 | 404 «La carga no existe», con cuerpos idénticos | US2-3, US3-4, SC-002 |
| 8 | `id` 0 y `id` `abc` | 400 y 404 respectivamente | US2-5 |
| 9 | `GET /cargas/{id}/contenido` del Excel de ejemplo | 200 filas de datos; los valores coinciden con el archivo leído aparte, incluidas las filas omitidas | US3-1, US3-2, SC-001a |
| 10 | Contenido de una carga cuyo archivo se reemplazó en SeaweedFS por texto no Excel | 422 | US3-5, SC-001b |
| 11 | Contenido con el archivo borrado de SeaweedFS | 500 «No se pudo recuperar…» y log Error con `idCarga` | US3-6a |
| 12 | Contenido con `seaweedfs` detenido | 503 tras los reintentos; después se vuelve a arrancar | US3-6 |
| 13 | Huella antes y después de todos los escenarios | Sin cambios | SC-006 |
| 14 | Las consultas 1, 5, 7 y 9 a través del Gateway y directas | Mismos estados y cuerpos | SC-007 |
| 15 | `grep -F` de los tokens emitidos y de valores de celdas del Excel en los logs de Control y del Gateway | 0 coincidencias de tokens; 0 contenidos de celda | FR-014 |

## Cierre

Borrar solo los archivos y correos de esta prueba, eliminar `CargaMasivaApp_F010`, volver al arranque normal sobre `CargaMasivaApp` y comprobar que todo está `healthy`. Registrar en "Resultados" lo ejecutado y lo no ejecutado.

## Resultados (implementación, 2026-11-03)

**Estado: implementada y verificada en ejecución** sobre `CargaMasivaApp_F010`, con toda la pila apuntando a esa base.
- Datos: Operador A (inicial), Operador B (creado por SQL con hash `PasswordHasher`; contraseña aleatoria fuera del repositorio, ya borrada) y Administrador.
- Cargas: 1 (A, Excel de ejemplo), 2 (A, `casos.xlsx` con casos límite), 3 (B, ejemplo), 4 (A, Pendiente por SQL con ruta sin archivo), 5 (A, Pendiente por SQL con ruta a un texto no Excel) y 6 (B, subida del escenario 4).
- Validación en el equipo de evaluación: no realizada.

| # | Resultado |
|---|---|
| 1 | El Administrador ve [5, 4, 3, 2, 1]; el Operador A, [5, 4, 2, 1]; el Operador B, [3], en orden descendente. Campos: `idCarga`, `nombreArchivo`, `usuario`, `fechaRecepcion`, `estado`, `fechaFin`. Fechas con `Z` (Abierto 1 confirmado) |
| 2 | Verificación complementaria (ver abajo): un Operador C sin cargas recibe 200 `{"data":[],"isSuccess":true,"message":"OK","code":200,"errors":null}` a través del Gateway y directo; la carga del Operador A no aparece |
| 3 | Sin token por el Gateway: 401; sin token directo a Control: 401; token alterado: 401 |
| 4 | 20 `GET /cargas` seguidos: todos 200, ningún 429; la subida posterior da 202 |
| 5 | Detalle de la carga 1: historial Pendiente → En proceso → Cargado → Finalizado → Notificado; 3 periodos y 84 incidencias, iguales a los recuentos de la base |
| 6 | Carga 4 (Pendiente): historial [Pendiente], `periodos` [], `incidencias` [], `fechaFin` null |
| 7 | El Operador B pide el detalle y el contenido de la carga 1 de A, y el Operador A pide la carga 999999 (detalle y contenido): 404 con cuerpos idénticos `{"data":null,"isSuccess":false,"message":"La carga no existe.","code":404,"errors":null}`. El Administrador ve la carga 1: 200 |
| 8 | `id` 0: 400 «El identificador de la carga debe ser mayor que 0.»; `id` `abc`: 404 «No encontrado» |
| 9 | Ejemplo: hoja "Productos", 4 encabezados, 200 filas. **0 diferencias** frente a un lector independiente (zip + XML, en Python) |
| 9 (casos límite) | Se mantienen: espacios (`"  Con espacios  "`), celdas vacías como null, fila vacía intermedia (4) como `[null, null, null, null]`, precio de texto `"abc"`, periodo inválido `"31-1"`, código duplicado, booleano `"true"`, fecha `"2031-01-15T10:30:00"` y un número con formato `0.00` devuelto como `"7.123456"` (sin formato visual). Las filas omitidas por el procesamiento aparecen igual que las insertadas |
| 10 | Carga 5 (texto no Excel): 422 «El archivo no se puede leer como Excel.» y log Warning con `IdCarga` |
| 11 | Carga 4 (sin archivo): 500 «No se pudo recuperar el archivo asociado a la carga.» y log Error con `IdCarga 4` |
| 12 | Con `seaweedfs` detenido: 3 reintentos y después 503 «El archivo no está disponible temporalmente. Intente nuevamente más tarde.» en 5,8 s, con log Warning. El detalle sigue funcionando (200). SeaweedFS se volvió a arrancar |
| 13 | La huella (`CHECKSUM_AGG`) de `CargaArchivo`, `CargaEstadoHistorial`, `CargaPeriodo`, `Producto` e `Incidencia` para las cargas 1–5 es idéntica antes y después de todas las consultas |
| 14 | `/cargas`, `/cargas/1`, `/cargas/999999` y `/cargas/1/contenido`: mismos estados y cuerpos a través del Gateway y directo. Abierto 3 confirmado: Ocelot distingue `/cargas/{id}` de `/cargas/{id}/contenido` sin `Priority` |
| 15 | 2136 líneas de log de Control y del Gateway (contenedor y archivo): 0 tokens, 0 valores de celda, 0 contraseñas o `JWT_CLAVE` y 0 `Bearer ` |

**Abierto 2**: verificado en la verificación complementaria (ver abajo), con dos hallazgos pendientes de decisión.

### Cierre (T023)

- Se borraron los 5 archivos de prueba de SeaweedFS (204), incluido `cargas/f010/ilegible.xlsx`, y los correos de Mailpit. La base principal tenía 0 cargas, así que no se borró nada ajeno.
- Colas con 0 mensajes.
- Arranque normal sobre `CargaMasivaApp`, con todos los servicios `healthy`.
- `CargaMasivaApp_F010` eliminada; quedan `CargaMasiva`, `CargaMasivaApp` y `CargaMasivaDb`.

## Verificación complementaria (2026-11-03): T003, fórmulas y escenario 2

Base temporal nueva `CargaMasivaApp_F010`, con toda la pila apuntando a ella. Datos aislados:
- **Operador C** sin cargas, creado por SQL con hash `PasswordHasher`; la contraseña es aleatoria, no se imprimió y ya se borró.
- **Carga 1 del Operador A**, insertada por SQL en estado Pendiente y apuntando a `seaweed://cargas/f010/formulas.xlsx`.

| Comprobación | Resultado |
|---|---|
| T003 (ingeniería inversa autorizada) | El *scaffold* de `CargaPeriodo` coincide con la entidad y la configuración actuales; solo difieren omisiones intencionadas (ver `tasks.md`). Código conservado sin cambios |
| Escenario 2 | El Operador C pide `GET /cargas` (Gateway y directo): 200 `{"data":[],"isSuccess":true,"message":"OK","code":200,"errors":null}`. El Administrador ve la carga 1 de A, que no aparece en el historial de C. C pide `GET /cargas/1`: 404 «La carga no existe.» |
| Fórmulas con valor en caché | Archivo temporal con los valores en caché escritos como los guarda Excel. `=2+3` → `"5"`, `=A2*2` → `"10"`, `="Hola "&"mundo"` → `"Hola mundo"`, `=A3*2` → `"20"`, `=A3>5` → `"true"`. **`=1+1` con 999 en caché → `"999"`**: se devuelve el valor almacenado, sin recalcular, como fija research D-06 |
| **Hallazgo 1, contra el contrato** (corregido; ver "Ajuste de fórmulas") | `=1/0` con `#DIV/0!` en caché → `"DivisionByZero"` (nombre del enumerado de ClosedXML). Research D-06 fija el texto del error (`#DIV/0!`). **No se corrigió**; requiere decisión |
| **Hallazgo 2, ambigüedad** (resuelto por el usuario: `null`; ver "Ajuste de fórmulas") | `=3*3` **sin** valor en caché (ClosedXML no guarda valores en caché al crear archivos; Excel sí) → `"9"`: ClosedXML la evalúa al leerla. El contrato ("valor almacenado") no define el caso sin valor almacenado. No se añadió ni se quitó ningún recálculo |
| Logs (Control y Gateway, 15 min) | 0 de 3 tokens emitidos, 0 valores de celda y 0 contraseñas |
| Cierre | `cargas/f010/formulas.xlsx` borrado (204); `CargaMasivaApp_F010` eliminada; arranque normal sobre `CargaMasivaApp` con todo `healthy`; la base principal sigue con 0 cargas |

## Ajuste de fórmulas (2026-11-03)

**Decisión del usuario**:
- Las fórmulas no se recalculan al consultar.
- Con resultado guardado, se devuelve ese resultado; un error, como su texto de Excel (`#DIV/0!`).
- Sin resultado guardado, se devuelve `null`.

**Corrección**: `CachedValue` más `TextoError` en el lector de contenido de Control.

**Verificación**: base temporal nueva `CargaMasivaApp_F010`, con toda la pila apuntando a ella. Cargas del Operador A insertadas por SQL:
- carga 1 → `formulas.xlsx`;
- carga 2 → Excel de ejemplo;
- carga 3 → `casos.xlsx`.

Archivo de fórmulas (XML de la hoja, con los resultados guardados como lo hace Excel): `A2 =2+3 [5]`, `B2 =A2*2 [10]`, `C2 ="Hola "&"mundo" [Hola mundo]`, `D2 =1+1 [999, manipulado]`, `E2 =3*3 [sin resultado]`, `B3 =A3*2 [20]`, `C3 =A3>5 [1]`, `D3 =1/0 [#DIV/0!]`.

| Caso | Resultado (Gateway y directo, idénticos) |
|---|---|
| Fórmula con resultado guardado | `"5"`, `"10"`, `"Hola mundo"`, `"20"`, `"true"`. **`D2` → `"999"`**: el resultado guardado, sin recalcular (el recálculo daría `"2"`) |
| Fórmula con error | **`D3` → `"#DIV/0!"`** (antes `"DivisionByZero"`) |
| Fórmula sin resultado guardado | **`E2` → `null`** (antes `"9"`, por evaluación) |
| Regresión: celdas de valor | Excel de ejemplo: 200 filas con 0 diferencias frente al lector independiente. `casos.xlsx`: idéntico a la verificación anterior (espacios, nulos, fila vacía, `"abc"`, `"31-1"`, `"true"`, fecha ISO y `"7.123456"`) |
| Logs (Control y Gateway, 15 min) | 0 de 2 tokens y 0 valores de celda |
| Cierre | 3 archivos de prueba borrados de `cargas/f010/` (204); `CargaMasivaApp_F010` eliminada; arranque normal sobre `CargaMasivaApp` con todo `healthy`; la base principal sigue con 0 cargas |
