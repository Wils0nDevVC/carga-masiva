import type { TokensDto } from '../models/Auth'

// Tokens de sesión en sessionStorage (constitución §11, Feature 011 Q1). Una sola clave; sin otros almacenes.
const CLAVE = 'carga-masiva.sesion'

export function leerSesion(): TokensDto | null {
  try {
    const valor = sessionStorage.getItem(CLAVE)
    return valor ? (JSON.parse(valor) as TokensDto) : null
  } catch {
    return null
  }
}

export function guardarSesion(tokens: TokensDto): void {
  try {
    sessionStorage.setItem(CLAVE, JSON.stringify(tokens))
  } catch {
    // Sin almacenamiento disponible la sesión no persiste.
  }
}

export function limpiarSesion(): void {
  try {
    sessionStorage.removeItem(CLAVE)
  } catch {
    // Nada que limpiar.
  }
}
