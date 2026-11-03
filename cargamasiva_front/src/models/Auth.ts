/** Contratos de Autenticación (Feature 008, contracts/auth-api.md). Fechas ISO en UTC. */
export interface TokensDto {
  accessToken: string
  expiraEn: string
  refreshToken: string
  refreshExpiraEn: string
}

export interface LoginRequest {
  email: string
  password: string
}

export interface RefreshRequest {
  refreshToken: string
}
