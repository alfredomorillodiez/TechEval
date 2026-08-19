## Context

TechEval es una app .NET (Domain/Application/Infrastructure/API/Web, Blazor WebAssembly) desplegada como stack de Docker Compose de una sola instancia (`sqlserver`, `api`, `web`, y `mailhog` en `dev`) en un servidor con hardware modesto (sin GPU útil para IA, CPU-bound). No existe hoy ninguna infraestructura de background jobs (`BackgroundService`, Hangfire, SignalR) ni ninguna integración con un modelo de IA.

El administrador ya probó localmente Ollama con `deepseek-r1:7b` y confirmó que genera texto coherente para preguntas de opción múltiple. El servidor real tendrá características similares a esa prueba (mismo tipo de CPU, sin GPU dedicada a IA), y todo — incluyendo Ollama — correrá en la misma máquina.

## Goals / Non-Goals

**Goals:**
- Generar preguntas nuevas a partir de un tema en texto libre, categoría, dificultad y tipo elegidos por el administrador.
- Procesar la generación en segundo plano, sin bloquear la interfaz del administrador.
- Garantizar que ninguna pregunta generada por IA quede utilizable en una prueba sin aprobación humana explícita.
- Aislar el fallo de una pregunta individual dentro de un lote, sin perder las demás.

**Non-Goals:**
- No se introduce un broker de mensajería ni Hangfire — la app corre en una sola instancia, una cola en memoria es suficiente.
- El modelo de IA no elige categoría, dificultad ni tipo — eso lo fija el administrador antes de generar.
- No se implementa lógica de limitación/pausa de la generación durante exámenes en curso (descartado como escenario real).
- No se soporta otro proveedor de IA (nube) en esta iteración — solo Ollama local.

## Decisions

**1. Modelo de datos: job + items, no solo un campo de estado en `Question`.**
Se añade `QuestionReviewStatus` (`Approved` | `PendingReview` | `Rejected`) a `Question` (default `Approved`, migración lo asigna a todo lo existente) para que el filtrado del banco pueda excluir lo no aprobado. Pero además se crean dos entidades nuevas:
- `QuestionGenerationJob`: una solicitud de generación (CategoryId, Difficulty, Type, Topic, RequestedCount, Status: `Queued`/`Running`/`Completed`/`Failed`, CreatedAt, CreatedByUserId).
- `QuestionGenerationJobItem`: una fila por cada pregunta solicitada dentro del job (Status: `Pending`/`Succeeded`/`Failed`, `QuestionId` nullable — se llena solo si se creó la pregunta —, `ErrorMessage` nullable).

Alternativa descartada: guardar solo el estado en `Question` y no tener un job explícito. Se descarta porque no hay dónde representar un ítem que **falló antes de convertirse en `Question`** (p. ej. el modelo devolvió JSON inválido) — la bandeja de revisión necesita mostrar ese error igual que muestra las preguntas que sí se generaron, y `QuestionGenerationJobItem` es el lugar natural para eso.

**2. Background processing con `BackgroundService` + cola en memoria, no Hangfire.**
Un `IBackgroundTaskQueue` (envoltorio simple sobre `System.Threading.Channels.Channel<int>` con los IDs de job) y un `QuestionGenerationWorker : BackgroundService` que los procesa uno a la vez. `POST /api/question-generation/jobs` crea el job + sus `QuestionGenerationJobItem` en estado `Pending`, encola el ID y devuelve `202 Accepted` de inmediato.
Alternativa descartada: Hangfire (persistencia de jobs, dashboard, reintentos). Se descarta por ser una dependencia nueva pesada para una necesidad de una sola instancia sin requisitos de reintento sofisticado; si el proceso de la API se reinicia a mitad de un job, ese job queda en `Running` sin terminar — aceptable para esta primera iteración (se puede seguir generando preguntas manualmente mientras tanto), documentado como riesgo abajo.

**3. Generar una pregunta por llamada a Ollama, no todo el lote en una sola respuesta.**
El worker llama a Ollama una vez por cada `QuestionGenerationJobItem`, pidiendo formato JSON estructurado (parámetro `format` de la API de Ollama) con la forma exacta de `CreateQuestionDto`/`CreateAnswerDto` (enunciado, y para `MultipleChoice` 4 opciones con exactamente una `IsCorrect = true`, o para `OpenEnded` un `SampleAnswer`).
Alternativa descartada: pedir las N preguntas en una sola respuesta JSON (un array). Se descarta porque un solo error de formato invalidaría el lote completo y complica mucho la validación; con una llamada por pregunta, si una falla (JSON inválido, conteo de respuestas incorrecto, cero o más de una respuesta correcta), se marca solo ese `QuestionGenerationJobItem` como `Failed` con el motivo, y el resto del lote sigue su curso. Esto sí implica N llamadas secuenciales al modelo (más lento en total), aceptado dado que ya se decidió que la generación es en segundo plano y no bloqueante.

