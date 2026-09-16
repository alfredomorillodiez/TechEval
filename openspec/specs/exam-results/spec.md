# exam-results Specification

## Purpose
TBD - created by archiving change exam-results. Update Purpose after archive.

## Requirements

### Requirement: Corrección automática de preguntas de opción múltiple
Al enviar un examen (`SubmitExamAsync`), el sistema SHALL evaluar automáticamente cada pregunta de tipo `MultipleChoice` comparando el `SelectedAnswerId` enviado por el candidato contra la respuesta marcada como `IsCorrect` en el banco de preguntas, marcando la respuesta del candidato (`UserAnswer.IsCorrect`) como verdadera o falsa y sumando los puntos de la pregunta (`Question.Points`) al total obtenido cuando coincide con la respuesta correcta.

#### Scenario: Respuesta de opción múltiple correcta
- **WHEN** el candidato selecciona en una pregunta de tipo `MultipleChoice` la respuesta marcada como `IsCorrect = true` en el banco de preguntas
- **THEN** el sistema marca `UserAnswer.IsCorrect = true` y suma los puntos de esa pregunta a `ObtainedPoints` del resultado

#### Scenario: Respuesta de opción múltiple incorrecta o sin responder
- **WHEN** el candidato selecciona una respuesta distinta de la correcta, o no selecciona ninguna respuesta para una pregunta de tipo `MultipleChoice`
- **THEN** el sistema marca `UserAnswer.IsCorrect = false` y no suma puntos de esa pregunta a `ObtainedPoints`

### Requirement: Preguntas abiertas pendientes de corrección manual
El sistema SHALL dejar las respuestas de preguntas de tipo `OpenQuestion` con contenido real sin evaluación automática, guardando `UserAnswer.IsCorrect = null`, `UserAnswer.AwardedPoints = null` y el texto libre en `OpenAnswer`, de forma que queden identificables como pendientes de revisión humana y no se contabilicen en `ObtainedPoints` hasta que un administrador las corrija. El sistema SHALL ofrecer un flujo de corrección manual que permita otorgarles puntos; una pregunta abierta nunca SHALL quedar permanentemente sin posibilidad de puntuar.

#### Scenario: Envío de una pregunta abierta
- **WHEN** el candidato responde una pregunta de tipo `OpenQuestion` con texto libre
- **THEN** el sistema guarda la respuesta en `UserAnswer.OpenAnswer` y establece `UserAnswer.IsCorrect = null` y `UserAnswer.AwardedPoints = null`, sin sumar ni descontar puntos automáticamente por esa pregunta

#### Scenario: La respuesta abierta puede puntuarse posteriormente
- **GIVEN** una respuesta abierta guardada con `AwardedPoints = null`
- **WHEN** un administrador la corrige a través del flujo de corrección manual
- **THEN** el sistema persiste los puntos otorgados y los incorpora al `ObtainedPoints` del resultado

### Requirement: Cálculo de puntuación y determinación de aprobado
Al completar la corrección de todas las preguntas —automática para las de test, manual para las abiertas con contenido—, el sistema SHALL calcular `ScorePercentage` como el porcentaje de `ObtainedPoints` sobre `TotalPoints` (redondeado a 2 decimales, `0` si `TotalPoints` es `0`), y SHALL determinar `Passed` comparando `ScorePercentage` contra el umbral `PassingScorePercentage` configurado en el examen (`Passed = true` cuando `ScorePercentage >= PassingScorePercentage`). Mientras queden respuestas abiertas sin corregir, el sistema SHALL mantener `Passed = null` y NO SHALL presentar la puntuación parcial como veredicto.

#### Scenario: Puntuación igual o superior al umbral de aprobación
- **WHEN** el `ScorePercentage` calculado sobre un resultado sin correcciones pendientes es mayor o igual que el `PassingScorePercentage` del examen
- **THEN** el resultado se guarda con `Passed = true`

#### Scenario: Puntuación inferior al umbral de aprobación
- **WHEN** el `ScorePercentage` calculado sobre un resultado sin correcciones pendientes es menor que el `PassingScorePercentage` del examen
- **THEN** el resultado se guarda con `Passed = false`

