import { Navigate, Outlet, useLocation } from 'react-router'
import { useAuth } from '../hooks/useAuth'

/** Sin sesión, las rutas protegidas redirigen a /login recordando la ruta solicitada (Feature 012). */
export default function RutaProtegida() {
  const { estaAutenticado } = useAuth()
  const ubicacion = useLocation()
  if (estaAutenticado) {
    return <Outlet />
  }
  return <Navigate to="/login" replace state={{ desde: ubicacion.pathname + ubicacion.search }} />
}
