## MODIFIED Requirements

### Requirement: Pantalla de confirmación con resultado final
Tras un envío exitoso (manual o automático), la interfaz SHALL mostrar una pantalla final sin permitir volver a las preguntas de la prueba. Cuando el resultado queda `Reviewed`, la pantalla SHALL mostrar el resultado obtenido y un aviso de que el detalle llegará también por email. Cuando el resultado queda `PendingReview`, la pantalla SHALL indicar que la prueba se ha recibido correctamente y que contiene preguntas que requieren corrección manual, informando de que recibirá el resultado por email cuando esté corregida, y NO SHALL mostrar puntuación, porcentaje, veredicto ni las respuestas correctas.

#### Scenario: Prueba sin abiertas pendientes muestra el resultado
- **WHEN** el envío de la prueba se completa correctamente y el resultado queda con estado `Reviewed`
- **THEN** la interfaz muestra si el candidato aprobó o no, el porcentaje de puntuación, los puntos obtenidos sobre el total, y un mensaje indicando que recibirá los resultados por email

#### Scenario: Prueba pendiente de corrección muestra únicamente el acuse
- **WHEN** el envío de la prueba se completa correctamente y el resultado queda con estado `PendingReview`
- **THEN** la interfaz muestra un mensaje de prueba recibida y pendiente de corrección manual, sin porcentaje, sin puntos, sin veredicto de aprobado o suspenso y sin ningún indicador visual de éxito o fracaso

#### Scenario: La respuesta de envío no revela el solucionario
- **WHEN** el candidato envía la prueba y el sistema responde con la confirmación
- **THEN** la respuesta del endpoint de envío NO SHALL incluir el texto de las respuestas correctas ni la evaluación de acierto por pregunta, con independencia del estado del resultado
