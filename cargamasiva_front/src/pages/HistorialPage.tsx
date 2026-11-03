import { Link } from 'react-router'
import EstadoCarga from '../components/EstadoCarga'
import FechaLocal from '../components/FechaLocal'
import { useHistorialCargas } from '../hooks/useHistorialCargas'

/** Historial de cargas (README §2, pantalla 2; Feature 014) con seguimiento por polling. */
export default function HistorialPage() {
  const { cargas, cargando, error } = useHistorialCargas()

  return (
    <section>
      <h1 className="mb-4 text-xl font-semibold">Historial de cargas</h1>

      {error && (
        <div role="alert" className="mb-4 rounded border border-red-200 bg-red-50 px-4 py-2 text-sm text-red-700">
          {error.message}
        </div>
      )}

      {cargas === null && cargando && <p className="text-sm text-gray-600">Cargando…</p>}

      {cargas !== null && cargas.length === 0 && <p className="text-sm text-gray-600">No hay cargas registradas</p>}

      {cargas !== null && cargas.length > 0 && (
        <div className="overflow-x-auto rounded-lg border bg-white shadow-sm">
          <table className="min-w-full text-left text-sm">
            <thead className="bg-gray-50 text-xs uppercase text-gray-500">
              <tr>
                <th className="px-4 py-2">Id</th>
                <th className="px-4 py-2">Archivo</th>
                <th className="px-4 py-2">Usuario</th>
                <th className="px-4 py-2">Recepción</th>
                <th className="px-4 py-2">Estado</th>
                <th className="px-4 py-2">Fin</th>
                <th className="px-4 py-2"><span className="sr-only">Acciones</span></th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {cargas.map((carga) => (
                <tr key={carga.idCarga} className="hover:bg-gray-50">
                  <td className="px-4 py-2">
                    <Link to={`/historial/${carga.idCarga}`} className="text-blue-700 hover:underline">
                      {carga.idCarga}
                    </Link>
                  </td>
                  <td className="px-4 py-2">
                    <Link to={`/historial/${carga.idCarga}`} className="hover:underline">
                      {carga.nombreArchivo}
                    </Link>
                  </td>
                  <td className="px-4 py-2">{carga.usuario}</td>
                  <td className="px-4 py-2 whitespace-nowrap"><FechaLocal iso={carga.fechaRecepcion} /></td>
                  <td className="px-4 py-2">
                    <EstadoCarga estado={carga.estado} />
                  </td>
                  <td className="px-4 py-2 whitespace-nowrap"><FechaLocal iso={carga.fechaFin} /></td>
                  <td className="px-4 py-2 text-right">
                    <Link
                      to={`/historial/${carga.idCarga}`}
                      className="inline-block rounded bg-blue-600 px-3 py-1 text-xs font-medium text-white hover:bg-blue-700"
                    >
                      Ver detalle
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}
