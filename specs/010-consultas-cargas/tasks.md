# Tasks: Consultas y seguimiento de cargas en ms-control

**Input**: `specs/010-consultas-cargas/` (plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md)

**Alcance**:
- Se tocan `ms-control/` (Api, Application, Domain e Infrastructure) y `api-gateway/src/Acity.Api/ocelot.json`.
- La verificación se hace fuera del repositorio.
- No se tocan `CargasController` ni el flujo de recepción (F005), los demás servicios, el esquema, Compose, `.env`, `referencias/` ni la constitución.
- No hay pruebas automatizadas (constitución §11).

**Reglas de ejecución**
- **Ingeniería inversa** (T003): la hace el usuario, o Claude con autorización expresa. La salida queda fuera del repositorio.
- **Datos de prueba**: solo en `CargaMasivaApp_F010`, con toda la pila apuntando a esa base.
  - No se tocan `CargaMasiva`, `CargaMasivaDb` ni volúmenes ajenos.
  - Nunca se usa `docker compose down -v`.
- **Secretos**: solo por entorno; no se imprimen tokens ni contraseñas.
- **Solo lectura**: ninguna consulta escribe en la base ni en SeaweedFS (FR-009).
- **Marcado**: `[x]` solo para lo ejecutado y verificado.
- **Cierre**: entregar el comando de commit en español, sin `Co-Authored-By`.

## Phase 1: Setup

- [x] T001 Añadir `<PackageReference Include="ClosedXML" Version="0.105.1" />` a `ms-control/src/Acity.Infrastructure/Acity.Infrastructure.csproj` y comprobarlo con `dotnet build ms-control/src/Acity.Control.slnx` (research D-01; ajustar el nombre si la solución se llama de otra forma).

## Phase 2: Foundational (bloquea todas las historias)

- [x] T002 [P] Crear `ms-control/src/Acity.Application/Interfaces/Repositories/ICargasConsulta.cs`. Es la única interfaz de lectura y declara:
  - `ListarAsync(int usuarioId, bool esAdministrador, CancellationToken ct)` → `IReadOnlyList<CargaResumenDto>`.
  - `ObtenerDetalleAsync(long idCarga, int usuarioId, bool esAdministrador, CancellationToken ct)` → `CargaDetalleDto?`.
  - `ObtenerArchivoAsync(long idCarga, int usuarioId, bool esAdministrador, CancellationToken ct)` → `(string NombreArchivo, string RutaArchivo)?`.
  - Si la carga no existe o es ajena para un Operador, devuelve `null` (FR-008).
- [ ] T003 **Manual**: ingeniería inversa de `dbo.CargaPeriodo` (`dotnet ef dbcontext scaffold … --table CargaPeriodo`), con la salida fuera del repositorio, sobre la base temporal `CargaMasivaApp_F010` creada por el runner. Contrastar con data-model.md.
- [x] T004 [P] Crear `ms-control/src/Acity.Domain/Entities/CargaPeriodo.cs` a partir de T003, solo con `long Id`, `long CargaId`, `string Periodo` ("char(7)"), `string Resultado` ("varchar(20)") y `DateTime FechaResultado`. `PeriodoReservado` no se mapea (research D-04).
- [x] T005 Crear `ms-control/src/Acity.Infrastructure/Persistence/Configurations/CargaPeriodoConfiguration.cs`:
  - `ToTable("CargaPeriodo")` y la clave `Id`.
  - `Periodo` con `HasMaxLength(7)`, `IsFixedLength()` e `IsUnicode(false)`; `Resultado` con `HasMaxLength(20)` e `IsUnicode(false)`.
  - La FK `CargaId` hacia `CargaArchivo` sin navegación, igual que `IncidenciaConfiguration`.
- [x] T006 [P] Crear los DTO en `ms-control/src/Acity.Application/Cargas/Queries/Dtos/`, según data-model.md:
  - `CargaResumenDto.cs`.
  - `CargaDetalleDto.cs`, con `EstadoHistorialDto`, `ResultadoPeriodoDto` e `IncidenciaDto`.
  - `ContenidoArchivoDto.cs`, con `FilaContenidoDto`.
- [x] T007 Crear `ms-control/src/Acity.Infrastructure/Persistence/CargasConsulta.cs`, que implementa `ICargasConsulta` con LINQ y `AsNoTracking` (research D-03):
  - **Filtro**: `esAdministrador || c.UsuarioId == usuarioId`.
  - **Join**: con `Usuario` para obtener el `Email`.
  - **Orden**: historial por `FechaRecepcion` y luego `Id`, ambos descendentes; estados por `FechaCambio` y `Id`; periodos por `FechaResultado` y `Id`; incidencias por `Id`.
  - **Fechas**: con `DateTime.SpecifyKind(…, DateTimeKind.Utc)`.
  - **Secciones vacías**: se devuelven como listas vacías.
