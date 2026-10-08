## REMOVED Requirements

### Requirement: Gestión del banco de preguntas desde la interfaz
**Reason**: El requisito obligaba a ofrecer desde el listado el acceso a la generación por IA y a la bandeja de revisión. Los dos desaparecen con el modelo local. Se sustituye por «Gestión manual del banco de preguntas desde la interfaz», que exige lo contrario: que esos accesos no existan.
**Migration**: El alta de preguntas es manual, en `/admin/questions/new`. No hay equivalente automático.

## ADDED Requirements

### Requirement: Gestión manual del banco de preguntas desde la interfaz
El sistema SHALL permitir crear, editar y listar preguntas del banco desde la interfaz de administración, adaptando el formulario según el tipo de pregunta seleccionado. El alta de preguntas SHALL ser exclusivamente manual, sin ofrecer ningún acceso a generación por IA ni a bandeja de revisión de preguntas generadas.

#### Scenario: Listado filtrable de preguntas
- **GIVEN** un administrador en `/admin/questions`
- **WHEN** selecciona una categoría, un nivel de dificultad o un tipo en los filtros superiores
- **THEN** el sistema SHALL recargar el listado llamando a `GET /api/questions` con los parámetros de filtro correspondientes

#### Scenario: Creación de una pregunta de tipo test
- **GIVEN** un administrador en `/admin/questions/new` con el tipo "Test (4 opciones)" seleccionado
- **WHEN** completa el enunciado, la categoría, marca exactamente una opción como correcta entre las cuatro disponibles, y guarda
- **THEN** el sistema llama a `POST /api/questions` con las cuatro respuestas y navega de vuelta a `/admin/questions`

#### Scenario: Intento de guardar sin marcar respuesta correcta
- **GIVEN** un administrador creando o editando una pregunta de tipo test
- **WHEN** intenta guardar sin haber marcado ninguna opción como correcta
- **THEN** el sistema MUST mostrar el mensaje "Marca la respuesta correcta." y no llamar a la API

#### Scenario: Edición de una pregunta existente
- **GIVEN** un administrador que navega a `/admin/questions/{id}` de una pregunta existente
- **WHEN** el formulario carga los datos vía `GET /api/questions/{id}` y el administrador modifica campos y guarda
- **THEN** el sistema llama a `PUT /api/questions/{id}` con los datos actualizados y navega de vuelta a `/admin/questions`

#### Scenario: Baja de una pregunta desde el listado
- **GIVEN** un administrador en `/admin/questions` con al menos una pregunta listada
- **WHEN** pulsa el botón de eliminar sobre una fila
- **THEN** el sistema llama a `DELETE /api/questions/{id}` y, si la operación es exitosa, SHALL retirar esa fila del listado sin recargar la página completa

#### Scenario: El listado no ofrece generación por IA
- **GIVEN** un administrador en `/admin/questions`
- **WHEN** consulta la cabecera del listado
- **THEN** el sistema SHALL NOT ofrecer ningún botón "Generar con IA" ni ningún acceso a una bandeja de "Pendientes de revisión"
