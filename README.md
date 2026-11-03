# Carga Masiva de Productos
Solución basada en microservicios para recibir archivos Excel, procesar productos por periodo, consultar los resultados y notificar al usuario por correo.

El procesamiento es asíncrono: la API recibe el archivo y devuelve el identificador de la carga; los servicios de procesamiento y notificaciones completan el flujo mediante RabbitMQ.

## Despliegue local
### Requisitos
- Docker con Docker Compose.
- Puertos de la solución disponibles.
- Archivo ```.env``` ya esta en el repositorio
- Postman para ejecutar la colección entregada.

### Arranque
```
docker compose up -d --build
```
- Se recomienda que todo en verde 

### Comprobar el estado
```
docker compose ps -a
```
###  Detener la solución
```
docker compose down
```
### Coleccion de Postman 
- Direcotio portable en Postman en la raiz - ```/postman ```
```
Postman/
├── carga-masiva.postman_collection.json
└── carga-masiva.postman_environment.json
```

- Para ejecutar el proyecto debe hacer login ```auth/login```, con las credencials colocas en el ```.env```, que por razones de prueba se subio al repsitorio.

```
ADMIN_EMAIL=admin@carga-masiva.test
ADMIN_CLAVE=Cme1cac81b3d4663e3aA1!
OPERADOR_EMAIL=operador@carga-masiva.test
OPERADOR_CLAVE=Cme1cac81b3d4663e3aA1!
```

### Frontend
- Directorio en la raíz: ```/cargamasiva_front``` (React, TypeScript y Vite). Se levanta con el mismo ```docker compose up -d --build``` como el servicio ```frontend```.
- Se abre en:
```
http://localhost:3000
```
- Inicia en el login: ```http://localhost:3000``` redirige a ```/historial``` y, sin sesión, a ```http://localhost:3000/login```. Use las mismas credenciales del ```.env``` indicadas arriba.

| Pantalla | Ruta |
|---|---|
| Login | http://localhost:3000/login |
| Subida de Excel | http://localhost:3000/subir |
| Historial de cargas | http://localhost:3000/historial |
| Detalle de una carga | http://localhost:3000/historial/{idCarga} |

- El frontend solo consume el Gateway: nginx reenvía ```/auth``` y ```/cargas``` a ```http://api-gateway:8080```.


### Direcciones locales


| Componente | Dirección |
|---|---|
| Gateway | http://localhost:8080 |
| Salud del Gateway | http://localhost:8080/health |
| Autenticación | http://localhost:8081 |
| Control | http://localhost:8082 |
| Carga Masiva | http://localhost:8083 |
| Notificaciones | http://localhost:8084 |
| Mailpit | http://localhost:8025 |
| Administración de RabbitMQ | http://localhost:15672 |
| SeaweedFS | http://localhost:8888 |
| SQL Server | `localhost,14333` |

## Correo de pruebas

```
http://localhost:8025
```

## Tecnologias 
| Componente | Tecnología | Uso |
|---|---|---|
| Backend | .NET 10 y ASP.NET Core | API REST y consumidores alojados en `BackgroundService` |
| Persistencia | SQL Server, EF Core y LINQ | Almacenamiento, mapeo y consultas |
| Evolución del esquema | Scripts SQL versionados | Aplicación automática mediante `database-runner` |
| Casos de uso | MediatR | Separación de comandos y consultas |
| Mensajería | RabbitMQ | Procesamiento y notificaciones asíncronas |
| Archivos | SeaweedFS | Almacenamiento del Excel original |
| Lectura de Excel | ClosedXML | Procesamiento y consulta del contenido |
| Resiliencia | Polly | Reintentos, tiempos de espera y circuit breakers |
| Correo | MailKit | Envío mediante SMTP |
| SMTP de pruebas | Mailpit | Captura y visualización local de correos |
| API Gateway | Ocelot | Enrutamiento y validación del JWT |
| Logs | Serilog | Registro técnico estructurado |
| Despliegue | Docker Compose | Arranque de la solución y sus dependencias |
| Cliente de pruebas | Postman | Ejecución de solicitudes HTTP |


## Resiliencia y consistencia

| Área | Tratamiento |
|---|---|
| Descarga de archivos | Reintentos limitados para errores transitorios |
| SQL Server | Reintentos y circuit breakers según los pipelines definidos |
| Persistencia por periodo | Transacción independiente y comprobación de si el periodo ya fue completado |
| Publicación en RabbitMQ | Reintentos limitados y confirmación del broker |
| Consumo | Confirmación manual y recuperación de cargas interrumpidas |
| Correo | Reintentos limitados sin reprocesar el Excel |


## Desarrollo con SDD y Spec Kit
El proyecto se implementó con el enfoque Spec-Driven Development (SDD), utilizando GitHub Spec Kit integrado con Claude Code. El desarrollo estuvo guiado por el criterio y la experiencia del desarrollador senior, quien definió el alcance, tomó las decisiones técnicas y revisó los resultados para cumplir los requisitos del reto tecnico sin añadir complejidad innecesaria.