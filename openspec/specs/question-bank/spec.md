# question-bank Specification

## Purpose
TBD - created by archiving change question-bank. Update Purpose after archive.

## Requirements

### Requirement: Creación de categorías con nombre único
El sistema SHALL permitir a un administrador crear una categoría de preguntas con nombre y descripción, y MUST rechazar la creación de una categoría cuyo nombre coincida (a nivel de base de datos) con el de una categoría existente.

#### Scenario: Alta de categoría válida
- **GIVEN** un administrador autenticado con rol `Admin`
- **WHEN** envía `POST /api/categories` con un nombre no utilizado previamente y una descripción
- **THEN** el sistema crea la categoría, la marca activa por defecto y devuelve `201 Created` con el recurso creado, incluyendo un contador de preguntas activas en cero

#### Scenario: Nombre de categoría duplicado
- **GIVEN** que ya existe una categoría llamada "Backend"
- **WHEN** un administrador intenta crear otra categoría llamada "Backend"
- **THEN** la operación SHALL fallar por violación de la restricción de unicidad y no se crea una segunda categoría con ese nombre

### Requirement: Creación de preguntas tipo test con validación de respuestas
El sistema SHALL permitir crear preguntas de tipo `MultipleChoice` asociadas a una categoría, y MUST exigir que la pregunta tenga exactamente 4 opciones de respuesta y exactamente 1 de ellas marcada como correcta antes de persistirla.

#### Scenario: Pregunta tipo test válida
- **GIVEN** un administrador que crea una pregunta de tipo `MultipleChoice` con dificultad, categoría y puntos
- **WHEN** envía exactamente 4 respuestas, una de ellas con `IsCorrect = true`
- **THEN** el sistema crea la pregunta junto con sus 4 respuestas ordenadas y devuelve el detalle completo con `201 Created`

#### Scenario: Número incorrecto de opciones
- **GIVEN** una pregunta de tipo `MultipleChoice` en creación
- **WHEN** se envían menos o más de 4 respuestas
- **THEN** el sistema SHALL rechazar la operación lanzando un error indicando que las preguntas tipo test deben tener exactamente 4 respuestas, sin crear la pregunta

#### Scenario: Ninguna o más de una respuesta correcta
- **GIVEN** una pregunta de tipo `MultipleChoice` con exactamente 4 respuestas
- **WHEN** el número de respuestas marcadas con `IsCorrect = true` es distinto de 1 (cero o más de una)
- **THEN** el sistema SHALL rechazar la operación indicando que debe existir exactamente 1 respuesta correcta, sin crear la pregunta

### Requirement: Creación de preguntas abiertas con respuesta modelo
El sistema SHALL permitir crear preguntas de tipo `OpenEnded` con un campo opcional `SampleAnswer` que sirva de referencia al corrector, sin exigir un conjunto de respuestas predefinidas.

#### Scenario: Pregunta abierta con respuesta modelo
- **GIVEN** un administrador que crea una pregunta de tipo `OpenEnded`
- **WHEN** incluye un texto de `SampleAnswer` como referencia de corrección
- **THEN** el sistema crea la pregunta sin exigir respuestas de opción múltiple y persiste el texto de referencia junto a la pregunta

### Requirement: Baja lógica (soft delete) de preguntas
El sistema SHALL dar de baja una pregunta marcándola como inactiva (`IsActive = false`) en lugar de eliminarla físicamente, preservando su historial de resultados asociado.

#### Scenario: Eliminación de una pregunta existente
- **GIVEN** una pregunta activa identificada por su `Id`
- **WHEN** un administrador envía `DELETE /api/questions/{id}`
- **THEN** el sistema SHALL marcar la pregunta como `IsActive = false`, actualizar su fecha de modificación y devolver `204 No Content`, sin eliminar el registro de la base de datos

#### Scenario: Eliminación de una pregunta inexistente
- **GIVEN** un `Id` que no corresponde a ninguna pregunta
- **WHEN** un administrador envía `DELETE /api/questions/{id}` con ese identificador
- **THEN** el sistema SHALL devolver `404 Not Found` sin modificar ningún registro

### Requirement: Baja lógica (soft delete) de categorías
El sistema SHALL dar de baja una categoría marcándola como inactiva en lugar de eliminarla físicamente, de forma consistente con el tratamiento de las preguntas.

#### Scenario: Desactivación de una categoría
- **GIVEN** una categoría activa con preguntas asociadas
- **WHEN** un administrador envía `DELETE /api/categories/{id}`
- **THEN** el sistema SHALL marcar la categoría como `IsActive = false` y devolver `204 No Content`, preservando las preguntas ya vinculadas a ella

### Requirement: Validación de datos de entrada del banco de preguntas
El sistema MUST validar la consistencia de los datos de categorías y preguntas antes de persistirlos, incluyendo longitudes máximas de texto y pertenencia a una categoría existente.

#### Scenario: Texto de pregunta dentro del límite permitido
- **GIVEN** una pregunta en creación con un texto de enunciado
- **WHEN** el texto no supera los 2000 caracteres permitidos por la configuración de persistencia
- **THEN** el sistema SHALL aceptar y almacenar la pregunta correctamente

