# exam-integrity Specification

## Purpose
Registrar señales de la actividad del candidato durante una prueba —salidas de la página y pegados en respuestas abiertas— y mostrarlas al corrector como información para decidir, sin que ningún proceso automático las use para puntuar o suspender.

## Requirements

### Requirement: Registro de señales durante la sesión en curso
El sistema SHALL exponer `POST /api/exam/integrity/{sessionId}` para registrar señales de integridad de una sesión de examen. El endpoint SHALL exigir el JWT de alumno que devuelve la validación del enlace, y SHALL comprobar que la sesión pertenece al alumno autenticado, con las mismas reglas que el auto-guardado de respuestas.

El sistema SHALL admitir tres tipos de señal:
- **Salida de la página**: la pestaña deja de estar visible o la ventana pierde el foco.
- **Vuelta a la página**: el candidato vuelve después de una salida. La señal lleva la duración de la ausencia.
- **Pegado en una respuesta abierta**: el candidato pega texto en el área de respuesta de una pregunta abierta. La señal lleva la pregunta y el número de caracteres pegados.

Cada señal SHALL guardar la pregunta que el candidato tenía en pantalla cuando se produjo.

#### Scenario: El candidato sale de la página
- **GIVEN** una `ExamSession` en estado `InProgress` que pertenece al alumno autenticado
- **WHEN** la interfaz envía una señal de salida de la página
- **THEN** el sistema guarda la señal asociada a la sesión, con su tipo, la pregunta en pantalla y la hora de recepción

#### Scenario: El candidato vuelve a la página
- **GIVEN** una `ExamSession` en estado `InProgress` que pertenece al alumno autenticado
- **WHEN** la interfaz envía una señal de vuelta con una ausencia de 150 segundos
- **THEN** el sistema guarda la señal con la duración de 150 segundos

#### Scenario: El candidato pega texto en una respuesta abierta
- **GIVEN** una `ExamSession` en estado `InProgress` que pertenece al alumno autenticado
- **WHEN** la interfaz envía una señal de pegado de 820 caracteres en una pregunta abierta
- **THEN** el sistema guarda la señal con la pregunta y el número 820
- **AND** el sistema SHALL NOT recibir ni guardar el texto pegado

#### Scenario: Señal sobre la sesión de otro alumno
- **GIVEN** una `ExamSession` que pertenece a otro alumno
- **WHEN** un alumno autenticado envía una señal con el identificador de esa sesión
- **THEN** el sistema responde `403 Forbidden` y SHALL NOT guardar la señal

#### Scenario: Señal sin autenticación
- **WHEN** se envía una señal sin JWT de alumno
- **THEN** el sistema responde `401 Unauthorized` y SHALL NOT guardar la señal

#### Scenario: Señal sobre una sesión ya terminada
- **GIVEN** una `ExamSession` en estado `Completed`, o una sesión que ya tiene `ExamResult`
- **WHEN** el alumno propietario envía una señal
- **THEN** el sistema responde `409 Conflict` y SHALL NOT guardar la señal

#### Scenario: Pegado sobre una pregunta que no es abierta o que no es del examen
- **WHEN** la interfaz envía una señal de pegado sobre una pregunta de test, o sobre una pregunta que no pertenece al examen de la sesión
- **THEN** el sistema responde `400 Bad Request` y SHALL NOT guardar la señal

### Requirement: Datos de la señal fijados por el servidor
El sistema SHALL fijar la hora de cada señal con el reloj del servidor, en UTC. El sistema SHALL NOT aceptar una hora enviada por el cliente. La duración de una ausencia la mide el cliente: el sistema SHALL guardarla como dato informado por el cliente y SHALL acotarla entre cero y el tiempo límite del examen. El número de caracteres de un pegado SHALL acotarse entre cero y la longitud máxima de una respuesta abierta.

#### Scenario: Duración negativa o excesiva
- **GIVEN** un examen de 60 minutos
- **WHEN** la interfaz envía una señal de vuelta con una duración negativa, o con una duración de 5 horas
- **THEN** el sistema guarda la duración acotada a 0 segundos, o a 60 minutos, respectivamente

#### Scenario: Hora de la señal
- **WHEN** el sistema guarda una señal
- **THEN** la hora guardada es la hora UTC del servidor en el momento de la recepción

### Requirement: Límite de señales por sesión
El sistema SHALL guardar como máximo 500 señales por sesión. Cuando una sesión alcanza el límite, el sistema SHALL dejar constancia de que el límite se alcanzó y SHALL descartar las señales siguientes sin error para el candidato.

