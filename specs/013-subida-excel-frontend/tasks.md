# Tasks: Pantalla de Subida de Excel

**Input**: `specs/013-subida-excel-frontend/` (plan.md, spec.md, research.md, data-model.md, contracts/subida-ui.md, quickstart.md)

**Alcance**: `cargamasiva_front/src/models/Carga.ts`, `cargamasiva_front/src/api/cargas.ts` y `cargamasiva_front/src/pages/SubirPage.tsx`.

**Reglas**:
- Sin dependencias ni hooks nuevos.
- Sin validaciones de tamaño o nombre en el cliente.
- Sin pruebas automatizadas.
- `[x]` solo para lo ejecutado.
- Verificar sobre una base temporal (`CargaMasivaApp_F013`).
- Commit en español, sin `Co-Authored-By`.

## Phase 1: US1 - Subir un Excel (P1) 🎯 MVP

**Objetivo**: subir un `.xlsx`, ver el `idCarga` con un enlace al detalle y ver los errores de `Response<T>`.

**Prueba independiente**: los escenarios 1 a 7 del quickstart.

- [x] T001 [P] [US1] Crear `cargamasiva_front/src/models/Carga.ts` con `export interface CargaRegistradaDto { idCarga: number; estado: string }` (F005, 202).
- [x] T002 [US1] Crear `cargamasiva_front/src/api/cargas.ts` con `subirCarga(archivo: File): Promise<CargaRegistradaDto>`:
  - `const datos = new FormData(); datos.append('archivo', archivo)`.
  - `return desempaquetar(cliente.post<Response<CargaRegistradaDto>>('/cargas', datos))`.
  - **Sin fijar `Content-Type`** (research D-01).
- [x] T003 [US1] Reescribir `cargamasiva_front/src/pages/SubirPage.tsx` (research D-02, D-03; `contracts/subida-ui.md`):
  - Estado: `archivo: File | null`, `enviando`, `resultado: CargaRegistradaDto | null` y `error: ApiError | null`. Un `ref` al input.
  - Input `type="file"`, `accept=".xlsx"`, `required`. Su `onChange` toma `files?.[0] ?? null`.
  - Botón «Subir»: con el texto «Subiendo…» y `disabled` mientras `enviando` o si no hay archivo.
  - **Envío** (`onSubmit`): `preventDefault`; si ya se está enviando o no hay archivo, no hacer nada; limpiar el error y el resultado; `await subirCarga(archivo)`.
    - **Si va bien**: guardar el resultado, vaciar `archivo` y el input (`ref.current.value = ''`).
    - **Si falla**: `setError(fallo as ApiError)`.
    - **Al terminar**: `enviando = false`.
  - **Éxito**: «Carga registrada», `idCarga #{idCarga}`, `estado` y `<Link to={/historial/${idCarga}}>Ver detalle</Link>`.
  - **Error**: `message` y la lista `errors` si no está vacía.
  - Estilos con Tailwind; sin `console`.
- [x] T004 [US1] `npm run build` en `cargamasiva_front/` con 0 errores y `docker compose up -d --build frontend` (o la pila completa sobre `CargaMasivaApp_F013` para verificar).
- [ ] T005 [US1] Verificar los escenarios 1 a 7 del quickstart en el navegador y registrar los resultados en `specs/013-subida-excel-frontend/quickstart.md`.

## Estado (2026-11-03)

- **T001–T004 hechas**: `npm run build` con 0 errores; 0 usos de `console`; `frontend` reconstruido y `healthy`.
- **T005 parcial**: la llamada se verificó con el `subirCarga` real (desde `tsx`, contra nginx y sobre `CargaMasivaApp_F013`). La interfaz no se probó en un navegador (ninguno disponible); ver `quickstart.md`.

## Dependencias y paralelismo

- T001 → T002 → T003 → T004 → T005. T001 se puede hacer en paralelo con la lectura del contrato.

## Estrategia

Una sola historia, que es el MVP.
