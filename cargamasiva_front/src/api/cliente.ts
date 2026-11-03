import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios'
import type { ApiError } from '../models/ApiError'
import type { TokensDto } from '../models/Auth'
import type { Response } from '../models/Response'
import { guardarSesion, leerSesion, limpiarSesion } from './sesion'

/** Instancia única: mismo origen (baseURL vacía); /auth y /cargas los reenvía el servidor al Gateway. */
export const cliente = axios.create({ baseURL: '' })

const MENSAJE_SIN_CONEXION = 'No se pudo conectar con el servidor'

/** Convierte cualquier error en ApiError a partir de Response<T>, sin perder code, message ni errors. */
export function aApiError(error: unknown): ApiError {
  if (error instanceof AxiosError) {
    const cuerpo = error.response?.data as Partial<Response<unknown>> | undefined
    if (cuerpo && typeof cuerpo === 'object' && typeof cuerpo.code === 'number') {
      return { code: cuerpo.code, message: cuerpo.message ?? MENSAJE_SIN_CONEXION, errors: cuerpo.errors ?? [] }
    }
    return { code: error.response?.status ?? 0, message: MENSAJE_SIN_CONEXION, errors: [] }
  }
  return { code: 0, message: MENSAJE_SIN_CONEXION, errors: [] }
}

function esApiError(error: unknown): error is ApiError {
  return typeof error === 'object' && error !== null && 'code' in error && 'message' in error && !(error instanceof Error)
}

/** Devuelve data de Response<T> o lanza ApiError. */
export async function desempaquetar<T>(promesa: Promise<{ data: Response<T> }>): Promise<T> {
  try {
    const { data: respuesta } = await promesa
    if (!respuesta.isSuccess) {
      throw { code: respuesta.code, message: respuesta.message ?? '', errors: respuesta.errors ?? [] } satisfies ApiError
    }
    return respuesta.data as T
  } catch (error) {
    throw esApiError(error) ? error : aApiError(error)
  }
}

// ---- Interceptores (Feature 011, US3) ----

// Rutas de Autenticación: sin token de acceso y fuera del ciclo de renovación (contrato de la Feature 008).
const RUTAS_AUTENTICACION = ['/auth/login', '/auth/refresh']

const esRutaAutenticacion = (url?: string) => RUTAS_AUTENTICACION.some((ruta) => url?.startsWith(ruta))

type ConfigReintentable = InternalAxiosRequestConfig & { _reintentada?: boolean }

/** Una sola renovación compartida por todas las peticiones que reciben 401 al mismo tiempo. */
let renovacionEnCurso: Promise<TokensDto> | null = null

function renovar(refreshToken: string): Promise<TokensDto> {
  renovacionEnCurso ??= desempaquetar(cliente.post<Response<TokensDto>>('/auth/refresh', { refreshToken }))
    .then((tokens) => {
      guardarSesion(tokens)
      return tokens
    })
    .finally(() => {
      renovacionEnCurso = null
    })
  return renovacionEnCurso
}

cliente.interceptors.request.use((config) => {
  const sesion = leerSesion()
  if (sesion && !esRutaAutenticacion(config.url)) {
    config.headers.Authorization = `Bearer ${sesion.accessToken}`
  }
  return config
})

cliente.interceptors.response.use(
  (respuesta) => respuesta,
  async (error: unknown) => {
    const config = error instanceof AxiosError ? (error.config as ConfigReintentable | undefined) : undefined
    const sesion = leerSesion()
    const debeRenovar =
      error instanceof AxiosError &&
      error.response?.status === 401 &&
      config !== undefined &&
      !config._reintentada &&
      !esRutaAutenticacion(config.url) &&
      sesion !== null

    if (!debeRenovar) {
      throw aApiError(error)
    }

    config._reintentada = true
    // Si la petición salió con un token que ya fue renovado por otra, basta con reintentar con el actual.
    const tokenUsado = String(config.headers.Authorization ?? '').replace('Bearer ', '')
    if (tokenUsado !== sesion.accessToken) {
      return cliente(config)
    }
    try {
      await renovar(sesion.refreshToken)
    } catch (fallo) {
      // Renovación fallida: se limpia la sesión y se vuelve al login; las peticiones en espera se rechazan.
      limpiarSesion()
      window.location.assign('/login')
      throw esApiError(fallo) ? fallo : aApiError(fallo)
    }
    // Reintento único con el token nuevo (lo añade el interceptor de petición).
    return cliente(config)
  },
)
