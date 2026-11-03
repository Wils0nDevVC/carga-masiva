# Implementation Plan: Consultas y seguimiento de cargas en ms-control

**Branch**: `010-consultas-cargas` (trabajo en `main`) | **Date**: 2026-11-03 | **Spec**: [spec.md](spec.md)

**Input**: `specs/010-consultas-cargas/spec.md`

**Procedencia**: `Dec` = decisión del usuario o constitución v3.5.0; `Spec`; `F005/F006/F007/F009`; `Plan` = decisión técnica (detalle en `research.md`); `Verif` = comprobado en esta etapa.

## Summary

Tres consultas de solo lectura en un nuevo `ConsultasCargasController` de Control, sin Rate Limiting:
- `GET /cargas`: historial.
- `GET /cargas/{id}`: detalle con historial de estados, resultados por periodo e incidencias.
- `GET /cargas/{id}/contenido`: filas del Excel original leído de SeaweedFS con ClosedXML (Q1 = B).

Las queries de MediatR leen a través de `ICargasConsulta` (EF, `AsNoTracking`) y filtran por propietario: el Administrador ve todo; para un Operador, una carga ajena equivale a una inexistente (404). Los errores del archivo siguen Q2 (422, 500 y 503). El Gateway añade las tres rutas GET con JWT. El único mapeo nuevo es `CargaPeriodo`. [Spec] [Dec Q1, Q2] [Plan]

## Technical Context

**Language/Version**: C# / .NET 10.

**Primary Dependencies**:
- Existentes en Control: MediatR 12.5.0, FluentValidation 12.2.0, EF Core SqlServer 10.0.14, Polly.Core 8.9.0, JwtBearer 10.0.14 y Serilog.
- Nueva en Control: ClosedXML 0.105.1, la misma de ms-carga-masiva (autorizada en Q1).

**Storage**: `CargaMasivaApp` sin cambios de esquema; SeaweedFS en lectura (GET).

**Testing**: manual, según `quickstart.md`. No hay tests automatizados (§11).

**Target Platform**: Docker Compose.

**Project Type**:
- Toca `ms-control/` (Api, Application, Domain e Infrastructure) y `api-gateway/src/Acity.Api/ocelot.json`.
- Compose y `.env` no cambian.

**Constraints**:
- Sin paginación, filtros, exportaciones, descarga, caché ni WebSockets.
- Sin escrituras ni cambios de estado.
- `CargasController` y el flujo de recepción (F005) no se modifican.

## Constitution Check

Constitución v3.5.0.

| Principio | Verificación | Resultado |
|---|---|---|
| §1 Alcance | Solo las tres consultas del README. Ampliación puntual de Control autorizada (Q1). Sin extras | Pasa |
| §3/§4 Capas | Controller en Api; queries, DTO (`record`), validadores e interfaces en Application; `CargaPeriodo` en Domain; EF, SeaweedFS y ClosedXML en Infrastructure; mapeos manuales | Pasa |
| §5 Contrato | `Response<T>`, `Code` igual al estado, `data` null en los errores y listas vacías sin datos | Pasa |
| §6 Persistencia y roles | Sin cambios de esquema. Mapeo mínimo de `CargaPeriodo` por ingeniería inversa. Administrador: todo; Operador: solo lo propio, también por identificador | Pasa |
| §9 Seguridad y Rate Limiting | JWT y rol en Control; JWT en el Gateway. El Rate Limiting sigue solo en el endpoint de carga | Pasa |
| §10 Logging | Sin tokens ni contenido de celdas; archivo ausente → Error con `idCarga` | Pasa |
| §12 Dependencias | ClosedXML, la única biblioteca de Excel aprobada | Pasa |

Tras la Phase 1: sin violaciones.

## Project Structure

```text
specs/010-consultas-cargas/
├── plan.md, research.md, data-model.md, quickstart.md
├── contracts/consultas-api.md
├── checklists/requirements.md
└── tasks.md            # /speckit-tasks (no creado aquí)

ms-control/src/
├── Acity.Api/Controllers/ConsultasCargasController.cs          # GET /cargas, /cargas/{id:long}, /cargas/{id:long}/contenido
├── Acity.Application/
│   ├── Cargas/Queries/
│   │   ├── Dtos/{CargaResumenDto, CargaDetalleDto, ContenidoArchivoDto}.cs
│   │   ├── ListarCargas/{ListarCargasQuery, ListarCargasQueryHandler}.cs
│   │   ├── ObtenerDetalleCarga/{ObtenerDetalleCargaQuery, …Handler, …Validator}.cs
│   │   └── ObtenerContenidoCarga/{ObtenerContenidoCargaQuery, …Handler, …Validator}.cs
│   └── Interfaces/
│       ├── Repositories/ICargasConsulta.cs
│       └── Services/{ILectorContenidoExcel, ArchivoNoEncontradoException, AlmacenamientoNoDisponibleException}.cs
│                    # + IAlmacenamientoArchivos.DescargarAsync
├── Acity.Domain/Entities/CargaPeriodo.cs
└── Acity.Infrastructure/
    ├── Acity.Infrastructure.csproj                              # + ClosedXML 0.105.1
    ├── DependencyInjection.cs                                   # + ICargasConsulta, ILectorContenidoExcel
    ├── Persistence/{CargasConsulta.cs, Configurations/CargaPeriodoConfiguration.cs}
    ├── Storage/SeaweedFsAlmacenamiento.cs                       # + DescargarAsync (GET, pipeline seaweedfs)
    └── Excel/LectorContenidoExcelClosedXml.cs

api-gateway/src/Acity.Api/ocelot.json                            # + 3 rutas GET con JWT
```

## Complexity Tracking

| Elemento | Por qué | Alternativa descartada |
|---|---|---|
| Controlador nuevo en lugar de ampliar `CargasController` | La política de Rate Limiting está declarada a nivel de clase en `CargasController`; las consultas no deben consumirla (FR-010) y la F005 no se toca | Mover el atributo al método `POST` (modifica código aprobado de la F005) |
| `ICargasConsulta` | Application no referencia EF; es el patrón de la F007 | Usar `IGenericRepository` con LINQ asíncrono (necesitaría EF en Application) |
| Dos excepciones de almacenamiento | Q2 distingue el archivo ausente (500) del almacenamiento inaccesible (503) | Un solo error genérico |
| Lector de contenido propio (no se reutiliza el de F006) | El contenido no valida encabezados ni interpreta valores (Q1); no hay biblioteca compartida | Copiar el lector de F006, que normaliza |

## Pendientes tras el plan

Ninguno que requiera decisión.

- **Ingeniería inversa**: la de `CargaPeriodo` en Control es manual o requiere autorización expresa.
- **Abiertos 1 a 3 de `research.md`**: las fechas UTC, el valor en caché de las fórmulas y el enrutamiento de Ocelot. Se verifican en la implementación.
- **Precisión de la spec**: un `id` no numérico da 404 de ruta, no 400 (research D-02).
- **Validación en el equipo de evaluación**: no realizada.
