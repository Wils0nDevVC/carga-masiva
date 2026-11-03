# Data Model: Historial

| Modelo (`cargamasiva_front/src/models/Carga.ts`) | Forma | Fuente |
|---|---|---|
| `CargaResumenDto` | `{ idCarga: number; nombreArchivo: string; usuario: string; fechaRecepcion: string; estado: string; fechaFin: string \| null }` | F010 `contracts/consultas-api.md` (`GET /cargas`) |

Estado del hook: `cargas: CargaResumenDto[] | null` (null hasta la primera respuesta), `cargando: boolean` y `error: ApiError | null`.
