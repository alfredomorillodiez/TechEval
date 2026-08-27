## MODIFIED Requirements

### Requirement: Solicitud de generación de preguntas por prompt
El sistema SHALL permitir a un administrador solicitar la generación de una cantidad determinada de preguntas para una categoría, dificultad y tipo dados, a partir de un tema en texto libre, creando un `QuestionGenerationJob` en estado `Queued` y respondiendo de inmediato sin esperar a que la generación termine. El sistema SHALL rechazar la solicitud si la categoría indicada no está habilitada para generación por IA (`Category.AllowsAiGeneration = false`).

#### Scenario: Solicitud válida encola un job
- **GIVEN** un administrador autenticado
- **WHEN** envía una solicitud de generación con `CategoryId` de una categoría activa existente y habilitada para generación por IA, `Difficulty`, `Type`, un `Topic` no vacío y una `Count` entre 1 y el máximo permitido
- **THEN** el sistema SHALL crear un `QuestionGenerationJob` en estado `Queued` con `Count` registros `QuestionGenerationJobItem` en estado `Pending`, encolar el job para procesamiento en segundo plano, y responder de inmediato con el identificador del job sin esperar a que la generación termine

#### Scenario: Validación de campos obligatorios
- **GIVEN** una solicitud de generación de preguntas
- **WHEN** falta el tema, el `CategoryId` no corresponde a ninguna categoría existente, o la cantidad solicitada es menor a 1 o mayor al máximo permitido
- **THEN** el sistema SHALL rechazar la operación sin crear ningún `QuestionGenerationJob`

#### Scenario: Rechazo por categoría no habilitada para generación por IA
- **GIVEN** una categoría existente y activa con `AllowsAiGeneration = false`
- **WHEN** un administrador envía una solicitud de generación indicando esa categoría, ya sea desde la interfaz o directamente contra `POST /api/question-generation/jobs`
- **THEN** el sistema SHALL rechazar la operación sin crear ningún `QuestionGenerationJob`, incluso si la solicitud es por lo demás válida

### Requirement: Aislamiento de errores por pregunta dentro de un lote
El sistema SHALL aislar el fallo de un `QuestionGenerationJobItem` individual (respuesta del modelo no interpretable como JSON, número de respuestas distinto al exigido para el tipo de pregunta, o ausencia de exactamente una respuesta marcada como correcta) sin afectar el procesamiento de los demás ítems del mismo job.

#### Scenario: Un ítem falla por respuesta del modelo mal formada
- **GIVEN** un `QuestionGenerationJobItem` en procesamiento cuya respuesta del modelo de IA no se puede interpretar como una pregunta válida para el tipo solicitado
- **WHEN** el sistema intenta parsear y validar esa respuesta
- **THEN** el sistema SHALL marcar ese ítem como `Failed` con un mensaje de error, sin crear ninguna `Question` para ese ítem, y SHALL continuar procesando los demás ítems del job

#### Scenario: Ítem exitoso crea una pregunta en revisión pendiente
- **GIVEN** un `QuestionGenerationJobItem` cuya respuesta del modelo de IA es válida para el tipo de pregunta solicitado (enunciado y, según el tipo, exactamente 4 respuestas con una marcada correcta, o una respuesta de referencia)
- **WHEN** el sistema la valida
- **THEN** el sistema SHALL crear una `Question` con `QuestionReviewStatus = PendingReview`, asociada a la categoría, dificultad y tipo del job, y marcar el ítem como `Succeeded` con el identificador de la pregunta resultante

## REMOVED Requirements

### Requirement: Crítica de calidad antes de aceptar una pregunta generada
**Reason**: Validado contra Ollama real (llamadas sueltas y un job completo a través de la aplicación): la etapa de crítica no demostró ser confiable — aprobó la única pregunta real que evaluó pese a que tenía exactamente los problemas que debía detectar (distractores casi idénticos, texto incoherente), mientras duplicaba el tiempo de generación por pregunta. Se reemplaza por un cambio de modelo (`qwen2.5-coder:14b`) que produjo mejor calidad estructural sin necesitar una segunda llamada de verificación.
**Migration**: Ninguna acción requerida. Las preguntas generadas siguen pasando por la validación estructural existente y por la revisión humana en `QuestionReview.razor`, que ya era y sigue siendo el filtro de calidad final.

## ADDED Requirements

### Requirement: Selector de categorías del generador excluye categorías no aptas
La interfaz de generación de preguntas por IA SHALL excluir del selector de categoría cualquier categoría con `AllowsAiGeneration = false`, sin afectar su disponibilidad en los flujos de creación o edición manual de preguntas.

#### Scenario: Categoría no apta no aparece en el selector de generación por IA
- **GIVEN** una categoría existente y activa con `AllowsAiGeneration = false`
- **WHEN** un administrador abre la pantalla de generación de preguntas por IA
- **THEN** el selector de categoría SHALL NOT incluir esa categoría entre las opciones

#### Scenario: Categoría no apta sigue disponible para creación manual
- **GIVEN** una categoría existente y activa con `AllowsAiGeneration = false`
- **WHEN** un administrador crea o edita una pregunta manualmente
- **THEN** el selector de categoría del formulario manual SHALL incluir esa categoría con normalidad
