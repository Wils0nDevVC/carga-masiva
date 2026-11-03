/** Error normalizado del cliente a partir de Response<T>; code 0 si no hubo respuesta del servidor. */
export interface ApiError {
  code: number
  message: string
  errors: string[]
}
