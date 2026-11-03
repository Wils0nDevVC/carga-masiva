/** Contrato común de respuesta de los servicios (constitución §5). `code` coincide con el estado HTTP. */
export interface Response<T> {
  data: T | null
  isSuccess: boolean
  message: string | null
  code: number
  errors: string[] | null
}
