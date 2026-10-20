## ADDED Requirements

### Requirement: Un fallo de carga no se muestra como un listado vacío
Toda pantalla que carga un listado de la API SHALL distinguir una respuesta vacía de un fallo de carga. Ante un fallo (API caída, red, `429`, `5xx`, sesión sin permiso distinta de la expirada), la pantalla SHALL mostrar un aviso que diga qué no se pudo cargar y un botón «Reintentar», y SHALL NOT mostrar el mensaje de «vacío» ni afirmar un estado del sistema que no ha podido comprobar. Una lista realmente vacía SHALL seguir mostrando su mensaje propio. Esto aplica, al menos, a: la bandeja de correcciones del administrador y la del evaluador, el historial del evaluador, el portal del alumno (pendientes y completadas), el listado de resultados y los resultados de una prueba, el listado de usuarios, el listado de pruebas, los evaluadores de una prueba y las categorías que alimentan los formularios del banco de preguntas y de las pruebas.

#### Scenario: Bandeja de correcciones con la API caída
- **GIVEN** la API parada
- **WHEN** un administrador abre `/admin/results/pending`
- **THEN** la pantalla muestra «No se pudo cargar las correcciones pendientes» con un botón «Reintentar»
- **AND** la pantalla SHALL NOT mostrar «No hay correcciones pendientes» ni «Todas las pruebas con preguntas abiertas están corregidas»

#### Scenario: Reintentar tras volver la API
- **GIVEN** una pantalla que mostró el error de carga
- **WHEN** la API vuelve y la persona pulsa «Reintentar»
- **THEN** la pantalla carga el listado y muestra sus filas, o su mensaje de vacío si de verdad no hay ninguna

#### Scenario: Una lista vacía de verdad
- **GIVEN** la API disponible y sin correcciones pendientes
- **WHEN** se abre la bandeja
- **THEN** la pantalla muestra su mensaje de que no hay correcciones pendientes, sin aviso de error

#### Scenario: Una carga auxiliar que falla
- **GIVEN** el formulario del banco de preguntas, que necesita las categorías para el desplegable
- **WHEN** las categorías no se pueden cargar
- **THEN** el formulario avisa de que no se pudieron cargar las categorías y ofrece reintentar, en lugar de mostrar un desplegable vacío sin explicación

### Requirement: Una pregunta que se escribe no se pierde ni cambia de estado sin avisar
El formulario de preguntas SHALL conservar el estado activo de la pregunta que se edita. Una pregunta desactivada SHALL mostrar un aviso de que lo está y un interruptor «Activa» que solo cambia si la persona lo cambia. Si el alta de una pregunta falla, el formulario SHALL permanecer abierto con todo lo escrito y SHALL mostrar el mensaje del servidor. Si la edición falla, SHALL mostrar el mensaje del servidor, y solo aconsejará dar de baja y crear otra cuando el servidor indique que la causa son respuestas ya registradas. Si la pregunta que se va a editar no se puede cargar, el formulario SHALL mostrar un error y SHALL NOT permitir guardar. Si una baja falla, la pantalla SHALL avisar.

#### Scenario: Editar una pregunta desactivada
- **GIVEN** una pregunta desactivada
- **WHEN** un administrador la abre, cambia el enunciado y guarda sin tocar el interruptor
- **THEN** la pregunta sigue desactivada

#### Scenario: Reactivar a propósito
- **GIVEN** una pregunta desactivada abierta en el formulario
- **WHEN** el administrador activa el interruptor «Activa» y guarda
- **THEN** la pregunta queda activa

#### Scenario: El alta falla
- **GIVEN** un alta que el servidor rechaza con un mensaje
- **WHEN** el administrador pulsa guardar
- **THEN** el formulario permanece abierto, conserva lo escrito y muestra el mensaje
- **AND** la pantalla SHALL NOT navegar al listado

#### Scenario: La pregunta que se edita no carga
- **GIVEN** la API caída al abrir `/admin/questions/{id}/edit`
- **WHEN** el formulario intenta cargar la pregunta
- **THEN** muestra un error con «Reintentar» y no deja guardar un formulario vacío

#### Scenario: Una baja que falla
- **WHEN** el servidor rechaza dar de baja una pregunta
- **THEN** la pantalla muestra un aviso y la pregunta sigue en el listado
