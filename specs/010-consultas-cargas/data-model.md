# Data Model: Consultas y seguimiento de cargas

Sin cambios de esquema. Todo es de solo lectura.

## Entidades leídas por Control

| Entidad | Estado en Control | Columnas usadas | Uso |
|---|---|---|---|
| `CargaArchivo` | mapeada (F005) | `Id`, `NombreArchivo`, `UsuarioId`, `FechaRecepcion`, `RutaArchivo`, `Estado`, `FechaFin` | historial, detalle y ruta del archivo |
| `Usuario` | mapeada (F005) | `Id`, `Email` | correo del responsable |
| `CargaEstadoHistorial` | mapeada (F005) | `CargaId`, `Estado`, `FechaCambio` | historial de estados |
| `Incidencia` | mapeada (F005) | `CargaId`, `Tipo`, `NumeroFila`, `CodigoProducto`, `PeriodoTexto`, `Motivo`, `FechaRegistro` | incidencias |
| `CargaPeriodo` | **nueva** (research D-04) | `Id` bigint, `CargaId` bigint, `Periodo` char(7), `Resultado` varchar(20), `FechaResultado` datetime2 | resultados por periodo |

`Producto` no se mapea (Q1 = B).

## Regla de visibilidad [Constitución §6] [Spec FR-007, FR-008]

Una carga es visible si `EsAdministrador` o si `CargaArchivo.UsuarioId == sub`. Una carga no visible se trata igual que una inexistente.

## DTO de respuesta (`record`)

- `CargaResumenDto(long IdCarga, string NombreArchivo, string Usuario, DateTime FechaRecepcion, string Estado, DateTime? FechaFin)`
- `CargaDetalleDto(...los campos de CargaResumenDto, IReadOnlyList<EstadoHistorialDto> Historial, IReadOnlyList<ResultadoPeriodoDto> Periodos, IReadOnlyList<IncidenciaDto> Incidencias)`
  - `EstadoHistorialDto(string Estado, DateTime Fecha)`
  - `ResultadoPeriodoDto(string Periodo, string Resultado, DateTime Fecha)`
  - `IncidenciaDto(string Tipo, int? NumeroFila, string? CodigoProducto, string? PeriodoTexto, string Motivo, DateTime Fecha)`
- `ContenidoArchivoDto(long IdCarga, string NombreArchivo, string Hoja, IReadOnlyList<string?> Encabezados, IReadOnlyList<FilaContenidoDto> Filas)`
  - `FilaContenidoDto(int NumeroFila, IReadOnlyList<string?> Celdas)`: tantas celdas como encabezados (columnas 1 a la última usada).
