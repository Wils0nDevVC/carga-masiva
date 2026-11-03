import { createContext, useCallback, useMemo, useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router'
import { login } from '../api/auth'
import { guardarSesion, leerSesion, limpiarSesion } from '../api/sesion'
import type { TokensDto } from '../models/Auth'

export interface AuthContextValue {
  estaAutenticado: boolean
  iniciarSesion: (email: string, password: string) => Promise<void>
  cerrarSesion: () => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)

/** Sesión del cliente (Feature 011): tokens de Autenticación en sessionStorage, sin decodificar el JWT. */
export function AuthProvider({ children }: { children: ReactNode }) {
  const navigate = useNavigate()
  const [tokens, setTokens] = useState<TokensDto | null>(() => leerSesion())

  const iniciarSesion = useCallback(async (email: string, password: string) => {
    const nuevos = await login({ email, password })
    guardarSesion(nuevos)
    setTokens(nuevos)
  }, [])

  const cerrarSesion = useCallback(() => {
    limpiarSesion()
    setTokens(null)
    navigate('/login', { replace: true })
  }, [navigate])

  const valor = useMemo(
    () => ({ estaAutenticado: tokens !== null, iniciarSesion, cerrarSesion }),
    [tokens, iniciarSesion, cerrarSesion],
  )

  return <AuthContext.Provider value={valor}>{children}</AuthContext.Provider>
}