#### Scenario: Resultado con correcciones pendientes
- **WHEN** el resultado contiene al menos una respuesta abierta con `AwardedPoints = null`
- **THEN** el sistema guarda `Passed = null` y no determina aprobado ni suspenso hasta que finalice la corrección manual

### Requirement: Persistencia del resultado y notificación al candidato
El sistema SHALL crear y persistir una entidad `ExamResult` (vinculada a `ExamSessionId`, `ExamId` y al `UserId` del alumno propietario de la sesión, con `CandidateName`, `CandidateEmail`, `TotalPoints`, `ObtainedPoints`, `ScorePercentage`, `Passed`, `Status` y `CompletedAt`) al enviar el examen, y SHALL notificar al candidato por correo de forma acorde al estado: un acuse de recibo sin cifras cuando el resultado queda `PendingReview`, y el correo de resultado con porcentaje y veredicto cuando queda `Reviewed`.

#### Scenario: Creación del ExamResult tras enviar el examen
- **WHEN** `SubmitExamAsync` termina de procesar todas las respuestas de la sesión
- **THEN** el sistema persiste un nuevo `ExamResult` con los puntos totales, los puntos obtenidos hasta el momento, el porcentaje, el estado de corrección y el veredicto cuando proceda, vinculado al `UserId` del alumno dueño de la sesión, y marca la `ExamSession` asociada como `Completed`

#### Scenario: Notificación de resultado por email en examen sin abiertas pendientes
- **WHEN** el `ExamResult` ha sido persistido con `Status = Reviewed`
- **THEN** el sistema envía al correo del candidato (`CandidateEmail`) un mensaje con el nombre del examen, el porcentaje obtenido y si aprobó o no

#### Scenario: Acuse de recibo en examen pendiente de corrección
- **WHEN** el `ExamResult` ha sido persistido con `Status = PendingReview`
- **THEN** el sistema envía al correo del candidato un acuse indicando que la prueba se recibió correctamente y que contiene preguntas que requieren corrección manual, **sin** incluir puntuación, porcentaje ni veredicto, y NO SHALL enviar el correo de resultado

### Requirement: Dashboard de estadísticas para administradores
El sistema SHALL exponer a usuarios con rol `Admin` un endpoint de dashboard (`GET /api/results/dashboard`) que devuelva el número de exámenes activos, el número de preguntas activas, la cantidad de resultados registrados en el mes en curso, el promedio de `ScorePercentage` del mes en curso, la tasa de aprobación del mes en curso, el número total de resultados pendientes de corrección, y los últimos 10 resultados registrados (ordenados por fecha de finalización descendente). El promedio de puntuación y la tasa de aprobación SHALL calcularse únicamente sobre resultados con `Status = Reviewed`, de forma que las puntuaciones parciales de los resultados pendientes no distorsionen las métricas.

#### Scenario: Consulta del dashboard con resultados en el mes actual
- **WHEN** un administrador solicita `GET /api/results/dashboard` y existen resultados completados dentro del mes calendario en curso
- **THEN** el sistema responde con el promedio de puntuación y la tasa de aprobación calculados únicamente sobre los resultados `Reviewed` de ese mes, junto con los conteos de exámenes/preguntas activos, el número de pendientes de corrección y los 10 resultados más recientes

#### Scenario: Consulta del dashboard sin resultados en el mes actual
- **WHEN** un administrador solicita `GET /api/results/dashboard` y no existe ningún resultado completado dentro del mes en curso
- **THEN** el sistema responde con promedio de puntuación y tasa de aprobación en `0` para el mes, sin fallar la consulta

#### Scenario: Los resultados pendientes no distorsionan las métricas
- **GIVEN** un mes con dos resultados `Reviewed` al 80 % y un resultado `PendingReview` con una puntuación parcial del 20 %
- **WHEN** un administrador solicita el dashboard
- **THEN** el promedio del mes es 80 % y la tasa de aprobación se calcula sobre los dos resultados corregidos, y el resultado pendiente se refleja únicamente en el contador de pendientes de corrección

