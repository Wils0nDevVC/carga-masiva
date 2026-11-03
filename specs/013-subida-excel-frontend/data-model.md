# Data Model: Subida de Excel

| Modelo (`cargamasiva_front/src/models/Carga.ts`) | Forma | Fuente |
|---|---|---|
| `CargaRegistradaDto` | `{ idCarga: number; estado: string }` | F005 `contracts/http-recepcion-carga.md` (202) |

Se reutilizan `Response<T>` y `ApiError` de la Feature 011.