#### Scenario: Señal número 501
- **GIVEN** una `ExamSession` en curso con 500 señales guardadas
- **WHEN** la interfaz envía otra señal
- **THEN** el sistema responde con éxito y SHALL NOT guardar la señal
- **AND** la sesión queda marcada como sesión que alcanzó el límite de señales

### Requirement: El registro de señales no interrumpe la prueba
Un fallo al enviar una señal SHALL NOT interrumpir la prueba ni mostrar ningún mensaje al candidato. La interfaz SHALL conservar en memoria las señales no enviadas y SHALL reintentarlas junto con la señal siguiente. El endpoint de señales SHALL tener limitación de ritmo propia, independiente de la del auto-guardado.

#### Scenario: El servidor no responde al enviar una señal
- **GIVEN** un candidato en mitad de la prueba
- **WHEN** el envío de una señal falla por un error de red
- **THEN** el candidato puede seguir respondiendo sin ningún aviso
- **AND** la interfaz reenvía la señal pendiente cuando se produce la señal siguiente

#### Scenario: El candidato vuelve después de una salida sin conexión
- **GIVEN** un candidato que salió de la página y perdió la conexión mientras estaba fuera
- **WHEN** el candidato vuelve y la conexión se recupera
- **THEN** la interfaz envía la señal de salida pendiente y después la señal de vuelta

### Requirement: Señales visibles para el corrector
El sistema SHALL permitir a usuarios con rol `Admin` consultar las señales de la sesión de un resultado con `GET /api/results/{id}/integrity`. La respuesta SHALL incluir un resumen y la cronología completa:
- Resumen: número de salidas, suma de las duraciones de ausencia, número de pegados, total de caracteres pegados y número de pegados por pregunta.
- Cronología: cada señal con su tipo, su hora, la pregunta en pantalla y su dato propio (duración o caracteres).
- Si la sesión alcanzó el límite de señales, la respuesta SHALL indicarlo.

La pantalla de detalle de un resultado y la pantalla de corrección de preguntas abiertas SHALL mostrar el resumen, y SHALL permitir desplegar la cronología. La pantalla de corrección SHALL mostrar, junto a cada respuesta abierta, los pegados de esa pregunta.

La interfaz SHALL presentar las duraciones de ausencia como datos medidos por el navegador del candidato.

#### Scenario: Resultado con señales
- **GIVEN** un resultado cuya sesión tiene tres salidas de 20, 45 y 150 segundos y un pegado de 820 caracteres en la pregunta 4
- **WHEN** un administrador solicita `GET /api/results/{id}/integrity`
- **THEN** el resumen indica 3 salidas, 215 segundos de ausencia, 1 pegado y 820 caracteres pegados, con 1 pegado en la pregunta 4
- **AND** la cronología contiene las siete señales ordenadas por hora

#### Scenario: Resultado sin señales
- **GIVEN** un resultado cuya sesión empezó después de este cambio y no tiene señales
- **WHEN** un administrador consulta las señales
- **THEN** el sistema indica que no hay señales registradas

#### Scenario: Resultado anterior al registro de señales
- **GIVEN** un resultado cuya sesión empezó antes de este cambio
- **WHEN** un administrador consulta las señales
- **THEN** el sistema indica que la sesión es anterior al registro de señales, y no lo presenta como una sesión sin señales

#### Scenario: Consulta sin rol de administrador
- **WHEN** un usuario sin rol `Admin` solicita `GET /api/results/{id}/integrity`
- **THEN** el sistema responde `403 Forbidden`, o `401 Unauthorized` si no está autenticado

#### Scenario: Resultado inexistente
- **WHEN** un administrador solicita las señales de un resultado que no existe
- **THEN** el sistema responde `404 Not Found`

### Requirement: Las señales no deciden la nota
Ningún proceso automático SHALL usar las señales para calcular la puntuación, cambiar el veredicto, cambiar el estado de corrección ni enviar un correo. El candidato SHALL NOT ver sus señales: el acuse de envío, el portal del alumno y los correos al candidato SHALL NOT incluirlas.

#### Scenario: Prueba de test con muchas salidas
- **GIVEN** una prueba solo con preguntas de test, cuya sesión tiene 40 salidas de la página
- **WHEN** el candidato envía la prueba
- **THEN** el sistema corrige y puntúa la prueba igual que si no tuviera señales
- **AND** el acuse y el correo al candidato no mencionan las señales