### Requirement: Listado de resultados y filtrado por examen
El sistema SHALL permitir a usuarios con rol `Admin` consultar el listado completo de resultados (`GET /api/results`) y el listado de resultados de un examen específico (`GET /api/results/exam/{examId}`), ambos ordenados por fecha de finalización descendente, exponiendo para cada resultado el nombre e email del candidato, el título del examen, el porcentaje obtenido, si aprobó (`null` cuando está pendiente de corrección), el estado de corrección y la fecha de finalización.

#### Scenario: Listado global de resultados
- **WHEN** un administrador solicita `GET /api/results`
- **THEN** el sistema responde con todos los `ExamResult` registrados, ordenados del más reciente al más antiguo, cada uno con su estado de corrección

#### Scenario: Listado de resultados de un examen concreto
- **WHEN** un administrador solicita `GET /api/results/exam/{examId}` para un examen existente
- **THEN** el sistema responde únicamente con los resultados cuyo `ExamId` coincide con el examen solicitado, ordenados del más reciente al más antiguo

#### Scenario: Resultado pendiente en el listado
- **WHEN** el listado incluye un resultado con `Status = PendingReview`
- **THEN** ese resultado se expone con `Passed = null` y su estado de corrección, de modo que el consumidor pueda distinguirlo de un suspenso

### Requirement: Detalle de resultado con revisión de respuestas
El sistema SHALL permitir a usuarios con rol `Admin` consultar el detalle de un resultado (`GET /api/results/{id}`), incluyendo el estado de corrección y, por cada pregunta de la sesión, el texto de la pregunta, la respuesta seleccionada o el texto abierto del candidato, la respuesta correcta esperada, si fue evaluada como correcta (o `null` si es una pregunta abierta pendiente), los puntos de la pregunta, los puntos otorgados (`AwardedPoints`, `null` si está pendiente) y el comentario del corrector cuando exista; si el resultado no existe, SHALL responder `404`. El enunciado, la respuesta seleccionada, la correcta y los puntos de la pregunta SHALL leerse de la copia congelada en la `UserAnswer` y no de la pregunta actual del banco.

#### Scenario: Consulta de detalle de un resultado existente
- **WHEN** un administrador solicita `GET /api/results/{id}` para un `id` de resultado existente
- **THEN** el sistema responde con los datos del resultado, su estado de corrección y la lista de revisión de respuestas, una entrada por cada pregunta única respondida en la sesión

#### Scenario: Detalle de un resultado ya corregido muestra la puntuación otorgada
- **WHEN** un administrador consulta el detalle de un resultado con `Status = Reviewed` que incluía preguntas abiertas
- **THEN** cada respuesta abierta se expone con los `AwardedPoints` otorgados por el corrector y su comentario, cuando exista

#### Scenario: Consulta de detalle de un resultado inexistente
- **WHEN** un administrador solicita `GET /api/results/{id}` para un `id` que no corresponde a ningún resultado
- **THEN** el sistema responde `404 Not Found`

#### Scenario: El detalle no cambia al editar la pregunta
- **GIVEN** un resultado cuyo detalle muestra el enunciado y las opciones de una pregunta
- **WHEN** un administrador edita esa pregunta en el banco y se vuelve a consultar el detalle
- **THEN** el sistema SHALL devolver el mismo enunciado, la misma opción elegida, la misma opción correcta y los mismos puntos máximos que antes de la edición

### Requirement: Estado de corrección del resultado
El sistema SHALL registrar en cada `ExamResult` un estado `Status` de tipo `ExamResultStatus` con los valores `PendingReview` y `Reviewed`. Al enviar un examen, el sistema SHALL fijar `Status = PendingReview` si y solo si el examen contiene al menos una pregunta de tipo `OpenQuestion`, con independencia de lo que el candidato haya respondido; en cualquier otro caso SHALL fijar `Status = Reviewed`. Mientras `Status` sea `PendingReview`, `ScorePercentage` y `ObtainedPoints` reflejan únicamente la parte ya corregida automáticamente y `Passed` SHALL ser `null`.

