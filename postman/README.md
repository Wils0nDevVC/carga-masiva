# Colección Postman

Peticiones del reto contra el **API Gateway** (`http://localhost:8080` con Docker Compose).

## Importar

1. En Postman, **Import** → selecciona `carga-masiva.postman_collection.json` y `carga-masiva.postman_environment.json`.
2. Activa el entorno **Carga masiva - local (Compose)**.
3. En el entorno, rellena `email` y `password` con un usuario inicial: los valores de `ADMIN_EMAIL`/`ADMIN_CLAVE` u `OPERADOR_EMAIL`/`OPERADOR_CLAVE` de tu `.env`. No se incluyen credenciales en estos archivos.

## Uso

1. **Autenticación → Login**: guarda `accessToken` y `refreshToken` en el entorno. Las demás peticiones usan el token automáticamente (Bearer, heredado de la colección).
2. **Cargas → Subir Excel**: en *Body → form-data*, en la fila `archivo` pulsa **Select Files** y elige tu `.xlsx` local; por ejemplo, `referencias/carga_masiva_productos (1).xlsx`. Guarda `idCarga`.
3. **Historial**, **Detalle** y **Contenido**: usan `{{idCarga}}`. Repite **Historial** o **Detalle** para seguir el estado: Pendiente → En proceso → Cargado → Finalizado → Notificado. El correo se ve en Mailpit (`http://localhost:8025`).
4. Cuando el token de acceso venza (60 min) recibirás 401: ejecuta **Refresh**. El refresh token dura 1 h y **rota**: cada uso devuelve uno nuevo y reutilizar el anterior da 401. Si vence, vuelve a hacer **Login**.

Notas:
- El Operador solo ve sus propias cargas; el Administrador ve todas.
- Subidas: 10 por minuto por usuario (429 si se supera).
