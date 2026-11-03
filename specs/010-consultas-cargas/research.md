# Research: Consultas y seguimiento de cargas en ms-control

**Procedencia**: `Dec` = decisión del usuario o constitución v3.5.0; `Spec` = spec 010; `F005/F006/F007/F009` = features anteriores; `Plan` = decisión técnica de este plan (alternativa más sencilla); `Verif` = comprobado en esta etapa; `Sin verif` = se comprueba en la implementación.

## D-01 Paquetes

| Paquete | Versión | Proyecto | Comprobación |
|---|---|---|---|
| `ClosedXML` | 0.105.1 | Control Infrastructure | La misma versión que ms-carga-masiva (F006). Biblioteca única para Excel (§12). Autorizada para Control en Q1 |

No se añade nada más: SeaweedFS se lee con el `HttpClient` existente y Polly ya está en Control. [Dec §12] [Spec Q1]

## D-02 Controlador y Rate Limiting [Spec FR-006, FR-010]

- **Nuevo controlador** `ConsultasCargasController` con `[Route("cargas")]` y `[Authorize(Roles = "Administrador,Operador")]`, **sin** `[EnableRateLimiting]`.
  - `CargasController` (F005) no se toca. Su política `carga` está declarada a nivel de clase, así que, si las consultas fueran en él, consumirían el límite de subidas.
  - La política no es global (`AddPolicy` más `UseRateLimiter`, sin `GlobalLimiter`) [Verif], así que el nuevo controlador no tiene límite. [Plan]
- **Rutas**:
  - `GET /cargas` → historial.
  - `GET /cargas/{id:long}` → detalle.
  - `GET /cargas/{id:long}/contenido` → contenido.
- **Validación del identificador**:
  - Un `id` no numérico no coincide con la ruta y da 404 «No encontrado» del manejador de códigos de estado existente.
  - Un `id ≤ 0` da 400 por el validador (FluentValidation).
  - Esto concreta Spec US2-5: "no válido" → 400 solo para `≤ 0`; el no numérico queda como 404 de ruta inexistente. [Plan]
- **Usuario**: el controlador pasa `UsuarioId` (claim `sub`, ya validado como entero > 0 en F005) y `EsAdministrador` (`User.IsInRole("Administrador")`) a cada query de MediatR.

## D-03 Consultas en Application e Infrastructure [Dec §3, §4] [F007 patrón `ICargasConsulta`]

- Las queries de MediatR van en `Application/Cargas/Queries/{ListarCargas, ObtenerDetalleCarga, ObtenerContenidoCarga}`, con sus DTO como `record`.
- Application no referencia EF, así que las lecturas van detrás de `ICargasConsulta` (Interfaces/Repositories), implementada en `Infrastructure/Persistence/CargasConsulta.cs` con LINQ, `AsNoTracking` y proyección directa a DTO. Es el mismo patrón que ms-notificaciones (F007).
- **Filtro por propietario dentro de la consulta**: `EsAdministrador || c.UsuarioId == usuarioId`.
  - Una carga ajena devuelve `null`, igual que una inexistente. Por eso la respuesta es idéntica: 404 «La carga no existe» (FR-008).
  - En el contenido, esa comprobación ocurre **antes** de leer el archivo (FR-005c).
- **Orden**:
  - historial: `FechaRecepcion` descendente y luego `Id` descendente;
  - estados: `FechaCambio` y luego `Id`;
  - periodos: `FechaResultado` y luego `Id`;
  - incidencias: `Id`.
- **Fechas**: en UTC. Las columnas `DATETIME2` guardan UTC (F005 y F006). Los DTO se proyectan con `DateTime.SpecifyKind(x, DateTimeKind.Utc)` para que el JSON lleve `Z`, sin `ValueConverter` global. [Plan] *Sin verif*: que EF traduzca o evalúe la proyección sin error; si no, se ajusta después de materializar.

## D-04 Mapeo adicional `CargaPeriodo` [Spec Key Entities] [Dec §6]

- Entidad de Control `CargaPeriodo` con `Id` (bigint), `CargaId` (bigint), `Periodo` (char(7)), `Resultado` (varchar(20)) y `FechaResultado` (datetime2).
- Columnas mínimas para "resultados por periodo"; `PeriodoReservado` no se mapea. Solo lectura.
- Configuración `CargaPeriodoConfiguration`: FK a `CargaArchivo` sin navegación, como en `IncidenciaConfiguration`.
- Se obtiene por **ingeniería inversa** (manual o con autorización expresa), y la salida se contrasta con el catálogo.
- `Producto` no se mapea (Q1 = B).

