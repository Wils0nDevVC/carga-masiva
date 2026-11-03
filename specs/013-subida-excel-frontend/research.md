# Research: Pantalla de Subida de Excel

**Procedencia**: `Dec` = decisión del usuario o constitución v3.8.1; `Spec` = spec 013; `F005/F011`; `Plan` = decisión técnica de este plan (alternativa más sencilla).

## D-01 Llamada HTTP [Spec FR-002] [F005] [F011]

- `api/cargas.ts` con `subirCarga(archivo: File): Promise<CargaRegistradaDto>`.
  - Construye un `FormData` con `append('archivo', archivo)` y hace `desempaquetar(cliente.post<Response<CargaRegistradaDto>>('/cargas', datos))`.
- **No se fija `Content-Type`**: Axios lo deriva del `FormData`, con el `boundary`. Fijarlo a mano rompería el `boundary`.
- El Bearer y la renovación los aporta la instancia común de la 011. En el reintento tras una renovación, Axios reenvía el mismo `FormData`, que es reutilizable en el navegador.

## D-02 Estado de la página, sin hook nuevo [Dec §11] [Plan]

- Estado local en `SubirPage.tsx`: `archivo: File | null`, `enviando`, `resultado: CargaRegistradaDto | null` y `error: ApiError | null`.
- No se crea un hook: el estado es de una sola página (mismo criterio que la Feature 012).
- Tras el éxito se limpia el selector con un `ref` al input y `value = ''`, y se borra `archivo`.

## D-03 Mensaje de éxito

`subirCarga` devuelve `data`. El texto «Carga registrada» es el `message` del contrato (F005), pero `desempaquetar` solo entrega `data`, así que la página muestra el texto fijo «Carga registrada» junto con `idCarga` y `estado`. Coincide con el contrato y evita ampliar `desempaquetar`. [Plan]
