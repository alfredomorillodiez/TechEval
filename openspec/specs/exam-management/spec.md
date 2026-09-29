# exam-management Specification

## Purpose
TBD - created by archiving change exam-management. Update Purpose after archive.

## Requirements

### Requirement: Creación manual de examen
El sistema SHALL permitir a un administrador crear un examen manualmente indicando título, descripción, tiempo límite en minutos, porcentaje de aprobación y una lista explícita de identificadores de preguntas (`QuestionIds`) del banco de preguntas.

#### Scenario: Todas las preguntas indicadas existen y están activas
- **GIVEN** un administrador autenticado con rol `Admin`
- **WHEN** envía `POST /api/exams` con `QuestionIds` que corresponden a preguntas existentes y activas
- **THEN** el sistema SHALL crear el examen asociando cada pregunta mediante un registro `ExamQuestion` y SHALL devolver `201 Created` con el examen resultante, incluyendo su cantidad de preguntas y puntos totales

#### Scenario: Alguna pregunta indicada no existe o está inactiva
- **GIVEN** un administrador autenticado
- **WHEN** envía `POST /api/exams` con al menos un `QuestionId` que no existe o que corresponde a una pregunta inactiva
- **THEN** el sistema SHALL rechazar la operación con un error indicando que algunas preguntas no existen o están inactivas, y SHALL NOT crear el examen

### Requirement: Generación automática de examen
El sistema SHALL permitir a un administrador generar un examen automáticamente indicando la cantidad de preguntas deseadas (`QuestionCount`) y, opcionalmente, una lista de categorías (`CategoryIds`) y un nivel de dificultad (`Difficulty`), seleccionando preguntas activas al azar que cumplan esos criterios.

#### Scenario: Hay suficientes preguntas activas que cumplen los criterios
- **GIVEN** un administrador autenticado
- **WHEN** envía `POST /api/exams/generate` con `QuestionCount` igual o menor a la cantidad de preguntas activas disponibles que cumplen los filtros de categoría y dificultad indicados
- **THEN** el sistema SHALL seleccionar aleatoriamente esa cantidad de preguntas, SHALL crear el examen con esas preguntas y SHALL devolver `201 Created` con el examen resultante

#### Scenario: Filtros opcionales de categoría y dificultad se omiten
- **GIVEN** un administrador autenticado
- **WHEN** envía `POST /api/exams/generate` sin `CategoryIds` ni `Difficulty`
- **THEN** el sistema SHALL considerar todas las preguntas activas del banco como elegibles para la selección aleatoria

### Requirement: Validación de disponibilidad de preguntas en generación automática
El sistema SHALL validar, antes de crear el examen generado automáticamente, que exista al menos la cantidad de preguntas activas solicitadas que cumplan los criterios indicados.

#### Scenario: No hay suficientes preguntas disponibles
- **GIVEN** un administrador autenticado
- **WHEN** envía `POST /api/exams/generate` solicitando una cantidad (`QuestionCount`) de preguntas mayor a la cantidad de preguntas activas disponibles que cumplen los criterios de categoría y/o dificultad
- **THEN** el sistema SHALL rechazar la operación con un mensaje que indique cuántas preguntas se encontraron frente a las solicitadas, y SHALL NOT crear el examen

### Requirement: Orden único y persistente de preguntas por examen
El sistema SHALL asignar y persistir un orden secuencial único (`ExamQuestion.Order`) a cada pregunta dentro de un examen, tanto en creación manual como en generación automática.

#### Scenario: Orden asignado en creación manual
- **GIVEN** una solicitud de creación manual de examen con una lista ordenada de `QuestionIds`
- **WHEN** el examen se crea
- **THEN** el sistema SHALL asignar a cada `ExamQuestion` un valor de `Order` secuencial que refleje la posición de la pregunta en la lista recibida, comenzando en 1

#### Scenario: Orden asignado en generación automática
- **GIVEN** una solicitud de generación automática de examen
- **WHEN** el sistema selecciona las preguntas aleatorias
- **THEN** el sistema SHALL asignar a cada `ExamQuestion` un valor de `Order` secuencial único según el orden de selección, comenzando en 1

### Requirement: Desactivación de examen (soft delete)
El sistema SHALL permitir desactivar un examen existente sin eliminarlo físicamente de la base de datos, preservando los tokens y resultados ya asociados a él.

#### Scenario: Desactivar un examen existente
- **GIVEN** un examen existente identificado por `id`
- **WHEN** un administrador envía `DELETE /api/exams/{id}`
- **THEN** el sistema SHALL marcar el examen como inactivo (`IsActive = false`), SHALL actualizar su fecha de modificación y SHALL devolver `204 No Content`, conservando el registro y su historial asociado

