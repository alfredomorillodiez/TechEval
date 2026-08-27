# ai-question-generation Specification

## Purpose
TBD - created by archiving change ai-question-generation. Update Purpose after archive.
## Requirements
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

### Requirement: Procesamiento secuencial de un job en segundo plano
El sistema SHALL procesar los `QuestionGenerationJob` uno a la vez mediante un servicio en segundo plano, generando cada `QuestionGenerationJobItem` del job de forma secuencial mediante el modelo de IA configurado.

#### Scenario: Procesamiento de un job encolado
- **GIVEN** un `QuestionGenerationJob` en estado `Queued` con varios `QuestionGenerationJobItem` en estado `Pending`
- **WHEN** el servicio en segundo plano lo toma para procesar
- **THEN** el sistema SHALL marcar el job como `Running` y generar cada ítem uno por uno, sin procesar dos jobs en paralelo al mismo tiempo

#### Scenario: Finalización del job
- **GIVEN** un `QuestionGenerationJob` en estado `Running` cuyos ítems ya terminaron de procesarse (todos en `Succeeded` o `Failed`)
- **WHEN** se resuelve el último ítem pendiente
- **THEN** el sistema SHALL marcar el job como `Completed`, incluso si alguno de sus ítems individuales terminó en `Failed`

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

### Requirement: Bandeja de revisión de preguntas generadas
El sistema SHALL exponer un listado de `QuestionGenerationJobItem` pendientes de revisión, mostrando el contenido de la pregunta generada cuando el ítem fue exitoso o el mensaje de error cuando falló, y permitiendo identificar de qué job proviene cada uno mediante el tema (`Topic`) y el identificador del job mostrados junto a cada ítem en la interfaz.

#### Scenario: Listado incluye éxitos y fallos del mismo lote
- **GIVEN** un `QuestionGenerationJob` con algunos `QuestionGenerationJobItem` en `Succeeded` y otros en `Failed`
- **WHEN** un administrador consulta la bandeja de revisión
- **THEN** el sistema SHALL devolver todos los ítems del job que aún no fueron aprobados ni rechazados, mostrando el contenido de la pregunta para los exitosos y el mensaje de error para los fallidos

#### Scenario: Cada ítem muestra su job de origen
- **GIVEN** una bandeja de revisión con ítems provenientes de más de un `QuestionGenerationJob`
- **WHEN** un administrador visualiza la lista
- **THEN** el sistema SHALL mostrar, junto a cada ítem, el tema (`Topic`) y el identificador del job del que proviene, permitiendo distinguir a qué lote pertenece

### Requirement: Aprobación de una pregunta generada
El sistema SHALL permitir a un administrador aprobar una pregunta generada, cambiando su `QuestionReviewStatus` a `Approved`, lo que la vuelve elegible para selección en pruebas en igualdad de condiciones que una pregunta creada manualmente.

#### Scenario: Aprobación exitosa
- **GIVEN** una `Question` con `QuestionReviewStatus = PendingReview`
- **WHEN** un administrador la aprueba
- **THEN** el sistema SHALL cambiar su `QuestionReviewStatus` a `Approved`, quedando elegible para los mismos criterios de selección que una pregunta creada manualmente

### Requirement: Rechazo de una pregunta generada
El sistema SHALL permitir a un administrador rechazar una pregunta generada, cambiando su `QuestionReviewStatus` a `Rejected`, excluyéndola permanentemente de cualquier selección para pruebas.

#### Scenario: Rechazo exitoso
- **GIVEN** una `Question` con `QuestionReviewStatus = PendingReview`
- **WHEN** un administrador la rechaza
- **THEN** el sistema SHALL cambiar su `QuestionReviewStatus` a `Rejected`, y esa pregunta SHALL dejar de aparecer en la bandeja de revisión y en cualquier listado de selección para pruebas, preservando el registro para trazabilidad

### Requirement: Edición de una pregunta pendiente antes de aprobar
El sistema SHALL permitir editar el contenido de una `Question` con `QuestionReviewStatus = PendingReview` mediante el mismo mecanismo de edición usado para una pregunta creada manualmente, sin exigir que esté aprobada previamente.

