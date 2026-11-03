import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { obtenerContenidoCarga } from '../api/cargas'
import EstadoCarga from '../components/EstadoCarga'
import FechaLocal from '../components/FechaLocal'
import { useDetalleCarga } from '../hooks/useDetalleCarga'
import type { ApiError } from '../models/ApiError'
import type { ContenidoArchivoDto } from '../models/Carga'

const MENSAJE_NO_EXISTE = 'La carga no existe.'

/** Id de la URL: solo enteros positivos; cualquier otro valor se trata como inexistente, sin llamar al servidor. */
function leerId(valor: string | undefined): number | null {
  const id = Number(valor)
  return Number.isInteger(id) && id > 0 ? id : null
}

function Aviso({ tipo, children }: { tipo: 'error' | 'info'; children: React.ReactNode }) {
  const color = tipo === 'error' ? 'border-red-200 bg-red-50 text-red-700' : 'border-gray-200 bg-gray-50 text-gray-700'
  return (
    <div role={tipo === 'error' ? 'alert' : 'status'} className={`rounded border px-4 py-2 text-sm ${color}`}>
      {children}
    </div>
  )
}

/** Detalle de una carga (README §2, pantalla 3; Feature 015) con seguimiento y contenido bajo demanda. */
export default function DetallePage() {
  const idCarga = leerId(useParams().idCarga)
  const { detalle, cargando, error, noExiste } = useDetalleCarga(idCarga)

  const [contenido, setContenido] = useState<ContenidoArchivoDto | null>(null)
  const [cargandoContenido, setCargandoContenido] = useState(false)
  const [errorContenido, setErrorContenido] = useState<ApiError | null>(null)

  async function verContenido() {
    if (idCarga === null || cargandoContenido) {
      return
    }
    setCargandoContenido(true)
    setErrorContenido(null)
    try {
      setContenido(await obtenerContenidoCarga(idCarga))
    } catch (fallo) {
      setErrorContenido(fallo as ApiError)
    } finally {
      setCargandoContenido(false)
    }
  }

  if (idCarga === null || noExiste) {
    return (
      <section className="space-y-3">
        <Aviso tipo="info">{MENSAJE_NO_EXISTE}</Aviso>
        <Link to="/historial" className="text-sm text-blue-700 hover:underline">
          Volver al historial
        </Link>
      </section>
    )
  }

  if (!detalle) {
    return (
      <section className="space-y-3">
        {cargando && <p className="text-sm text-gray-600">Cargando…</p>}
        {error && <Aviso tipo="error">{error.message}</Aviso>}
      </section>
    )
  }

  return (
    <section className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">Carga #{detalle.idCarga}</h1>
        <Link to="/historial" className="text-sm text-blue-700 hover:underline">
          Volver al historial
        </Link>
      </div>

      {error && <Aviso tipo="error">{error.message}</Aviso>}

      <dl className="grid grid-cols-1 gap-3 rounded-lg border bg-white p-4 text-sm shadow-sm sm:grid-cols-3">
        <div><dt className="text-gray-500">Archivo</dt><dd>{detalle.nombreArchivo}</dd></div>
        <div><dt className="text-gray-500">Usuario</dt><dd>{detalle.usuario}</dd></div>
        <div><dt className="text-gray-500">Estado</dt><dd><EstadoCarga estado={detalle.estado} /></dd></div>
        <div><dt className="text-gray-500">Recepción</dt><dd><FechaLocal iso={detalle.fechaRecepcion} /></dd></div>
        <div><dt className="text-gray-500">Fin</dt><dd><FechaLocal iso={detalle.fechaFin} /></dd></div>
      </dl>

      <div>
        <h2 className="mb-2 font-semibold">Historial de estados</h2>
        <ol className="space-y-1 text-sm">
          {detalle.historial.map((entrada, i) => (
            <li key={`${entrada.estado}-${i}`} className="flex gap-3">
              <span className="w-32 whitespace-nowrap text-gray-500"><FechaLocal iso={entrada.fecha} /></span>
              <EstadoCarga estado={entrada.estado} />
            </li>
          ))}
        </ol>
      </div>

      <div>
        <h2 className="mb-2 font-semibold">Resultados por periodo</h2>
        {detalle.periodos.length === 0 ? (
          <p className="text-sm text-gray-600">Sin periodos</p>
        ) : (
          <Tabla
            columnas={['Periodo', 'Resultado', 'Fecha']}
            filas={detalle.periodos.map((p) => [p.periodo, p.resultado, <FechaLocal key="f" iso={p.fecha} />])}
          />
        )}
      </div>

      <div>
        <h2 className="mb-2 font-semibold">Incidencias</h2>
        {detalle.incidencias.length === 0 ? (
          <p className="text-sm text-gray-600">Sin incidencias</p>
        ) : (
          <Tabla
            columnas={['Tipo', 'Fila', 'Código', 'Periodo', 'Motivo', 'Fecha']}
            filas={detalle.incidencias.map((i) => [
              i.tipo,
              i.numeroFila ?? '',
              i.codigoProducto ?? '',
              i.periodoTexto ?? '',
              i.motivo,
              <FechaLocal key="f" iso={i.fecha} />,
            ])}
          />
        )}
      </div>

      <div>
        <h2 className="mb-2 font-semibold">Contenido del Excel</h2>
        <button
          type="button"
          onClick={verContenido}
          disabled={cargandoContenido}
          className="rounded bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
        >
          {cargandoContenido ? 'Cargando contenido…' : errorContenido ? 'Reintentar' : 'Ver contenido'}
        </button>
        <div className="mt-3 space-y-2">
          {errorContenido && <Aviso tipo="error">{errorContenido.message}</Aviso>}
          {contenido && !errorContenido && (
            <>
              <p className="text-sm text-gray-600">Hoja: {contenido.hoja}</p>
              {contenido.filas.length === 0 && <p className="text-sm text-gray-600">El archivo no tiene filas de datos</p>}
              <Tabla
                columnas={['Fila', ...contenido.encabezados.map((e) => e ?? '')]}
                filas={contenido.filas.map((f) => [f.numeroFila, ...f.celdas.map((c) => c ?? '')])}
              />
            </>
          )}
        </div>
      </div>
    </section>
  )
}

function Tabla({ columnas, filas }: { columnas: string[]; filas: React.ReactNode[][] }) {
  return (
    <div className="overflow-x-auto rounded-lg border bg-white shadow-sm">
      <table className="min-w-full text-left text-sm">
        <thead className="bg-gray-50 text-xs uppercase text-gray-500">
          <tr>
            {columnas.map((c, i) => (
              <th key={i} className="px-3 py-2">{c}</th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y">
          {filas.map((fila, i) => (
            <tr key={i}>
              {fila.map((celda, j) => (
                <td key={j} className="whitespace-pre px-3 py-1.5">{celda}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