#### Scenario: Desactivar un examen que no existe
- **GIVEN** que no existe ningún examen con el `id` indicado
- **WHEN** un administrador envía `DELETE /api/exams/{id}`
- **THEN** el sistema SHALL devolver `404 Not Found` y SHALL NOT modificar ningún registro

### Requirement: Listado de exámenes con estadísticas
El sistema SHALL exponer un listado de todos los exámenes que incluya, para cada uno, la cantidad de preguntas, los puntos totales, el porcentaje de aprobación, el estado activo/inactivo, la cantidad de tokens de examen enviados y la cantidad de resultados obtenidos.

#### Scenario: Consultar el listado de exámenes
- **GIVEN** que existen exámenes creados en el sistema, algunos con tokens enviados y resultados registrados
- **WHEN** un administrador envía `GET /api/exams`
- **THEN** el sistema SHALL devolver `200 OK` con un resumen por examen que incluya cantidad de preguntas, puntos totales, tokens enviados y cantidad de resultados asociados

### Requirement: Consulta de detalle de examen
El sistema SHALL exponer el detalle completo de un examen, incluyendo sus preguntas ordenadas, los textos, tipo, dificultad, puntos y opciones de respuesta de cada pregunta (incluyendo cuál es la correcta, para uso administrativo).

#### Scenario: Consultar un examen existente
- **GIVEN** un examen existente con preguntas asociadas
- **WHEN** un administrador envía `GET /api/exams/{id}`
- **THEN** el sistema SHALL devolver `200 OK` con el examen y sus preguntas ordenadas por `Order`, incluyendo las opciones de respuesta y cuál de ellas es correcta

#### Scenario: Consultar un examen que no existe
- **GIVEN** que no existe ningún examen con el `id` indicado
- **WHEN** un administrador envía `GET /api/exams/{id}`
- **THEN** el sistema SHALL devolver `404 Not Found`

### Requirement: Asignación de evaluadores a una prueba
El sistema SHALL permitir a un administrador asignar evaluadores a una prueba y quitárselos. Una prueba SHALL poder tener varios evaluadores, y un evaluador varias pruebas. Solo SHALL poder asignarse un usuario con rol `Evaluador` y activo; en otro caso, el sistema SHALL responder `400 Bad Request`. Asignar un evaluador que ya está asignado SHALL NOT duplicar la asignación. Cada asignación SHALL guardar quién la hizo y cuándo.

El sistema SHALL exponer la lista de evaluadores de una prueba con su nombre y su email. Quitar una asignación SHALL NOT modificar las correcciones que el evaluador ya hizo en esa prueba.

#### Scenario: Asignación de un evaluador
- **GIVEN** una prueba sin evaluadores y un usuario activo con rol `Evaluador`
- **WHEN** un administrador lo asigna a la prueba
- **THEN** el sistema responde con la lista de evaluadores de la prueba, que lo incluye

#### Scenario: Asignación de un usuario que no es evaluador
- **WHEN** un administrador intenta asignar a una prueba un usuario con rol `Admin` o `Alumno`, o un evaluador desactivado
- **THEN** el sistema responde `400 Bad Request` y no crea la asignación

#### Scenario: Asignación repetida
- **GIVEN** un evaluador ya asignado a una prueba
- **WHEN** un administrador lo asigna otra vez
- **THEN** la prueba sigue con una sola asignación de ese evaluador

#### Scenario: Quitar una asignación conserva las correcciones
- **GIVEN** un evaluador que corrigió dos resultados de una prueba
- **WHEN** un administrador le quita la asignación
- **THEN** los dos resultados siguen corregidos y con el evaluador en `ReviewedByUserId`

#### Scenario: Acceso sin rol Admin
- **WHEN** un evaluador intenta asignar o quitar evaluadores
- **THEN** el sistema responde `403 Forbidden`

### Requirement: Reparto por nivel de dificultad en la generación automática
El sistema SHALL aceptar en `POST /api/exams/generate`, en lugar de un nivel único, un porcentaje entero para cada nivel de dificultad (`Basic`, `Intermediate`, `Advanced`). Cada porcentaje SHALL estar entre 0 y 100 y los tres SHALL sumar 100; en otro caso, el sistema SHALL responder `400 Bad Request`. Un nivel al 0 % queda fuera del reparto y la prueba no tiene ninguna pregunta de ese nivel. Indicar a la vez un nivel único y un reparto SHALL responder `400 Bad Request`. El porcentaje SHALL referirse al número de preguntas, no a los puntos.

