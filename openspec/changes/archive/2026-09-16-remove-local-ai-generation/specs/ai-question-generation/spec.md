## REMOVED Requirements

### Requirement: Solicitud de generación de preguntas por prompt
**Reason**: Se elimina la dependencia de un modelo de IA local (Ollama); el banco de preguntas se mantiene exclusivamente por alta manual.
**Migration**: Usar el alta manual de preguntas en `/admin/questions/new`. No hay equivalente automático.

### Requirement: Procesamiento secuencial de un job en segundo plano
**Reason**: Sin generación por IA no existen `QuestionGenerationJob` que procesar; se elimina el servicio en segundo plano `QuestionGenerationWorker`.
**Migration**: N/A.

### Requirement: Aislamiento de errores por pregunta dentro de un lote
**Reason**: Este requisito solo aplicaba al procesamiento de lotes generados por IA, que se elimina.
**Migration**: N/A.

### Requirement: Bandeja de revisión de preguntas generadas
**Reason**: Sin generación por IA no hay preguntas pendientes de revisión que listar; se elimina la pantalla `/admin/questions/review`.
**Migration**: N/A.

### Requirement: Aprobación de una pregunta generada
**Reason**: Toda pregunta se crea manualmente y nace aprobada (ver capacidad `question-bank`); no existe un estado intermedio que aprobar.
**Migration**: N/A.

### Requirement: Rechazo de una pregunta generada
**Reason**: No existen preguntas generadas por IA que rechazar.
**Migration**: Usar la baja lógica (`DELETE /api/questions/{id}`) para retirar una pregunta manual que no se quiera usar.

### Requirement: Edición de una pregunta pendiente antes de aprobar
**Reason**: No existe el estado `PendingReview`; toda pregunta manual es editable de inmediato mediante el flujo de edición ya cubierto por `question-bank`.
**Migration**: Usar `/admin/questions/{id}` para editar cualquier pregunta existente, sin distinción de estado de revisión.

### Requirement: Recuperación de jobs interrumpidos al reiniciar
**Reason**: No existen `QuestionGenerationJob` que recuperar tras un reinicio de la API.
**Migration**: N/A.

### Requirement: Indicador de progreso de generación en curso
**Reason**: No existe ningún proceso de generación en curso que reportar.
**Migration**: N/A.

### Requirement: Razonamiento del modelo antes de responder en formato estructurado
**Reason**: No existe ningún modelo de IA configurado que emita respuestas a parsear.
**Migration**: N/A.

### Requirement: Selector de categorías del generador excluye categorías no aptas
**Reason**: Se elimina la pantalla de generación por IA y el flag `Category.AllowsAiGeneration` que la sustentaba (ver capacidad `question-bank` para el tratamiento de categorías).
**Migration**: N/A.