## D-05 Lectura del archivo desde SeaweedFS [Spec FR-005, FR-005d] [Dec Q2]

- `IAlmacenamientoArchivos` añade `Task<byte[]> DescargarAsync(string rutaArchivo, CancellationToken ct)`. `SeaweedFsAlmacenamiento` hace `GET` al filer con la misma `ConstruirUri` (esquema `seaweed://`), igual que ms-carga-masiva (F006).
- **Pipeline**: el `seaweedfs` existente (3 reintentos, 30 s y su circuit breaker; el GET es idempotente). Comparte el circuito con la subida porque la dependencia es la misma. [Plan]
- **Excepciones de Application** (`Interfaces/Services`), traducidas por Infrastructure:

| Situación | Excepción | Respuesta (Q2) |
|---|---|---|
| El filer responde 404 (archivo ausente en la ruta registrada) | `ArchivoNoEncontradoException` (sin reintento) | **500** «No se pudo recuperar el archivo asociado a la carga» + log Error con `idCarga` |
| Error de red, 5xx, 408 o 429 tras agotar los reintentos; tiempo de espera; circuito abierto (`BrokenCircuitException`) | `AlmacenamientoNoDisponibleException` | **503** «El archivo no está disponible temporalmente. Intente nuevamente más tarde» |

- Otros códigos 4xx del filer (por ejemplo 400 o 403) no son transitorios y se tratan como 503. No se esperan con la ruta registrada. [Plan]
- El handler captura las dos excepciones y devuelve `Response<T>.Fail` con el código y el mensaje de Q2. No se modifica el `GlobalExceptionHandler`.

## D-06 Lectura del Excel [Spec FR-005a] [Dec Q1, Q2]

- `ILectorContenidoExcel` (Application) → `LectorContenidoExcelClosedXml` (Infrastructure) devuelve `ContenidoExcel?`: `null` si es **ilegible**, es decir, si `new XLWorkbook(stream)` lanza o si no hay hojas.
  - El handler responde a `null` con **422** «El archivo no se puede leer como Excel».
  - No se exigen los cuatro encabezados: el contenido no valida, solo muestra (a diferencia de F006). [Dec Q1: sin normalizar ni reprocesar]
- **Rango**:
  - primera hoja;
  - columnas de 1 a `LastColumnUsed()`, filas de 1 a `LastRowUsed()`;
  - la fila 1 son los encabezados y las siguientes, los datos, incluidas las filas vacías intermedias.
  - Una hoja sin celdas usadas da encabezados y filas vacíos, con 200. Es contenido real vacío, no un error.
- **Valor de cada celda**, sin aplicar el formato visual (`GetFormattedString` no se usa):

| Tipo (`XLDataType`) | Representación |
|---|---|
| Blank | `null` |
| Text | el texto tal cual, sin trim |
| Number | `double.ToString("R", InvariantCulture)` (por ejemplo `12.5`, `1500`) |
| Boolean | `true` o `false` |
| DateTime | `yyyy-MM-ddTHH:mm:ss` (invariante) |
| TimeSpan | `c` (invariante) |
| Error | el texto del error (por ejemplo `#DIV/0!`) |

  Las fórmulas se leen por su valor almacenado. *Sin verif*: que ClosedXML exponga el valor en caché sin recalcular.
- **Memoria**: el archivo cabe en memoria (≤ 10 MiB, F005). Se libera con `using`.
- **Logs**: no se registra el contenido; solo el `idCarga` y el resultado.

## D-07 Gateway [Spec FR-012] [F009]

Nuevas rutas en `api-gateway/src/Acity.Api/ocelot.json`, todas con `AuthenticationProviderKeys ["Bearer"]` y sin `RouteClaimsRequirement`:
- `GET /cargas` → `ms-control:8080/cargas`
- `GET /cargas/{id}` → `/cargas/{id}`
- `GET /cargas/{id}/contenido` → `/cargas/{id}/contenido`

La ruta `POST /cargas` existente no cambia. Las respuestas de Control (200, 400, 404, 422, 500 y 503) se reenvían sin cambios (F009 Q3). *Sin verif*: que Ocelot distinga `/cargas/{id}` de `/cargas/{id}/contenido` sin `Priority`. Si no lo hace, se añade `Priority` a la más específica.

## Abiertos (se verifican en la implementación)

1. La representación de fechas en UTC en la serialización (D-03).
2. Que el valor en caché de las fórmulas se lea con ClosedXML (D-06).
3. El enrutamiento de Ocelot con plantillas que se solapan (D-07).
4. Validación en el equipo de evaluación: no realizada.