- [x] T008 Registrar `ICargasConsulta` → `CargasConsulta` (scoped) en `ms-control/src/Acity.Infrastructure/DependencyInjection.cs`.
- [x] T009 Crear `ms-control/src/Acity.Api/Controllers/ConsultasCargasController.cs` (`[ApiController]`, `[Route("cargas")]`, `[Authorize(Roles = "Administrador,Operador")]`, **sin** `[EnableRateLimiting]`):
  - Un método privado obtiene `usuarioId = int.Parse(User.FindFirst("sub")!.Value)` y `esAdministrador = User.IsInRole("Administrador")`.
  - Cada acción devuelve `StatusCode(r.Code, r)`.
  - Las acciones se añaden en cada historia.

**Checkpoint**: `dotnet build` sin advertencias ni errores.

## Phase 3: US1 - Historial de cargas (P1) 🎯 MVP

**Objetivo**: `GET /cargas` según el rol, sin Rate Limiting.

**Prueba independiente**: quickstart, escenarios 1 a 4.

- [x] T010 [P] [US1] Crear `ms-control/src/Acity.Application/Cargas/Queries/ListarCargas/{ListarCargasQuery,ListarCargasQueryHandler}.cs`:
  - `record ListarCargasQuery(int UsuarioId, bool EsAdministrador) : IRequest<Response<IReadOnlyList<CargaResumenDto>>>`.
  - El handler devuelve `Response.Ok(lista)`; una lista vacía también da 200 (FR-002).
- [x] T011 [US1] Añadir `[HttpGet] Listar()` a `ConsultasCargasController.cs`.
- [x] T012 [US1] Añadir a `api-gateway/src/Acity.Api/ocelot.json` la ruta `GET /cargas` → `ms-control:8080/cargas`, con `AuthenticationProviderKeys ["Bearer"]` y sin `RouteClaimsRequirement` (research D-07).

## Phase 4: US2 - Detalle de una carga (P1)

**Objetivo**: `GET /cargas/{id}` con historial de estados, periodos e incidencias, y 404 para una carga ajena o inexistente.

**Prueba independiente**: quickstart, escenarios 5 a 8.

- [x] T013 [P] [US2] Crear `ms-control/src/Acity.Application/Cargas/Queries/ObtenerDetalleCarga/{ObtenerDetalleCargaQuery,ObtenerDetalleCargaQueryHandler,ObtenerDetalleCargaQueryValidator}.cs`:
  - El validador exige `IdCarga > 0` (si no, 400).
  - Si `ICargasConsulta` devuelve null, `Response.Fail(404, "La carga no existe.")`; si no, `Ok(detalle)`.
- [x] T014 [US2] Añadir `[HttpGet("{id:long}")] ObtenerDetalle(long id)` a `ConsultasCargasController.cs`.
- [x] T015 [US2] Añadir a `ocelot.json` la ruta `GET /cargas/{id}` → `ms-control:8080/cargas/{id}` con JWT.

## Phase 5: US3 - Contenido del archivo (P2)

**Objetivo**: `GET /cargas/{id}/contenido` con las filas originales del Excel y los errores de Q2.

**Prueba independiente**: quickstart, escenarios 7 y 9 a 12.

- [x] T016 [P] [US3] Crear en `ms-control/src/Acity.Application/Interfaces/Services/`:
  - `ArchivoNoEncontradoException.cs` y `AlmacenamientoNoDisponibleException.cs`.
  - `ILectorContenidoExcel.cs`, con `ContenidoExcel? Leer(byte[] contenido)`, donde `ContenidoExcel` es `record(string Hoja, IReadOnlyList<string?> Encabezados, IReadOnlyList<FilaContenidoDto> Filas)`.
  - En `IAlmacenamientoArchivos.cs`, el método `Task<byte[]> DescargarAsync(string rutaArchivo, CancellationToken ct)`.
- [x] T017 [US3] Implementar `DescargarAsync` en `ms-control/src/Acity.Infrastructure/Storage/SeaweedFsAlmacenamiento.cs` (research D-05):
  - `GET` a la misma `ConstruirUri`, dentro del pipeline `seaweedfs` existente.
  - Un 404 lanza `ArchivoNoEncontradoException`, sin reintento.
  - Otro código no satisfactorio lanza `HttpRequestException` y el pipeline lo reintenta si es transitorio.
  - Una excepción final de red, de tiempo de espera, `BrokenCircuitException` o cualquier `HttpRequestException` restante se traduce a `AlmacenamientoNoDisponibleException`.
- [x] T018 [P] [US3] Crear `ms-control/src/Acity.Infrastructure/Excel/LectorContenidoExcelClosedXml.cs` (research D-06):
  - Devuelve null si `new XLWorkbook(stream)` lanza o si no hay hojas.
  - Lee la primera hoja: columnas 1 a `LastColumnUsed()`, la fila 1 como encabezados y las filas 2 a `LastRowUsed()` como datos, incluidas las vacías.
  - **Valor de cada celda**: Blank → null; Text → tal cual, sin trim; Number → `ToString("R", InvariantCulture)`; Boolean → `true`/`false`; DateTime → `yyyy-MM-ddTHH:mm:ss`; TimeSpan → `c`; Error → su texto. Sin `GetFormattedString`.
  - No registra el contenido. Registrarlo como singleton en `DependencyInjection.cs`.
