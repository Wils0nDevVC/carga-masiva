const COLOR_ESTADO: Record<string, string> = {
  Pendiente: 'bg-yellow-100 text-yellow-800',
  'En proceso': 'bg-yellow-100 text-yellow-800',
  Cargado: 'bg-blue-100 text-blue-800',
  Finalizado: 'bg-blue-100 text-blue-800',
  Notificado: 'bg-green-100 text-green-800',
  Fallido: 'bg-red-100 text-red-800',
}

/** Insignia del estado de una carga (Historial y Detalle). */
export default function EstadoCarga({ estado }: { estado: string }) {
  return (
    <span className={`rounded px-2 py-0.5 text-xs font-medium ${COLOR_ESTADO[estado] ?? 'bg-gray-100 text-gray-800'}`}>
      {estado}
    </span>
  )
}
