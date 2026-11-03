# Tasks: Pantalla de Historial de cargas

**Input**: `specs/014-historial-frontend/` (plan.md, spec.md, research.md, data-model.md, contracts/historial-ui.md, quickstart.md)

**Alcance**: `cargamasiva_front/src/{models/Carga.ts, api/cargas.ts, hooks/useHistorialCargas.ts, pages/HistorialPage.tsx}`.

**Reglas**:
- Sin dependencias, paginación, filtros ni WebSockets.
- Sin pruebas automatizadas.
- `[x]` solo para lo ejecutado.
- Las subidas de prueba se hacen sobre una base temporal.
- Commit en español, sin `Co-Authored-By`.

## Phase 1: US1 - Ver el historial y seguir los estados (P1) 🎯 MVP

**Objetivo**: tabla de `GET /cargas` con enlaces al detalle, estado vacío, errores y *polling* cada 5 s solo con la pestaña visible.

**Prueba independiente**: los escenarios 1 a 8 del quickstart.

- [x] T001 [US1] Añadir a `cargamasiva_front/src/models/Carga.ts` el tipo `export interface CargaResumenDto { idCarga: number; nombreArchivo: string; usuario: string; fechaRecepcion: string; estado: string; fechaFin: string | null }` (F010).
- [x] T002 [US1] Añadir a `cargamasiva_front/src/api/cargas.ts` la función `listarCargas(): Promise<CargaResumenDto[]>`, que hace `desempaquetar(cliente.get<Response<CargaResumenDto[]>>('/cargas'))`.
- [x] T003 [US1] Crear `cargamasiva_front/src/hooks/useHistorialCargas.ts` (research D-02), que devuelve `{ cargas: CargaResumenDto[] | null, cargando: boolean, error: ApiError | null }`:
  - **Arranque**: un `useEffect` con la bandera `activo = true`.
  - **Consulta**: `consultar()` llama a `listarCargas()`.
    - Si va bien, `setCargas` y `setError(null)`.
    - Si falla, `setError` y se conservan las `cargas`.
    - Al terminar, `setCargando(false)` y, si `activo` y `document.visibilityState === 'visible'`, programa la siguiente con `temporizador = setTimeout(consultar, 5000)`.
  - **Visibilidad** (`visibilitychange`):
    - al ocultarse, `clearTimeout`;
    - al hacerse visible, `clearTimeout` y `consultar()` inmediato, sin solapar: con una bandera `enCurso`, no se lanza si ya hay una consulta en curso.
  - **Limpieza**: `activo = false`, `clearTimeout` y quitar el listener.
  - Constante `INTERVALO_MS = 5000`. Sin `console`.
- [x] T004 [US1] Reescribir `cargamasiva_front/src/pages/HistorialPage.tsx` (research D-03; `contracts/historial-ui.md`):
  - **Estados de la pantalla**: «Cargando…» mientras `cargas === null` y `cargando`; «No hay cargas registradas» si la lista está vacía.
  - **Tabla** con las columnas Id, Archivo, Usuario, Recepción, Estado y Fin:
    - Id y Archivo con `Link` a `/historial/{idCarga}`;
    - fechas con `toLocaleString('es', { dateStyle: 'short', timeStyle: 'short' })` y Fin vacío si es null;
    - estado con color: Pendiente y En proceso amarillo; Cargado y Finalizado azul; Notificado verde; Fallido rojo.
  - **Error**: `error.message` en un aviso, sin ocultar la tabla.
  - Estilos con Tailwind.
- [x] T005 [US1] `npm run build` en `cargamasiva_front/` con 0 errores y `docker compose up -d --build frontend`.
- [ ] T006 [US1] Verificar los escenarios 1 a 8 del quickstart y registrar los resultados en `specs/014-historial-frontend/quickstart.md`.

## Estado (2026-11-03)

- **T001–T005 hechas**: `npm run build` con 0 errores; 0 usos de `console`; `frontend` reconstruido y `healthy`.
- **T006 parcial**: `listarCargas()` se verificó con el módulo real contra nginx. El *polling*, la visibilidad y la interfaz requieren un navegador, que no estaba disponible; ver `quickstart.md`.

## Dependencias y paralelismo

- T001 → T002 → T003 → T004 → T005 → T006 (cadena de tipos e importaciones).

## Estrategia

Una sola historia, que es el MVP.
