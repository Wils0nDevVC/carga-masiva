Principios rectores del proyecto bistro:

1. Calidad del código:
   - Aplicar SOLID y clean code.
   - Separar responsabilidades por capas (controller, service, repository).
   - Manejo global de excepciones con @RestControllerAdvice y ProblemDetail
     (RFC 7807).
   - Validación de entrada con Bean Validation en los DTOs.

2. Arquitectura:
   - Monolito modular: un solo deployable con módulos con fronteras fuertes.
   - Módulos organizados como sub-packages: reservations, tables, notifications.
   - Cada módulo tiene su propio dominio, su repositorio y sus interfaces
     públicas.

3. Convenciones de código:
   - Identificadores, clases y métodos en inglés.
   - Comentarios, logs, mensajes de excepción y textos al cliente en español.
   - Endpoints versionados bajo /api/v1/.
   - Uso de DTOs para request y response (nunca exponer entidades JPA).
   - Uso de MapStruct para el mapeo entre entidades y DTOs.

4. Convenciones de repositorio:
   - Commits con formato clase-NN-slug.
   - README.md con instrucciones para arrancar el proyecto.