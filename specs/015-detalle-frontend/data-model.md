# Data Model: Detalle

Contratos TypeScript de F010 (`cargamasiva_front/src/models/Carga.ts`):

| Modelo | Forma |
|---|---|
| `EstadoHistorialDto` | `{ estado: string; fecha: string }` |
| `ResultadoPeriodoDto` | `{ periodo: string; resultado: string; fecha: string }` |
| `IncidenciaDto` | `{ tipo: string; numeroFila: number \| null; codigoProducto: string \| null; periodoTexto: string \| null; motivo: string; fecha: string }` |
| `CargaDetalleDto` | `CargaResumenDto & { historial: EstadoHistorialDto[]; periodos: ResultadoPeriodoDto[]; incidencias: IncidenciaDto[] }` |
| `FilaContenidoDto` | `{ numeroFila: number; celdas: (string \| null)[] }` |
| `ContenidoArchivoDto` | `{ idCarga: number; nombreArchivo: string; hoja: string; encabezados: (string \| null)[]; filas: FilaContenidoDto[] }` |

Estados terminales (para detener el *polling*): `Notificado` y `Fallido` (constitución §8).
