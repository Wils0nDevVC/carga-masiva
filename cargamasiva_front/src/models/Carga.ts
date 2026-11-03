/** Carga registrada por Control (Feature 005, respuesta 202 de POST /cargas). */
export interface CargaRegistradaDto {
  idCarga: number
  estado: string
}

/** Carga en el historial (Feature 010, GET /cargas). Fechas ISO en UTC; fechaFin null si no terminó. */
export interface CargaResumenDto {
  idCarga: number
  nombreArchivo: string
  usuario: string
  fechaRecepcion: string
  estado: string
  fechaFin: string | null
}

/** Detalle de una carga (Feature 010, GET /cargas/{id}). */
export interface EstadoHistorialDto {
  estado: string
  fecha: string
}

export interface ResultadoPeriodoDto {
  periodo: string
  resultado: string
  fecha: string
}

export interface IncidenciaDto {
  tipo: string
  numeroFila: number | null
  codigoProducto: string | null
  periodoTexto: string | null
  motivo: string
  fecha: string
}

export interface CargaDetalleDto extends CargaResumenDto {
  historial: EstadoHistorialDto[]
  periodos: ResultadoPeriodoDto[]
  incidencias: IncidenciaDto[]
}

/** Filas del Excel original (Feature 010, GET /cargas/{id}/contenido): valores sin normalizar; null = celda vacía. */
export interface FilaContenidoDto {
  numeroFila: number
  celdas: (string | null)[]
}

export interface ContenidoArchivoDto {
  idCarga: number
  nombreArchivo: string
  hoja: string
  encabezados: (string | null)[]
  filas: FilaContenidoDto[]
}
