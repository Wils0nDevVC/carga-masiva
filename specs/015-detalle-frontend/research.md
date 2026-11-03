# Research: Pantalla de Detalle

**Procedencia**: `Dec` = decisión del usuario o constitución v3.8.1; `Spec` = spec 015; `F010/F011/F014`; `Plan` = decisión técnica de este plan (alternativa más sencilla).

## D-01 Hook `useDetalleCarga(idCarga)` [Dec §11] [F014]

- Mismo patrón que `useHistorialCargas`:
  - cadena de `setTimeout` de 5 s, sin solapamiento;
  - pausa con `visibilitychange`;
  - limpieza al desmontar.
- **Diferencias**:
  - **No se programa la siguiente consulta** si:
    - el último detalle está en un estado terminal (`Notificado` o `Fallido`), o
    - la consulta devolvió 404 (`noExiste = true`).
  - **Al volver a la pestaña**: se consulta solo si no es terminal ni 404.
  - **Cambio de `idCarga`** (navegación entre detalles): se reinicia el estado (el efecto depende de `idCarga`).
- **Devuelve** `{ detalle, cargando, error, noExiste }`.
- No se generaliza un hook de *polling* compartido: hay dos usos con condiciones distintas, y una abstracción para dos casos sería anticipada (§1, §12). [Plan]

## D-02 Contenido bajo demanda, en la página [Spec FR-004, FR-005]

- Estado local en `DetallePage`: `contenido`, `cargandoContenido` y `errorContenido`. No hay hook: es una acción única de la página, sin efectos ni consultas repetidas.
- «Ver contenido» llama a `obtenerContenidoCarga(id)`. Si hay error, lo muestra y el botón sigue disponible para reintentar. El *polling* del detalle no toca este estado.

## D-03 Componentes compartidos con el Historial [Plan]

- `components/EstadoCarga.tsx`: insignia de estado con los colores de la Feature 014.
- `components/FechaLocal.tsx`: muestra una fecha ISO en hora local (`es`, `short`) o nada si es null.
- `HistorialPage` pasa a usarlos, así se elimina la duplicación sin cambiar su comportamiento.

## D-04 Identificador de la ruta

- `useParams().idCarga` se convierte con `Number(...)`.
- Si no es un entero > 0, se trata como inexistente («La carga no existe.») sin llamar al servidor. Así se evitan el 400 del servidor y el 404 del Gateway para un texto en la URL.
- Coherente con la spec: inexistente equivale a no visible.
