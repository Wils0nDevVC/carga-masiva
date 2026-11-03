# Tasks: Pantalla de Detalle de una carga

**Input**: `specs/015-detalle-frontend/` (plan.md, spec.md, research.md, data-model.md, contracts/detalle-ui.md, quickstart.md)

**Alcance**: `cargamasiva_front/src/{models/Carga.ts, api/cargas.ts, hooks/useDetalleCarga.ts, components/EstadoCarga.tsx, components/FechaLocal.tsx, pages/DetallePage.tsx, pages/HistorialPage.tsx}`.

**Reglas**:
- Sin dependencias, paginación, filtros ni descarga.
- Sin un hook genérico de *polling* y sin pruebas automatizadas.
- `[x]` solo para lo ejecutado.
- Las pruebas que crean datos van sobre una base temporal.
- Commit en español, sin `Co-Authored-By`.

## Phase 1: Foundational

- [x] T001 [P] Añadir a `cargamasiva_front/src/models/Carga.ts` los tipos de data-model.md:
  - `EstadoHistorialDto`, `ResultadoPeriodoDto` e `IncidenciaDto` (`numeroFila`, `codigoProducto` y `periodoTexto` son nullables);
  - `CargaDetalleDto` = `CargaResumenDto` más `historial`, `periodos` e `incidencias`;
  - `FilaContenidoDto` (`celdas: (string | null)[]`) y `ContenidoArchivoDto` (`encabezados: (string | null)[]`).
- [x] T002 [P] Crear `cargamasiva_front/src/components/EstadoCarga.tsx`, que es la insignia con el mapa de colores de `HistorialPage` (Pendiente y En proceso amarillo; Cargado y Finalizado azul; Notificado verde; Fallido rojo; gris por defecto). Crear también `cargamasiva_front/src/components/FechaLocal.tsx`, que muestra `toLocaleString('es', { dateStyle: 'short', timeStyle: 'short' })` o nada si el valor es null.
- [x] T003 Refactorizar `cargamasiva_front/src/pages/HistorialPage.tsx` para usar `EstadoCarga` y `FechaLocal`, quitando `COLOR_ESTADO` y `formatearFecha`, sin cambiar el comportamiento (research D-03).
- [x] T004 Añadir a `cargamasiva_front/src/api/cargas.ts`:
  - `obtenerDetalleCarga(id: number)`: `GET /cargas/${id}`;
  - `obtenerContenidoCarga(id: number)`: `GET /cargas/${id}/contenido`.
  - Las dos con `desempaquetar`.

## Phase 2: US1 - Ver el detalle y seguir el estado (P1) 🎯 MVP

**Prueba independiente**: los escenarios 1 a 4 y 7 del quickstart.

- [x] T005 [US1] Crear `cargamasiva_front/src/hooks/useDetalleCarga.ts` (research D-01), que devuelve `{ detalle, cargando, error, noExiste }`:
  - **Base**: el mismo patrón que `useHistorialCargas` (bandera `activo`, `enCurso`, cadena de `setTimeout` de 5000 ms y `visibilitychange`), en un efecto que depende de `idCarga`. Al cambiar el id, se reinician los estados.
  - **Respuesta correcta**: `setDetalle` y `setError(null)`.
  - **Error**:
    - con `code === 404`, se marca `noExiste = true`;
    - con otro código, se guarda el error y se conserva el detalle.
  - **Se programa la siguiente consulta** solo si `activo`, la pestaña está visible, no es `noExiste` y el último estado no es `Notificado` ni `Fallido` (se guarda en una variable local del efecto). Lo mismo al volver la visibilidad.
  - Sin `console`.
- [x] T006 [US1] Reescribir `cargamasiva_front/src/pages/DetallePage.tsx`, parte del detalle (`contracts/detalle-ui.md`):
  - **Identificador**: `Number(useParams().idCarga)`. Si no es un entero > 0, se muestra «La carga no existe.» con un enlace «Volver al historial» y no se llama al servidor; el hook se invoca con `null` y no consulta.
  - **Estados de la pantalla**: «Cargando…» al principio y «La carga no existe.» si `noExiste`; el aviso de `error.message` si hay error.
  - **Contenido de la página**:
    - datos generales (id, archivo, usuario, `FechaLocal` de recepción, `EstadoCarga` y `FechaLocal` de fin);
    - historial de estados como lista cronológica;
    - tabla de periodos o «Sin periodos»;
    - tabla de incidencias (tipo, fila, código, periodo, motivo y fecha) o «Sin incidencias».
  - Estilos con Tailwind.

## Phase 3: US2 - Ver el contenido del Excel (P2)

**Prueba independiente**: los escenarios 5 y 6 del quickstart.

- [x] T007 [US2] Añadir a `cargamasiva_front/src/pages/DetallePage.tsx` el contenido bajo demanda (research D-02):
  - **Estado local**: `contenido`, `cargandoContenido` y `errorContenido`.
  - **Botón**: «Ver contenido» (o «Reintentar» tras un error), deshabilitado con «Cargando contenido…» mientras carga, que llama a `obtenerContenidoCarga(id)`.
  - **Resultado**:
    - hoja y tabla con la columna «Fila» más los encabezados (un encabezado `null` se ve vacío) y las celdas tal cual (`null` se ve en blanco);
    - sin filas: «El archivo no tiene filas de datos»;
    - error: `errorContenido.message` como aviso de error, nunca como contenido vacío.
  - El *polling* no toca este estado.
- [x] T008 `npm run build` en `cargamasiva_front/` con 0 errores y `docker compose up -d --build frontend`.
- [ ] T009 Verificar el quickstart: las llamadas con los módulos reales y la interfaz en el navegador, si hay uno disponible. Registrar los resultados en `specs/015-detalle-frontend/quickstart.md`.

## Estado (2026-11-03)

- **T001–T008 hechas**: `npm run build` con 0 errores; 0 usos de `console`; `frontend` reconstruido. `HistorialPage` usa ahora `EstadoCarga` y `FechaLocal`, sin cambios de comportamiento.
- **T009 parcial**: las llamadas se verificaron con los módulos reales contra nginx (solo lecturas sobre `CargaMasivaApp`). La interfaz y el *polling* requieren un navegador, que no estaba disponible; ver `quickstart.md`.

## Dependencias y paralelismo

- T001 y T002 en paralelo. T003 depende de T002. T004 depende de T001.
- T005 depende de T004. T006 depende de T002 y T005. T007 depende de T006 (mismo archivo). Después, T008 y T009.

## Estrategia

- **MVP**: US1, el detalle con seguimiento del estado.
- **US2**: añade el contenido bajo demanda.
