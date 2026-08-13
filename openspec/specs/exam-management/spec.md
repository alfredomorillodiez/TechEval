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

