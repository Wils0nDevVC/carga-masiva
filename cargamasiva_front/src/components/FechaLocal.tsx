/** Fecha ISO (UTC) del servidor en la hora local del navegador; nada si es null. */
export default function FechaLocal({ iso }: { iso: string | null }) {
  return <>{iso ? new Date(iso).toLocaleString('es', { dateStyle: 'short', timeStyle: 'short' }) : ''}</>
}