El número de preguntas de cada nivel SHALL calcularse con el método del mayor resto: cada nivel recibe la parte entera de su cuota (`QuestionCount × porcentaje / 100`), y las preguntas que faltan hasta `QuestionCount` van, una a una, a los niveles con mayor parte decimal. Si dos niveles empatan en la parte decimal, la pregunta va al de mayor porcentaje, y si también empatan, al nivel más fácil.

Las preguntas de cada nivel SHALL elegirse al azar entre las preguntas activas de ese nivel en las categorías indicadas, o en todas si no se indica ninguna. El orden de las preguntas en la prueba SHALL mezclar los niveles, no agruparlos.

#### Scenario: Reparto exacto
- **GIVEN** un banco con suficientes preguntas activas de los tres niveles
- **WHEN** un administrador genera una prueba de 10 preguntas con el reparto 30 / 50 / 20
- **THEN** la prueba tiene 3 preguntas básicas, 5 intermedias y 2 avanzadas

#### Scenario: Redondeo por mayor resto
- **WHEN** un administrador genera una prueba de 10 preguntas con el reparto 33 / 33 / 34
- **THEN** la prueba tiene 3 preguntas básicas, 3 intermedias y 4 avanzadas, que suman 10

#### Scenario: Empate en el redondeo
- **WHEN** un administrador genera una prueba de 3 preguntas con el reparto 50 / 50 / 0
- **THEN** la prueba tiene 2 preguntas básicas y 1 intermedia, porque en el empate gana el nivel más fácil, y ninguna avanzada

#### Scenario: Un nivel al 0 % no aparece
- **WHEN** un administrador genera una prueba con el reparto 0 / 100 / 0
- **THEN** todas las preguntas de la prueba son intermedias

#### Scenario: Porcentajes que no suman 100
- **WHEN** un administrador genera una prueba con el reparto 30 / 30 / 30
- **THEN** el sistema responde `400 Bad Request` y no crea la prueba

#### Scenario: Nivel único y reparto a la vez
- **WHEN** la petición indica el nivel `Advanced` y además un reparto
- **THEN** el sistema responde `400 Bad Request` y no crea la prueba

#### Scenario: Los niveles salen mezclados
- **WHEN** un administrador genera una prueba con un reparto de dos o más niveles
- **THEN** el orden de la prueba no agrupa las preguntas por nivel de forma sistemática

### Requirement: Rechazo si un nivel del reparto no alcanza
Si algún nivel del reparto necesita más preguntas de las que hay activas de ese nivel en las categorías indicadas, el sistema SHALL rechazar la generación con `400 Bad Request` y SHALL NOT crear la prueba. El mensaje SHALL nombrar cada nivel que no alcanza, con las preguntas que necesita y las que hay. El sistema SHALL NOT completar nunca un nivel con preguntas de otro nivel ni de otra categoría.

#### Scenario: Faltan preguntas avanzadas
- **GIVEN** unas categorías con 8 preguntas básicas, 15 intermedias y 2 avanzadas activas
- **WHEN** un administrador genera una prueba de 10 preguntas con el reparto 20 / 30 / 50
- **THEN** el sistema rechaza la generación con un mensaje que dice que el nivel avanzado necesita 5 preguntas y hay 2, y no crea la prueba

#### Scenario: Faltan preguntas de dos niveles
- **GIVEN** unas categorías con 1 pregunta básica, 20 intermedias y 1 avanzada activas
- **WHEN** un administrador genera una prueba de 10 preguntas con el reparto 30 / 40 / 30
- **THEN** el mensaje nombra el nivel básico (necesita 3, hay 1) y el avanzado (necesita 3, hay 1)

#### Scenario: Sobran preguntas de otros niveles
- **GIVEN** unas categorías con 50 preguntas básicas y 1 intermedia activas
- **WHEN** un administrador genera una prueba de 5 preguntas con el reparto 0 / 100 / 0
- **THEN** el sistema rechaza la generación, aunque haya preguntas básicas de sobra

### Requirement: El nivel único no cambia
Cuando la generación indica un nivel único, la prueba SHALL tener solo preguntas de ese nivel, y el sistema SHALL rechazar la generación si no hay bastantes, como hoy. Sin nivel ni reparto, el comportamiento SHALL seguir como hoy: preguntas al azar de todos los niveles.

#### Scenario: Nivel único sin preguntas suficientes
- **GIVEN** unas categorías con 2 preguntas avanzadas activas
- **WHEN** un administrador genera una prueba de 5 preguntas con el nivel único `Advanced`
- **THEN** el sistema rechaza la generación y no crea la prueba
