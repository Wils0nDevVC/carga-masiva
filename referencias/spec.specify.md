Feature: ciclo de vida de una reserva en bistro.

CONTEXTO DEL SISTEMA
bistro es el backend de reservas de UN restaurante (no una plataforma de
múltiples locales). Es backend puro: se prueba por HTTP, no tiene interfaz
gráfica. En esta etapa NO hay autenticación de usuarios: cualquier cliente
puede crear una reserva enviando sus datos en la request.

QUÉ HACE ESTA FEATURE
Un cliente solicita una mesa para una franja horaria. El sistema verifica si
hay una mesa disponible con capacidad suficiente, y en el mismo flujo decide:
si hay mesa, la asigna y confirma la reserva; si no hay, la rechaza. El
resultado es inmediato y terminal.

ACTOR
- Cliente: envía la solicitud de reserva vía HTTP. Se identifica con su nombre
  y un dato de contacto (email), no con una cuenta de usuario.

CASOS DE USO
- El cliente crea una solicitud de reserva indicando: nombre, email, fecha y
  hora de la reserva, y cantidad de comensales. El sistema responde con un
  código de reserva y el estado.
- El sistema evalúa la disponibilidad. Si hay una mesa con capacidad
  suficiente libre en esa franja, la asigna y la reserva queda CONFIRMED. Si
  no hay, la reserva queda REJECTED.
- El cliente puede consultar el estado de su reserva usando el código.

APIS (bajo /api/v1/)
- POST /api/v1/reservations: crea la solicitud. Recibe nombre, email, fecha y
  hora de la reserva, y cantidad de comensales. Devuelve el código de reserva y
  el estado inicial.
- GET /api/v1/reservations/{reservationCode}: devuelve el estado actual de la
  reserva.

REGLAS DE DOMINIO
- Estados posibles de una reserva: PENDING, CONFIRMED, REJECTED.
- La reserva nace en PENDING y termina en CONFIRMED o REJECTED. No hay otros
  estados.
- Una reserva solo puede pasar a CONFIRMED si se le asignó una mesa.
- Una mesa ya asignada a una reserva en una franja horaria no puede asignarse
  a otra reserva en la misma franja.
- Que no haya mesa disponible es un rechazo de negocio (REJECTED), NO un error
  del sistema. La API responde exitosamente informando el rechazo.

ENTIDAD PRINCIPAL
- Reservation: reservationCode (identificador público de la reserva),
  customerName, customerEmail, reservationTime (fecha y hora de la reserva),
  partySize (cantidad de comensales), status, assignedTableId (la mesa
  asignada, si la hay).

LÍMITES (qué NO incluye esta feature)
- NO hay autenticación ni cuentas de usuario.
- NO hay pagos, ni menú, ni pedidos de comida.
- NO hay cancelación ni reprogramación de reservas.
- NO hay lista de espera.
- NO hay gestión de las mesas en sí (crear, editar mesas): eso es otra feature.
- NO hay envío de notificaciones: eso es otra feature.
- NO definas métricas de negocio (satisfacción, tiempos, concurrencia): no
  aplican en esta etapa.
- Respetá estos límites: no agregues funcionalidad que no esté descrita acá.