#### Scenario: Edición de una pregunta generada antes de aprobarla
- **GIVEN** una `Question` con `QuestionReviewStatus = PendingReview`
- **WHEN** un administrador modifica su enunciado o respuestas y guarda los cambios
- **THEN** el sistema SHALL persistir los cambios sin alterar su `QuestionReviewStatus`, permitiendo aprobarla o rechazarla después de la edición

### Requirement: Recuperación de jobs interrumpidos al reiniciar
El sistema SHALL marcar como `Failed`, al arrancar, cualquier `QuestionGenerationJob` que haya quedado en estado `Running` de una ejecución anterior de la API, junto con sus `QuestionGenerationJobItem` que sigan en `Pending`.

#### Scenario: Reinicio de la API con un job a medio procesar
- **GIVEN** un `QuestionGenerationJob` en estado `Running` con `QuestionGenerationJobItem` pendientes, persistidos antes de un reinicio de la API
- **WHEN** la API vuelve a arrancar
- **THEN** el sistema SHALL marcar ese job como `Failed` y sus ítems pendientes como `Failed` con un mensaje indicando la interrupción, en lugar de dejarlos indefinidamente en estado `Running`/`Pending`

### Requirement: Indicador de progreso de generación en curso
El sistema SHALL exponer el progreso agregado de generación por `QuestionGenerationJob` — incluyendo los `QuestionGenerationJobItem` aún en estado `Pending`, que no forman parte del listado de la bandeja de revisión — y SHALL mostrar este progreso de forma siempre visible en la ventana de revisión de preguntas, actualizándose automáticamente mientras la ventana permanece abierta.

#### Scenario: Job en curso con ítems aún generándose
- **GIVEN** un `QuestionGenerationJob` en estado `Running` con `RequestedCount = 5`, de los cuales 2 ítems están `Succeeded`, 1 `Failed` y 2 siguen `Pending`
- **WHEN** un administrador consulta el progreso de generación desde la ventana de revisión
- **THEN** el sistema SHALL indicar que el job sigue en curso, cuántas preguntas del lote ya están listas para revisar, cuántas fallaron y cuántas quedan por generar

#### Scenario: Actualización automática sin recargar la página
- **GIVEN** un administrador con la ventana de revisión abierta mientras un job está `Running`
- **WHEN** el servicio en segundo plano completa la generación de un nuevo ítem del job
- **THEN** el sistema SHALL reflejar el progreso actualizado en la ventana de revisión en un plazo de pocos segundos, sin que el administrador tenga que recargar la página manualmente

#### Scenario: Sin jobs pendientes de atención
- **GIVEN** que no existe ningún `QuestionGenerationJob` con ítems `Pending`, o `Succeeded` con `Question.QuestionReviewStatus = PendingReview`, o `Failed`
- **WHEN** un administrador consulta el progreso de generación
- **THEN** el sistema SHALL indicar que no hay generación en curso ni preguntas pendientes de revisión, sin listar jobs cuyos ítems ya fueron todos aprobados o rechazados

### Requirement: Razonamiento del modelo antes de responder en formato estructurado
El sistema SHALL permitir que el modelo de generación configurado emita su razonamiento (por ejemplo, un bloque de pensamiento delimitado) antes de comprometerse a la respuesta estructurada final, y SHALL extraer el contenido estructurado de la parte de la respuesta posterior a ese razonamiento cuando esté presente, sin fallar el parseo cuando el modelo configurado no emita razonamiento explícito.

#### Scenario: El modelo emite un bloque de razonamiento antes del JSON
- **GIVEN** una respuesta del modelo que incluye un bloque de razonamiento seguido del contenido estructurado esperado
- **WHEN** el sistema parsea la respuesta
- **THEN** el sistema SHALL extraer el contenido estructurado de la parte posterior al bloque de razonamiento, ignorando el razonamiento para efectos de validación

#### Scenario: El modelo no emite bloque de razonamiento
- **GIVEN** una respuesta del modelo que no incluye ningún bloque de razonamiento, solo el contenido estructurado
- **WHEN** el sistema parsea la respuesta
- **THEN** el sistema SHALL procesar el contenido estructurado normalmente, sin requerir la presencia de un bloque de razonamiento

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