- [x] T019 [US3] Crear `ms-control/src/Acity.Application/Cargas/Queries/ObtenerContenidoCarga/{ObtenerContenidoCargaQuery,ObtenerContenidoCargaQueryHandler,ObtenerContenidoCargaQueryValidator}.cs` (validador `IdCarga > 0`). El handler sigue este orden (FR-005c, Q2):
  1. `ObtenerArchivoAsync`: si devuelve null → 404 «La carga no existe.», sin leer el archivo.
  2. `DescargarAsync`:
     - `ArchivoNoEncontradoException` → log Error con `IdCarga` y 500 «No se pudo recuperar el archivo asociado a la carga.».
     - `AlmacenamientoNoDisponibleException` → log Warning y 503 «El archivo no está disponible temporalmente. Intente nuevamente más tarde.».
  3. `Leer`: si devuelve null → 422 «El archivo no se puede leer como Excel.».
  4. Si todo va bien → `Ok(new ContenidoArchivoDto(...))`.
- [x] T020 [US3] Añadir `[HttpGet("{id:long}/contenido")] ObtenerContenido(long id)` a `ConsultasCargasController.cs`, y a `ocelot.json` la ruta `GET /cargas/{id}/contenido` con JWT. Si Ocelot confunde esta ruta con `/cargas/{id}`, añadir `Priority` a la más específica (Abierto 3).

## Phase 6: Verificación y cierre

- [x] T021 Compilar `ms-control` y `api-gateway` sin advertencias. Levantar la pila con `DB_NAME=CargaMasivaApp_F010 docker compose up -d --build` y preparar los datos del quickstart: segundo Operador por SQL, cargas de A y B, y una carga Pendiente por SQL.
- [x] T022 Verificar los escenarios 1 a 15 del quickstart, incluidos los Abiertos 1 a 3 de research.md:
  - Huella de la base antes y después (SC-006).
  - Pruebas a través del Gateway y directas.
  - Sin imprimir tokens.
  - Registrar los resultados en `specs/010-consultas-cargas/quickstart.md`.
- [x] T023 Cierre:
  - Borrar solo los archivos y correos de esta prueba.
  - Eliminar `CargaMasivaApp_F010`.
  - Arranque normal sobre `CargaMasivaApp` con todo `healthy`.
  - Completar "Resultados" en `quickstart.md` y entregar el comando de commit.

## Estado de la implementación (2026-11-03)

- **22 de 23 tareas hechas.** Compilación de `ms-control` y `api-gateway`: 0 advertencias, 0 errores. Escenarios 1–15 ejecutados sobre `CargaMasivaApp_F010`; el detalle está en `quickstart.md`.
- **T003 sin marcar**: la ingeniería inversa no estaba autorizada. `CargaPeriodo` (T004 y T005) se escribió a partir de data-model.md y se contrastó con el catálogo de SQL Server (`sys.columns` y `sys.foreign_keys`), sin diferencias: `Id` bigint identity, `CargaId` bigint, `Periodo` char(7), `Resultado` varchar(20), `FechaResultado` datetime2 y `FK_CargaPeriodo_Carga`. `PeriodoReservado` se omite a propósito. Falta el *scaffold* si el usuario lo autoriza.
- **Ajuste respecto a T002**: `ObtenerArchivoAsync` devuelve el `record ArchivoCarga(NombreArchivo, RutaArchivo)` en lugar de una tupla, por legibilidad.

## Dependencias y orden

- Setup (T001) → Foundational (T002 a T009) → US1 (T010 a T012) → US2 (T013 a T015) → US3 (T016 a T020) → Verificación (T021 a T023).
- T003 bloquea T004 y T005. T006 bloquea T002 y T007 (los DTO se usan en la interfaz), así que conviene hacer T006 antes que T002.
- T011, T014 y T020 editan el mismo controlador; T012, T015 y T020 editan el mismo `ocelot.json`. Ambos grupos van en secuencia.
- US1 a US3 comparten la base común, pero cada una se puede probar por separado.

## Paralelismo

- Foundational: T004 y T006 en paralelo (después de T003, en el caso de T004).
- US1: T010 en paralelo con T013 (archivos distintos).
- US3: T016 y T018 en paralelo; después, T017 y T019.

## Estrategia

- **MVP**: US1, el historial con visibilidad por rol, sin Rate Limiting. Es el seguimiento y el *polling* del README.
- US2 añade el detalle para reconstruir lo ocurrido; US3, el contenido original del Excel (Q1 = B).
