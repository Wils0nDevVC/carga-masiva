import { useParams } from 'react-router'

export default function DetallePage() {
  const { idCarga } = useParams()
  return (
    <section>
      <h1 className="text-xl font-semibold">Detalle de la carga {idCarga}</h1>
      <p className="mt-2 text-sm text-gray-600">Pendiente de implementar en su feature.</p>
    </section>
  )
}
