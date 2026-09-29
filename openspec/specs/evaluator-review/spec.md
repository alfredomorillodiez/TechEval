# evaluator-review Specification

## Purpose
Corrección de las preguntas abiertas por parte del evaluador: la cola de las pruebas que tiene asignadas, la corrección a ciegas sin la identidad del candidato, las señales de integridad sin la hora del reloj y el historial de sus propias correcciones.

## Requirements

### Requirement: Cola de correcciones del evaluador
El sistema SHALL exponer a usuarios con rol `Evaluador` la cola de los `ExamResult` con `Status = PendingReview` que pertenecen a pruebas que tiene asignadas, ordenados por `CompletedAt` ascendente. La cola SHALL excluir los resultados en los que el propio evaluador es el candidato, por `UserId` o por `CandidateEmail`. Cada entrada SHALL incluir el identificador del resultado, el título de la prueba, el alias del candidato, la fecha de envío sin la hora, los días de espera, el número de respuestas abiertas y si otra persona tiene el resultado reservado, con el fin de la reserva. La entrada SHALL NOT incluir el nombre ni el email del candidato, ni quién tiene la reserva.

#### Scenario: Cola con resultados de pruebas asignadas y sin asignar
- **GIVEN** un evaluador asignado a la prueba A y no a la prueba B, y un resultado pendiente en cada una
- **WHEN** el evaluador solicita su cola
- **THEN** el sistema responde `200 OK` solo con el resultado de la prueba A

#### Scenario: La cola no lleva la identidad del candidato
- **WHEN** el evaluador solicita su cola
- **THEN** ninguna entrada de la respuesta contiene el nombre ni el email del candidato, en ningún campo

#### Scenario: El evaluador no ve sus propios resultados
- **GIVEN** un evaluador que antes fue alumno y tiene un resultado pendiente en una prueba que ahora tiene asignada
- **WHEN** solicita su cola
- **THEN** ese resultado no aparece

#### Scenario: Resultado reservado por otra persona
- **GIVEN** un resultado de una prueba asignada que otro corrector reservó hace cinco minutos
- **WHEN** el evaluador solicita su cola
- **THEN** la entrada indica que está reservado y hasta cuándo, sin decir quién lo reservó

#### Scenario: Evaluador sin pruebas asignadas
- **WHEN** un evaluador sin asignaciones solicita su cola
- **THEN** el sistema responde `200 OK` con una lista vacía

### Requirement: Alias estable del candidato
En todo lo que recibe el evaluador, el candidato SHALL aparecer con un alias derivado del identificador del resultado, con la forma `Candidato R-<id>`. El alias SHALL ser el mismo en la cola, en el detalle, en la respuesta del envío y en el historial. Dos resultados del mismo candidato SHALL tener alias distintos, para que el evaluador no pueda relacionarlos.

#### Scenario: Mismo alias en la cola y en el detalle
- **GIVEN** el resultado 42 en la cola del evaluador
- **WHEN** el evaluador abre su detalle
- **THEN** la cola y el detalle muestran el alias `Candidato R-42`

#### Scenario: Dos resultados del mismo candidato
- **GIVEN** un candidato con resultados pendientes en dos pruebas asignadas al mismo evaluador
- **WHEN** el evaluador solicita su cola
- **THEN** las dos entradas tienen alias distintos

### Requirement: Detalle de corrección a ciegas
El sistema SHALL exponer al evaluador el detalle de corrección de un resultado pendiente de una prueba asignada, con el mismo contenido de corrección que recibe el administrador (enunciado y puntos congelados, respuesta del candidato, puntuación precargada y respuesta de referencia), pero con el alias en lugar del nombre y del email, y con la fecha de envío sin la hora. Abrir el detalle SHALL reservar el resultado para el evaluador, según el requisito de reserva.

Si el resultado no existe, pertenece a una prueba no asignada o es un resultado propio del evaluador, el sistema SHALL responder `404 Not Found`, sin distinguir los tres casos. Si el resultado ya está corregido, SHALL responder `409 Conflict`. Si otra persona lo tiene reservado, SHALL responder `409 Conflict` con el fin de la reserva.

#### Scenario: Detalle de un resultado asignado
- **GIVEN** un resultado pendiente de una prueba asignada al evaluador
- **WHEN** el evaluador solicita su detalle de corrección
- **THEN** el sistema responde `200 OK` con las respuestas abiertas, el alias y la fecha sin hora, y sin el nombre ni el email del candidato

#### Scenario: Resultado de una prueba no asignada
- **GIVEN** un resultado pendiente de una prueba que el evaluador no tiene asignada
- **WHEN** el evaluador solicita su detalle
- **THEN** el sistema responde `404 Not Found`, igual que para un identificador inexistente

#### Scenario: Asignación retirada entretanto
- **GIVEN** un evaluador con el detalle de un resultado abierto
- **WHEN** el administrador le quita la asignación de la prueba y el evaluador vuelve a pedir el detalle
- **THEN** el sistema responde `404 Not Found`

### Requirement: Envío de la corrección por el evaluador
El sistema SHALL aceptar del evaluador la corrección de un resultado pendiente de una prueba asignada, con las mismas reglas que la corrección del administrador: todas las respuestas abiertas en una sola operación, puntos dentro del rango, escritura en una sola transacción, recálculo, cierre y correo al candidato. El sistema SHALL registrar al evaluador en `ReviewedByUserId`. La respuesta SHALL contener el alias, la puntuación final y el veredicto, y SHALL NOT contener el nombre ni el email del candidato.

