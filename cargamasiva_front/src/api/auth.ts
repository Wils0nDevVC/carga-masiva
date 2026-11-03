import type { LoginRequest, RefreshRequest, TokensDto } from '../models/Auth'
import type { Response } from '../models/Response'
import { cliente, desempaquetar } from './cliente'

// Rutas de Autenticación (Feature 008): nunca llevan el token de acceso ni disparan una renovación.
export const RUTA_LOGIN = '/auth/login'
export const RUTA_REFRESH = '/auth/refresh'

export function login(req: LoginRequest): Promise<TokensDto> {
  return desempaquetar(cliente.post<Response<TokensDto>>(RUTA_LOGIN, req))
}

export function refresh(req: RefreshRequest): Promise<TokensDto> {
  return desempaquetar(cliente.post<Response<TokensDto>>(RUTA_REFRESH, req))
}
