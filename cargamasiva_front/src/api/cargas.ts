import type { CargaRegistradaDto } from '../models/Carga'
import type { Response } from '../models/Response'
import { cliente, desempaquetar } from './cliente'

/** POST /cargas multipart (campo `archivo`). Sin Content-Type manual: Axios lo genera con el boundary. */
export function subirCarga(archivo: File): Promise<CargaRegistradaDto> {
  const datos = new FormData()
  datos.append('archivo', archivo)
  return desempaquetar(cliente.post<Response<CargaRegistradaDto>>('/cargas', datos))
}
