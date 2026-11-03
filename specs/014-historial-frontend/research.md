# Research: Pantalla de Historial

**Procedencia**: `Dec` = decisión del usuario o constitución v3.8.1; `Spec` = spec 014; `F010/F011`; `Plan` = decisión técnica de este plan (alternativa más sencilla).

## D-01 Hook `useHistorialCargas` [Dec §11]

Se crea un hook porque coordina estado, consultas y efectos: el *polling* con pausa por visibilidad y la limpieza al desmontar. La constitución §11 lo admite para esa razón exacta. Devuelve `{ cargas, cargando, error }`.

## D-02 *Polling* sin solapamiento [Spec FR-004]

- **Cadena de `setTimeout`**, no `setInterval`: se consulta, se espera la respuesta y entonces se programa la siguiente a los 5 s. Así nunca hay dos consultas en curso.
- **`visibilitychange`**:
  - al ocultarse la pestaña, se cancela el temporizador pendiente;
  - al hacerse visible, se consulta de inmediato y se reanuda la cadena.
- **Al desmontar**: se cancelan el temporizador y el listener, y una bandera `activo` evita actualizar el estado tras desmontar.
- **Error**: se guarda el `ApiError`, se conservan las `cargas` anteriores y se sigue consultando. Un éxito limpia el error.
- Sin `AbortController`: la respuesta tardía se ignora con la bandera. [Plan]

## D-03 Formato y presentación

- **Fechas**: `new Date(iso).toLocaleString('es', { dateStyle: 'short', timeStyle: 'short' })`. El servidor envía UTC con `Z` (F010) y el navegador las convierte a la hora local.
- **Estado**: un color por estado con clases de Tailwind:
  - Pendiente y En proceso: amarillo;
  - Cargado y Finalizado: azul;
  - Notificado: verde;
  - Fallido: rojo.
- **Fila**: la celda del identificador y la del archivo llevan `Link` a `/historial/{idCarga}` (enlace accesible, sin `onClick` en `<tr>`).
