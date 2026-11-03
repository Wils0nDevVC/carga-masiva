import { useEffect, useState } from 'react'
import { obtenerDetalleCarga } from '../api/cargas'
import type { ApiError } from '../models/ApiError'
import type { CargaDetalleDto } from '../models/Carga'
import { INTERVALO_MS } from './useHistorialCargas'

/** Estados finales: no cambian más (constitución §8). */
const ESTADOS_TERMINALES = new Set(['Notificado', 'Fallido'])

/**
 * Detalle con polling (Feature 015): mismo patrón que el historial, pero se detiene en un estado terminal o ante
 * un 404 (inexistente o ajena). Con idCarga null no consulta.
 */
export function useDetalleCarga(idCarga: number | null) {
  const [detalle, setDetalle] = useState<CargaDetalleDto | null>(null)
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState<ApiError | null>(null)
  const [noExiste, setNoExiste] = useState(false)

  useEffect(() => {
    setDetalle(null)
    setError(null)
    setNoExiste(false)
    setCargando(idCarga !== null)
    if (idCarga === null) {
      return
    }

    let activo = true
    let enCurso = false
    let terminado = false
    let temporizador: ReturnType<typeof setTimeout> | undefined
    const id = idCarga

    const debeSeguir = () => activo && !terminado && document.visibilityState === 'visible'

    async function consultar() {
      if (enCurso) {
        return
      }
      enCurso = true
      try {
        const actual = await obtenerDetalleCarga(id)
        if (activo) {
          setDetalle(actual)
          setError(null)
          terminado = ESTADOS_TERMINALES.has(actual.estado)
        }
      } catch (fallo) {
        if (activo) {
          const apiError = fallo as ApiError
          if (apiError.code === 404) {
            setNoExiste(true)
            terminado = true
          } else {
            setError(apiError)
          }
        }
      } finally {
        enCurso = false
        if (activo) {
          setCargando(false)
          clearTimeout(temporizador)
          if (debeSeguir()) {
            temporizador = setTimeout(consultar, INTERVALO_MS)
          }
        }
      }
    }

    function alCambiarVisibilidad() {
      clearTimeout(temporizador)
      if (debeSeguir()) {
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
  }, [idCarga])

  return { detalle, cargando, error, noExiste }
}