#### Scenario: Pregunta referenciando una categoría inexistente
- **GIVEN** un `CategoryId` que no corresponde a ninguna categoría existente
- **WHEN** se intenta crear una pregunta con ese `CategoryId`
- **THEN** el sistema SHALL rechazar la operación por la restricción de clave foránea entre `Question` y `Category`, sin persistir la pregunta

### Requirement: Visualización de fecha y hora de creación y actualización de una pregunta
El sistema SHALL exponer y mostrar, en la pantalla de edición de una pregunta, la fecha y hora exactas (con precisión de minutos) de creación (`CreatedAt`) y, si existe, de última actualización (`UpdatedAt`) de la pregunta, expresadas en UTC.

#### Scenario: Pregunta con fecha de creación y sin actualizaciones posteriores
- **GIVEN** una pregunta creada y nunca modificada desde su creación
- **WHEN** un administrador abre su pantalla de edición
- **THEN** el sistema SHALL mostrar la fecha y hora de creación en UTC con precisión de minutos, sin mostrar una fecha de última actualización

#### Scenario: Pregunta editada después de su creación
- **GIVEN** una pregunta que fue modificada después de creada
- **WHEN** un administrador abre su pantalla de edición
- **THEN** el sistema SHALL mostrar tanto la fecha y hora de creación como la de última actualización, ambas en UTC con precisión de minutos

### Requirement: Filtrado de preguntas activas del banco
El sistema SHALL permitir listar preguntas filtrando de forma combinable por categoría, nivel de dificultad y tipo de pregunta, devolviendo únicamente preguntas activas (`IsActive = true`) por defecto.

#### Scenario: Filtro combinado por categoría y dificultad
- **GIVEN** un banco de preguntas con múltiples categorías y niveles de dificultad
- **WHEN** se solicita `GET /api/questions?categoryId=1&difficulty=Advanced`
- **THEN** el sistema SHALL devolver únicamente las preguntas activas de la categoría 1 con dificultad `Advanced`, ordenadas por nombre de categoría y dificultad

#### Scenario: Filtro sin parámetros
- **GIVEN** un banco de preguntas existente
- **WHEN** se solicita `GET /api/questions` sin parámetros de filtro
- **THEN** el sistema SHALL devolver el listado resumido de todas las preguntas activas, independientemente de categoría, dificultad o tipo

### Requirement: Edición de una pregunta sin destruir su histórico de respuestas
El sistema SHALL permitir modificar una pregunta existente —enunciado, categoría, dificultad, puntos, respuesta modelo y opciones— sin borrar y recrear sus opciones de respuesta. Cada opción recibida SHALL emparejarse con la existente por su identificador y actualizarse en su sitio; una opción sin identificador SHALL tratarse como nueva. El sistema MUST conservar el identificador de toda opción que algún candidato ya haya elegido, porque `UserAnswer.SelectedAnswerId` la referencia y perderla destruiría el registro de lo que ese candidato respondió.

#### Scenario: Edición de una pregunta que nadie ha respondido todavía
- **GIVEN** una pregunta tipo test cuyas opciones no están referenciadas por ninguna `UserAnswer`
- **WHEN** un administrador guarda cambios en su enunciado y en el texto de sus opciones
- **THEN** el sistema actualiza la pregunta y sus opciones, y registra `UpdatedAt`
- **AND** los identificadores de las opciones no cambian

#### Scenario: Edición de una pregunta ya respondida por candidatos
- **GIVEN** una pregunta tipo test con al menos una opción ya elegida en una `UserAnswer`
- **WHEN** un administrador corrige el enunciado o el texto de las opciones y guarda
- **THEN** el sistema SHALL completar la edición correctamente
- **AND** el sistema SHALL NOT emitir ningún borrado sobre las opciones existentes
- **AND** las respuestas ya registradas siguen apuntando a la misma opción

#### Scenario: Cambio del veredicto de una opción ya elegida
- **GIVEN** una pregunta tipo test ya respondida cuya opción marcada como correcta resultó ser errónea
- **WHEN** un administrador cambia cuál de las opciones es la correcta y guarda
- **THEN** el sistema actualiza el campo `IsCorrect` de las opciones afectadas sin recrearlas
- **AND** los resultados ya cerrados conservan la puntuación que se les otorgó, porque se congela en `UserAnswer.AwardedPoints`

#### Scenario: Añadir una opción nueva a una pregunta existente
- **GIVEN** una pregunta a la que se añade una opción que no tenía
- **WHEN** el administrador guarda con una opción sin identificador
- **THEN** el sistema crea esa opción y conserva las anteriores con su identificador

#### Scenario: Eliminar una opción que nadie ha elegido
- **GIVEN** una pregunta con una opción que ninguna `UserAnswer` referencia
- **WHEN** el administrador guarda sin incluir esa opción
- **THEN** el sistema borra esa opción y mantiene el resto

#### Scenario: Intento de eliminar una opción ya elegida por un candidato
- **GIVEN** una pregunta con una opción referenciada por al menos una `UserAnswer`
- **WHEN** el administrador guarda sin incluir esa opción, por ejemplo al convertir la pregunta a respuesta abierta
- **THEN** el sistema MUST rechazar la operación con un conflicto y un mensaje que explique que esa opción ya ha sido elegida por candidatos
- **AND** el sistema SHALL NOT modificar nada de la pregunta: ni el enunciado, ni las otras opciones
- **AND** el rechazo SHALL NOT llegar como un error interno del servidor