#### Scenario: Examen sin preguntas abiertas
- **WHEN** un candidato envía un examen compuesto únicamente por preguntas de tipo `MultipleChoice`
- **THEN** el sistema fija `Status = Reviewed` y determina `Passed` con la puntuación calculada, sin requerir intervención humana

#### Scenario: Examen con al menos una abierta respondida
- **WHEN** un candidato envía un examen que incluye una pregunta abierta con texto no vacío
- **THEN** el sistema fija `Status = PendingReview`, `Passed = null`, y el resultado queda a la espera de corrección manual

#### Scenario: Examen con abiertas todas en blanco
- **GIVEN** un examen que incluye dos preguntas abiertas
- **WHEN** el candidato envía el examen dejando ambas respuestas vacías o compuestas solo por espacios en blanco
- **THEN** el sistema pre-puntúa ambas a `AwardedPoints = 0` y fija igualmente `Status = PendingReview` con `Passed = null`, de modo que el resultado entre en la cola y un administrador confirme la puntuación antes de cerrarlo

#### Scenario: La composición del examen determina el estado, no las respuestas
- **GIVEN** dos candidatos que realizan el mismo examen con una pregunta abierta, uno respondiéndola y otro dejándola vacía
- **WHEN** ambos envían la prueba
- **THEN** los dos resultados quedan con `Status = PendingReview`, de forma que el estado sea predecible desde el momento en que se compone el examen

### Requirement: Puntuación parcial y comentario del corrector por respuesta
El sistema SHALL almacenar en cada `UserAnswer` los puntos efectivamente otorgados (`AwardedPoints`, entero opcional) y un comentario opcional del corrector (`ReviewerComment`). Para preguntas de tipo `MultipleChoice`, el sistema SHALL fijar `AwardedPoints` automáticamente en el envío: los puntos completos de la pregunta si la respuesta es correcta, `0` en caso contrario. Para preguntas abiertas, `AwardedPoints` SHALL permanecer `null` hasta que un administrador corrija la respuesta, salvo cuando la respuesta se entrega en blanco, en cuyo caso SHALL pre-fijarse a `0` en el propio envío como valor por defecto que el corrector confirmará.

El sistema SHALL calcular `ObtainedPoints` como la suma de los `AwardedPoints` de las respuestas de la sesión, de modo que la puntuación obtenida quede congelada en el momento del envío para las preguntas de test y no varíe si posteriormente se edita `Question.Points`.

#### Scenario: Puntuación automática de una respuesta de test correcta
- **WHEN** el candidato acierta una pregunta de tipo `MultipleChoice` de 2 puntos
- **THEN** el sistema fija `IsCorrect = true` y `AwardedPoints = 2` en la `UserAnswer` correspondiente

#### Scenario: Respuesta abierta con contenido queda sin puntuar
- **WHEN** el candidato entrega texto libre en una pregunta abierta
- **THEN** el sistema guarda el texto en `OpenAnswer` y deja `IsCorrect = null` y `AwardedPoints = null`, marcando la respuesta como pendiente de corrección

#### Scenario: Respuesta abierta en blanco se pre-puntúa a cero
- **WHEN** el candidato deja una pregunta abierta vacía o con solo espacios en blanco
- **THEN** el sistema fija `AwardedPoints = 0` para esa respuesta en el momento del envío, sin que ello exima al resultado de pasar por la cola de corrección

#### Scenario: La edición posterior de una pregunta no altera un resultado ya emitido
- **GIVEN** un resultado en el que el candidato acertó una pregunta de test que valía 1 punto, con `AwardedPoints = 1` registrado
- **WHEN** un administrador edita esa pregunta y le asigna 5 puntos, y después se recalcula el resultado al cerrar su corrección manual
- **THEN** el sistema SHALL seguir contabilizando 1 punto por esa respuesta, manteniendo la coherencia con el `TotalPoints` congelado en el envío
