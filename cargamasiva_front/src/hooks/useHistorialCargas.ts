import { useEffect, useState } from 'react'
import { listarCargas } from '../api/cargas'
import type { ApiError } from '../models/ApiError'
import type { CargaResumenDto } from '../models/Carga'

/** Intervalo del seguimiento en tiempo real (README: polling). */
export const INTERVALO_MS = 5000

/**
 * Historial con polling (Feature 014): una consulta tras otra (sin solapar), solo con la pestaña visible;
 * se detiene al desmontar y conserva los últimos datos ante un error.
 */
export function useHistorialCargas() {
  const [cargas, setCargas] = useState<CargaResumenDto[] | null>(null)
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState<ApiError | null>(null)

  useEffect(() => {
    let activo = true
    let enCurso = false
    let temporizador: ReturnType<typeof setTimeout> | undefined

    const visible = () => document.visibilityState === 'visible'

    async function consultar() {
      if (enCurso) {
        return
      }
      enCurso = true
      try {
        const lista = await listarCargas()
        if (activo) {
          setCargas(lista)
          setError(null)
        }
      } catch (fallo) {
        if (activo) {
          setError(fallo as ApiError)
        }
      } finally {
        enCurso = false
        if (activo) {
          setCargando(false)
          clearTimeout(temporizador)
          if (visible()) {
            temporizador = setTimeout(consultar, INTERVALO_MS)
          }
        }
      }
    }

    function alCambiarVisibilidad() {
      clearTimeout(temporizador)
      if (visible()) {
        void consultar()
      }
    }

    void consultar()
    document.addEventListener('visibilitychange', alCambiarVisibilidad)
    return () => {
      activo = false
      clearTimeout(temporizador)
      document.removeEventListener('visibilitychange', alCambiarVisibilidad)
    }
  }, [])

  return { cargas, cargando, error }
}
