# Contrato de la pantalla de Historial (`/historial`, protegida)

| Situación | Comportamiento |
|---|---|
| Primera consulta en curso | «Cargando…» |
| `data` vacía | «No hay cargas registradas» |
| Con cargas | Tabla con las columnas Id, Archivo, Usuario, Recepción, Estado y Fin (vacío si `fechaFin` es null), en el orden recibido; Id y Archivo enlazan a `/historial/{idCarga}`; además, cada fila tiene un botón «Ver detalle» con el mismo destino (petición del usuario, 2026-11-03) |
| Cada 5 s, con la pestaña visible | Nueva consulta tras terminar la anterior |
| Pestaña oculta | Sin consultas; al volver, una consulta inmediata |
| Error | `message` del `ApiError`, conservando la última tabla |
| 401 | Renovación transparente (F011); si falla, `/login` |
