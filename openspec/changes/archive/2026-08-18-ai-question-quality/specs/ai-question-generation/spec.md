## MODIFIED Requirements

### Requirement: Aislamiento de errores por pregunta dentro de un lote
El sistema SHALL aislar el fallo de un `QuestionGenerationJobItem` individual (respuesta del modelo no interpretable como JSON, número de respuestas distinto al exigido para el tipo de pregunta, ausencia de exactamente una respuesta marcada como correcta, o rechazo en la etapa de crítica de calidad) sin afectar el procesamiento de los demás ítems del mismo job.

#### Scenario: Un ítem falla por respuesta del modelo mal formada
- **GIVEN** un `QuestionGenerationJobItem` en procesamiento cuya respuesta del modelo de IA no se puede interpretar como una pregunta válida para el tipo solicitado
- **WHEN** el sistema intenta parsear y validar esa respuesta
- **THEN** el sistema SHALL marcar ese ítem como `Failed` con un mensaje de error, sin crear ninguna `Question` para ese ítem, y SHALL continuar procesando los demás ítems del job

#### Scenario: Un ítem falla por rechazo de la etapa de crítica
- **GIVEN** un `QuestionGenerationJobItem` cuya respuesta del modelo de IA es estructuralmente válida (formato correcto, cantidad de respuestas correcta) pero la etapa de crítica de calidad la rechaza por ambigüedad, distractores no distinguibles entre sí, o texto incoherente
- **WHEN** el sistema procesa el veredicto de la crítica
- **THEN** el sistema SHALL marcar ese ítem como `Failed` con el motivo concreto indicado por la crítica como mensaje de error, sin crear ninguna `Question` para ese ítem, y SHALL continuar procesando los demás ítems del job

#### Scenario: Ítem exitoso crea una pregunta en revisión pendiente
- **GIVEN** un `QuestionGenerationJobItem` cuya respuesta del modelo de IA es válida para el tipo de pregunta solicitado (enunciado y, según el tipo, exactamente 4 respuestas con una marcada correcta, o una respuesta de referencia) y que además fue aprobada por la etapa de crítica de calidad
- **WHEN** el sistema la valida
- **THEN** el sistema SHALL crear una `Question` con `QuestionReviewStatus = PendingReview`, asociada a la categoría, dificultad y tipo del job, y marcar el ítem como `Succeeded` con el identificador de la pregunta resultante

## ADDED Requirements

### Requirement: Crítica de calidad antes de aceptar una pregunta generada
Tras generar una pregunta candidata estructuralmente válida, el sistema SHALL someterla a una etapa adicional de crítica que evalúe ambigüedad (existencia de más de una respuesta defendible como correcta), distinguibilidad de los distractores entre sí, y coherencia del texto, aceptando la pregunta únicamente si la crítica no encuentra ninguno de estos problemas.

#### Scenario: La crítica aprueba una pregunta sin problemas detectados
- **GIVEN** una pregunta candidata generada, estructuralmente válida, sin ambigüedad ni distractores parecidos entre sí ni texto incoherente
- **WHEN** el sistema somete la pregunta a la etapa de crítica
- **THEN** la crítica SHALL emitir un veredicto de aprobación, permitiendo que el ítem continúe hacia `Succeeded` según el requirement de aislamiento de errores

#### Scenario: La crítica rechaza una pregunta con distractores parecidos entre sí
- **GIVEN** una pregunta candidata generada donde dos o más opciones de respuesta son difíciles de distinguir entre sí en significado
- **WHEN** el sistema somete la pregunta a la etapa de crítica
- **THEN** la crítica SHALL emitir un veredicto de rechazo con un motivo que identifique el problema de distinguibilidad de los distractores

#### Scenario: La crítica rechaza una pregunta ambigua
- **GIVEN** una pregunta candidata generada donde más de una opción de respuesta podría defenderse razonablemente como correcta
- **WHEN** el sistema somete la pregunta a la etapa de crítica
- **THEN** la crítica SHALL emitir un veredicto de rechazo con un motivo que identifique la ambigüedad detectada

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
