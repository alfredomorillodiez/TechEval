## MODIFIED Requirements

### Requirement: Gestión del banco de preguntas desde la interfaz
El sistema SHALL permitir crear, editar y listar preguntas del banco desde la interfaz de administración, adaptando el formulario según el tipo de pregunta seleccionado, y SHALL ofrecer desde esa misma pantalla acceso a la generación de preguntas por IA y a la bandeja de revisión de preguntas generadas.

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

#### Scenario: Acceso a la generación de preguntas por IA desde el listado
- **GIVEN** un administrador en `/admin/questions`
- **WHEN** consulta la cabecera del listado
- **THEN** el sistema SHALL ofrecer un botón "Generar con IA" que navega a `/admin/questions/generate`

#### Scenario: Acceso a la bandeja de revisión desde el listado
- **GIVEN** un administrador en `/admin/questions` con al menos una pregunta en `QuestionReviewStatus = PendingReview`
- **WHEN** consulta la cabecera del listado
- **THEN** el sistema SHALL ofrecer un acceso a "Pendientes de revisión" que navega a `/admin/questions/review`, indicando la cantidad de preguntas pendientes