**4. Reutilizar el flujo de edición de preguntas existente para "editar" un ítem pendiente.**
Una pregunta generada con éxito es una fila real de `Question` con `QuestionReviewStatus = PendingReview`. "Editar" en la bandeja de revisión reutiliza `PUT /api/questions/{id}` (mismo DTO, mismo servicio) en lugar de crear un endpoint paralelo. "Aprobar" y "Rechazar" son las únicas operaciones nuevas: cambian `QuestionReviewStatus` a `Approved`/`Rejected`.
Alternativa descartada: un modelo de "borrador" completamente separado de `Question`, promovido a `Question` real solo al aprobar. Se descarta porque duplica el esquema (categoría, dificultad, respuestas, tipo) sin necesidad — el único requisito real es que no sea *seleccionable* para pruebas hasta aprobarse, lo cual ya resuelve el filtrado por `QuestionReviewStatus`.

**5. Ollama como servicio nuevo en `docker-compose.yml`, contactado por nombre de servicio.**
`api` llama a `http://ollama:11434` (no `localhost`), igual que ya llama a `sqlserver` por nombre de servicio en la cadena de conexión. Config nueva: `Ollama:BaseUrl` y `Ollama:Model` (`deepseek-r1:7b`) en `appsettings.json`, con override a `http://localhost:11434` en desarrollo local si el admin corre Ollama fuera de Docker (como ya lo probó).

**6. Filtrado del banco de preguntas excluye `PendingReview` y `Rejected` por defecto.**
El requisito existente de `question-bank` ("Filtrado de preguntas del banco") ya excluye inactivas (`IsActive = false`); se extiende para excluir también lo no aprobado. Esto aplica tanto al listado general (`GET /api/questions`) como a la selección para generación automática de pruebas — así una pregunta de IA nunca llega a un candidato sin pasar por revisión, sin tener que tocar el código de generación de pruebas en sí (que ya consume ese mismo endpoint/filtrado).

**7. Nuevos accesos como botones en la pantalla de preguntas, no un ítem nuevo en la barra lateral.**
Siguiendo el patrón ya usado en la pantalla de pruebas (botones "Manual" / "Generar automático" en la cabecera del listado), la pantalla de preguntas (`/admin/questions`) suma un botón "Generar con IA" (→ `/admin/questions/generate`) y un acceso a "Pendientes de revisión" (→ `/admin/questions/review`), sin agregar un quinto ítem a la barra lateral de 4 secciones.

**8. Esquema nuevo vía `scripts/create_database.sql`, no migraciones de EF Core.**
El proyecto no tiene ninguna migración de EF Core generada hoy (`EnsureCreatedAsync` crea el esquema completo en desarrollo, y `scripts/create_database.sql` es la alternativa documentada para entornos sin CLI de .NET — ver capacidad `deployment-ops`). Se sigue esa misma convención: la columna `QuestionReviewStatus` y las tablas `QuestionGenerationJobs`/`QuestionGenerationJobItems` se añaden al modelo de EF Core y a `scripts/create_database.sql` (respetando el orden de dependencias de claves foráneas ya establecido ahí), sin introducir el primer historial de migraciones del proyecto como parte de este cambio.

## Risks / Trade-offs

- [Ollama comparte CPU/RAM con `sqlserver`/`api` en la misma máquina; un lote grande puede degradar el rendimiento general mientras corre] → Mitigación: procesamiento secuencial de un job a la vez (no en paralelo), y es una operación de administrador de baja frecuencia, no algo que ocurra durante el uso normal de candidatos.
- [Si la API se reinicia mientras un job está `Running`, ese job queda huérfano sin terminar ni marcarse como `Failed`] → Mitigación: al iniciar, el `BackgroundService` marca como `Failed` cualquier job que haya quedado en `Running` de una ejecución anterior, para que no quede invisible en la bandeja.
- [El modelo puede alucinar una respuesta técnicamente incorrecta (ej. marcar la opción incorrecta como válida) sin que el formato JSON esté "roto"] → Mitigación: esto es exactamente lo que la revisión humana obligatoria está diseñada para atrapar; no se intenta validar la corrección semántica automáticamente.
- [Generar preguntas de calidad pareja para las 5 categorías existentes puede variar mucho según el tema pedido] → Mitigación: fuera de alcance de este cambio — es un problema de calidad del prompt/modelo, no de la funcionalidad; se puede iterar el prompt del sistema sin cambios de arquitectura.

## Migration Plan

- Migración de EF Core: nueva columna `QuestionReviewStatus` en `Questions` (default `Approved`, valor `0`/primero del enum para que todas las filas existentes queden aprobadas automáticamente), y nuevas tablas `QuestionGenerationJobs` y `QuestionGenerationJobItems`.
- Nuevo servicio `ollama` en `docker-compose.yml` con volumen persistente para los modelos descargados (evitar re-descargar `deepseek-r1:7b` en cada `docker-compose up`), y un paso manual documentado de `docker exec ollama ollama pull deepseek-r1:7b` la primera vez (no se automatiza la descarga del modelo en el arranque del contenedor, para no alargar el `docker-compose up` inicial).
- Sin pasos de rollback especiales más allá de revertir el commit y la migración (`dotnet ef database update <migración-anterior>`); el servicio `ollama` puede simplemente quitarse del compose si se decide abandonar la funcionalidad.

## Open Questions

- ¿Qué pasa con los `QuestionGenerationJob`/`Item` de preguntas ya rechazadas — se conservan indefinidamente para auditoría o se podan después de un tiempo? (No bloqueante para esta iteración; se puede decidir después.)
