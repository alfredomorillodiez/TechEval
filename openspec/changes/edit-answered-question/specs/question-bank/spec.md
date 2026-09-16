## ADDED Requirements

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
