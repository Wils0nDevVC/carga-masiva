import { useRef, useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { subirCarga } from '../api/cargas'
import type { ApiError } from '../models/ApiError'
import type { CargaRegistradaDto } from '../models/Carga'

/** Subida de Excel (README §2, pantalla 1; Feature 013). Las validaciones del archivo las hace Control. */
export default function SubirPage() {
  const entrada = useRef<HTMLInputElement>(null)
  const [archivo, setArchivo] = useState<File | null>(null)
  const [enviando, setEnviando] = useState(false)
  const [resultado, setResultado] = useState<CargaRegistradaDto | null>(null)
  const [error, setError] = useState<ApiError | null>(null)

  async function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    if (enviando || !archivo) {
      return
    }
    setEnviando(true)
    setError(null)
    setResultado(null)
    try {
      setResultado(await subirCarga(archivo))
      setArchivo(null)
      if (entrada.current) {
        entrada.current.value = ''
      }
    } catch (fallo) {
      setError(fallo as ApiError)
    } finally {
      setEnviando(false)
    }
  }

  return (
    <section className="max-w-lg">
      <h1 className="mb-6 text-xl font-semibold">Subida de Excel</h1>
      <form onSubmit={enviar} className="space-y-4 rounded-lg border bg-white p-6 shadow-sm">
        <label className="block">
          <span className="text-sm font-medium">Archivo (.xlsx)</span>
          <input
            ref={entrada}
            type="file"
            accept=".xlsx"
            required
            onChange={(e) => setArchivo(e.target.files?.[0] ?? null)}
            className="mt-1 block w-full text-sm file:mr-3 file:rounded file:border-0 file:bg-gray-100 file:px-3 file:py-2"
          />
        </label>
        <button
          type="submit"
          disabled={enviando || !archivo}
          className="rounded bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
        >
          {enviando ? 'Subiendo…' : 'Subir'}
        </button>
      </form>

      {resultado && (
        <div role="status" className="mt-4 rounded border border-green-200 bg-green-50 px-4 py-3 text-sm text-green-800">
          <p className="font-medium">Carga registrada</p>
          <p>
            Carga #{resultado.idCarga} · estado {resultado.estado}
          </p>
          <Link to={`/historial/${resultado.idCarga}`} className="mt-1 inline-block underline">
            Ver detalle
          </Link>
        </div>
      )}

      {error && (
        <div role="alert" className="mt-4 rounded border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
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
    </section>
  )
}