El sistema SHALL comprobar la asignación y la reserva en el propio envío, no solo al abrir el detalle. Sin asignación, SHALL responder `404 Not Found`. Si otra persona tiene el resultado reservado, SHALL responder `409 Conflict` sin modificar nada.

#### Scenario: Corrección completa por un evaluador
- **GIVEN** un resultado pendiente de una prueba asignada, reservado por el evaluador
- **WHEN** el evaluador envía una corrección válida de todas sus respuestas abiertas
- **THEN** el sistema cierra el resultado como `Reviewed`, registra al evaluador en `ReviewedByUserId`, libera la reserva y responde `200 OK` con el alias, la puntuación y el veredicto
- **AND** la respuesta no contiene el nombre ni el email del candidato

#### Scenario: Envío sin asignación
- **GIVEN** un evaluador al que el administrador quitó la asignación después de abrir el detalle
- **WHEN** el evaluador envía la corrección
- **THEN** el sistema responde `404 Not Found` y el resultado sigue en `PendingReview`

#### Scenario: Envío con el resultado reservado por otra persona
- **GIVEN** un resultado cuya reserva del evaluador caducó y que otro corrector reservó después
- **WHEN** el evaluador envía su corrección
- **THEN** el sistema responde `409 Conflict` y no modifica ninguna respuesta

### Requirement: Señales de integridad para el evaluador
El sistema SHALL exponer al evaluador las señales de integridad de la sesión de un resultado de una prueba asignada, con el mismo resumen y la misma cronología que recibe el administrador, salvo el momento de cada señal: en lugar de la hora del reloj, cada señal SHALL indicar los segundos transcurridos desde el inicio de la sesión. Así la hora a la que el candidato hizo la prueba no ayuda a identificarlo. Sin asignación, el sistema SHALL responder `404 Not Found`.

#### Scenario: Cronología sin hora del reloj
- **GIVEN** una sesión que empezó a las 10:00:00 con una salida de la página a las 10:12:30
- **WHEN** el evaluador asignado consulta las señales del resultado
- **THEN** esa señal indica 750 segundos desde el inicio y la respuesta no contiene ninguna hora del reloj

#### Scenario: Señales de un resultado no asignado
- **WHEN** el evaluador consulta las señales de un resultado de una prueba que no tiene asignada
- **THEN** el sistema responde `404 Not Found`

### Requirement: Historial de correcciones propias
El sistema SHALL exponer al evaluador la lista de los resultados con `ReviewedByUserId` igual a su identificador, del más reciente al más antiguo, con el alias, el título de la prueba, la fecha de la corrección sin la hora, la puntuación final y el veredicto. El sistema SHALL exponer también el detalle de solo lectura de cada uno: el enunciado, la respuesta del candidato, los puntos máximos, los puntos otorgados y el comentario de cada respuesta abierta. El historial SHALL NOT depender de la asignación: el evaluador sigue viendo sus correcciones aunque ya no tenga asignada la prueba. El historial SHALL NOT incluir el nombre ni el email del candidato. El detalle de un resultado que no corrigió el evaluador SHALL responder `404 Not Found`.

#### Scenario: Historial tras perder la asignación
- **GIVEN** un evaluador que corrigió un resultado de la prueba A y al que después quitaron la asignación de A
- **WHEN** solicita su historial
- **THEN** el resultado de la prueba A sigue en la lista, con su alias

#### Scenario: Detalle de una corrección ajena
- **WHEN** un evaluador solicita el detalle de historial de un resultado que corrigió otra persona
- **THEN** el sistema responde `404 Not Found`

#### Scenario: El historial es de solo lectura
- **WHEN** el evaluador consulta el detalle de una corrección suya
- **THEN** la respuesta muestra los puntos y los comentarios que dio, y el sistema no ofrece ninguna operación para modificarlos

### Requirement: Interfaz del evaluador
El sistema SHALL ofrecer al rol `Evaluador`:

- En `/evaluacion`, la cola de correcciones, con el alias, la prueba, la fecha, los días de espera, el número de respuestas y un aviso en las entradas reservadas por otra persona. Si la cola está vacía, un estado vacío que lo dice.
- En `/evaluacion/{resultId}`, la pantalla de corrección, con el mismo formulario y las mismas validaciones que la del administrador, el panel de señales con tiempos desde el inicio, y una acción para cancelar que libera la reserva.
- En `/evaluacion/historial` y `/evaluacion/historial/{resultId}`, el historial y su detalle de solo lectura.

Ninguna de estas páginas SHALL mostrar el nombre ni el email del candidato.

#### Scenario: Corrección de principio a fin
- **GIVEN** un evaluador con un resultado en su cola
- **WHEN** abre la corrección desde la cola, puntúa todas las respuestas y confirma
- **THEN** la interfaz muestra la puntuación final y el veredicto, vuelve a la cola, y el resultado ya no está en ella sino en el historial

#### Scenario: Abrir un resultado reservado por otra persona
- **GIVEN** un resultado reservado por otro corrector
- **WHEN** el evaluador intenta abrirlo
- **THEN** la interfaz muestra que está en corrección por otra persona y hasta cuándo, y no muestra el formulario

#### Scenario: Cancelar una corrección
- **GIVEN** un evaluador en la pantalla de corrección
- **WHEN** pulsa «Cancelar»
- **THEN** la interfaz libera la reserva y vuelve a la cola, donde el resultado queda libre para otros
