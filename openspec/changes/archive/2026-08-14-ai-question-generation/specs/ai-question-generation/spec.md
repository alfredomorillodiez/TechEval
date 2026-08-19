## ADDED Requirements

### Requirement: Solicitud de generación de preguntas por prompt
El sistema SHALL permitir a un administrador solicitar la generación de una cantidad determinada de preguntas para una categoría, dificultad y tipo dados, a partir de un tema en texto libre, creando un `QuestionGenerationJob` en estado `Queued` y respondiendo de inmediato sin esperar a que la generación termine.

#### Scenario: Solicitud válida encola un job
- **GIVEN** un administrador autenticado
- **WHEN** envía una solicitud de generación con `CategoryId` de una categoría activa existente, `Difficulty`, `Type`, un `Topic` no vacío y una `Count` entre 1 y el máximo permitido
- **THEN** el sistema SHALL crear un `QuestionGenerationJob` en estado `Queued` con `Count` registros `QuestionGenerationJobItem` en estado `Pending`, encolar el job para procesamiento en segundo plano, y responder de inmediato con el identificador del job sin esperar a que la generación termine

#### Scenario: Validación de campos obligatorios
- **GIVEN** una solicitud de generación de preguntas
- **WHEN** falta el tema, el `CategoryId` no corresponde a ninguna categoría existente, o la cantidad solicitada es menor a 1 o mayor al máximo permitido
- **THEN** el sistema SHALL rechazar la operación sin crear ningún `QuestionGenerationJob`

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
El sistema SHALL exponer un listado de `QuestionGenerationJobItem` pendientes de revisión, mostrando el contenido de la pregunta generada cuando el ítem fue exitoso o el mensaje de error cuando falló, y permitiendo identificar de qué job proviene cada uno.

#### Scenario: Listado incluye éxitos y fallos del mismo lote
- **GIVEN** un `QuestionGenerationJob` con algunos `QuestionGenerationJobItem` en `Succeeded` y otros en `Failed`
- **WHEN** un administrador consulta la bandeja de revisión
- **THEN** el sistema SHALL devolver todos los ítems del job que aún no fueron aprobados ni rechazados, mostrando el contenido de la pregunta para los exitosos y el mensaje de error para los fallidos

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
