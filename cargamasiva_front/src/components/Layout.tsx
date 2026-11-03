import { Link, Outlet } from 'react-router'
import { useAuth } from '../hooks/useAuth'

export default function Layout() {
  const { estaAutenticado, cerrarSesion } = useAuth()
  return (
    <div className="min-h-screen bg-gray-50 text-gray-900">
      <header className="border-b bg-white">
        <nav className="mx-auto flex max-w-5xl items-center gap-6 px-4 py-3">
          <span className="font-semibold">Carga masiva</span>
          <Link to="/subir" className="text-sm hover:underline">Subir Excel</Link>
          <Link to="/historial" className="text-sm hover:underline">Historial</Link>
          <span className="ml-auto">
            {estaAutenticado ? (
              <button type="button" onClick={cerrarSesion} className="text-sm hover:underline">
                Cerrar sesión
              </button>
            ) : (
              <Link to="/login" className="text-sm hover:underline">Iniciar sesión</Link>
            )}
          </span>
        </nav>
      </header>
      <main className="mx-auto max-w-5xl px-4 py-6">
        <Outlet />
      </main>
    </div>
  )
}
