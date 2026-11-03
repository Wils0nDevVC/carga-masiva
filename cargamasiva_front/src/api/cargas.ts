import type { CargaDetalleDto, CargaRegistradaDto, CargaResumenDto, ContenidoArchivoDto } from '../models/Carga'
import type { Response } from '../models/Response'
import { cliente, desempaquetar } from './cliente'

/** POST /cargas multipart (campo `archivo`). Sin Content-Type manual: Axios lo genera con el boundary. */
export function subirCarga(archivo: File): Promise<CargaRegistradaDto> {
  const datos = new FormData()
  datos.append('archivo', archivo)
  return desempaquetar(cliente.post<Response<CargaRegistradaDto>>('/cargas', datos))
}

/** Historial de cargas visibles para el usuario (Feature 010): Administrador todas; Operador las propias. */
export function listarCargas(): Promise<CargaResumenDto[]> {
  return desempaquetar(cliente.get<Response<CargaResumenDto[]>>('/cargas'))
}

/** Detalle de una carga (Feature 010). Inexistente o ajena → ApiError 404 «La carga no existe.». */
export function obtenerDetalleCarga(id: number): Promise<CargaDetalleDto> {
  return desempaquetar(cliente.get<Response<CargaDetalleDto>>(`/cargas/${id}`))
}

/** Filas del Excel original (Feature 010). Errores 404, 422, 500 y 503 como ApiError. */
export function obtenerContenidoCarga(id: number): Promise<ContenidoArchivoDto> {
  return desempaquetar(cliente.get<Response<ContenidoArchivoDto>>(`/cargas/${id}/contenido`))
}
