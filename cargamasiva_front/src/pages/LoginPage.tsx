import { useState, type FormEvent } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router'
import { useAuth } from '../hooks/useAuth'
import type { ApiError } from '../models/ApiError'

const DESTINO_POR_DEFECTO = '/historial'

/** Solo rutas internas de la aplicación; /login o un valor ausente → /historial. */
function destinoTrasLogin(estado: unknown): string {
  const desde = (estado as { desde?: unknown } | null)?.desde
  return typeof desde === 'string' && desde.startsWith('/') && !desde.startsWith('/login') ? desde : DESTINO_POR_DEFECTO
}

/** Login (README §2, pantalla 0; Feature 012). Credenciales contra el Gateway mediante el contexto de sesión. */
export default function LoginPage() {
  const { estaAutenticado, iniciarSesion } = useAuth()
  const navigate = useNavigate()
  const ubicacion = useLocation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [enviando, setEnviando] = useState(false)
  const [error, setError] = useState<ApiError | null>(null)

  if (estaAutenticado) {
    return <Navigate to={DESTINO_POR_DEFECTO} replace />
  }

  async function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    if (enviando) {
      return
    }
    setEnviando(true)
    setError(null)
    try {
      await iniciarSesion(email, password)
      navigate(destinoTrasLogin(ubicacion.state), { replace: true })
    } catch (fallo) {
      setError(fallo as ApiError)
      setPassword('')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <section className="mx-auto mt-10 max-w-sm rounded-lg border bg-white p-6 shadow-sm">
      <h1 className="mb-6 text-xl font-semibold">Iniciar sesión</h1>
      <form onSubmit={enviar} className="space-y-4">
        <label className="block">
          <span className="text-sm font-medium">Correo</span>
          <input
            type="email"
            required
            autoComplete="username"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className="mt-1 w-full rounded border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </label>
        <label className="block">
          <span className="text-sm font-medium">Contraseña</span>
          <input
            type="password"
            required
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className="mt-1 w-full rounded border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </label>

        {error && (
          <div role="alert" className="rounded border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            <p>{error.message}</p>
            {error.errors.length > 0 && (
              <ul className="mt-1 list-disc pl-5">
                {error.errors.map((detalle) => (
                  <li key={detalle}>{detalle}</li>
                ))}
              </ul>
            )}
          </div>
        )}

        <button
          type="submit"
          disabled={enviando}
          className="w-full rounded bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
        >
          {enviando ? 'Ingresando…' : 'Ingresar'}
        </button>
      </form>
    </section>
  )
}
