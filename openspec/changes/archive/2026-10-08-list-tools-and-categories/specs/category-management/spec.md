## ADDED Requirements

### Requirement: Listado de categorías con estado
El sistema SHALL devolver cada categoría con su estado activo (`IsActive`). `GET /api/categories` SHALL devolver solo las categorías activas, y con `includeInactive=true` SHALL devolverlas todas. El contador de preguntas SHALL contar solo las preguntas activas de la categoría.

#### Scenario: Listado por defecto
- **GIVEN** una categoría activa y otra desactivada
- **WHEN** un administrador envía `GET /api/categories`
- **THEN** el sistema devuelve solo la activa, con `IsActive = true`

#### Scenario: Listado con las desactivadas
- **GIVEN** una categoría activa y otra desactivada
- **WHEN** un administrador envía `GET /api/categories?includeInactive=true`
- **THEN** el sistema devuelve las dos, cada una con su `IsActive`

### Requirement: Validación y unicidad del nombre de categoría
El sistema SHALL validar el nombre y la descripción de una categoría al crearla o editarla, y MUST responder `400 Bad Request` con un mensaje que diga qué falla, sin escribir nada. El nombre SHALL no estar vacío ni pasar de 100 caracteres, y la descripción SHALL no pasar de 500. Un nombre que ya usa otra categoría, sin distinguir mayúsculas ni espacios en los extremos, SHALL responder `409 Conflict` con un mensaje claro, y SHALL NOT llegar a la base de datos como un fallo de índice.

#### Scenario: Nombre vacío
- **WHEN** un administrador envía `POST /api/categories` con el nombre en blanco
- **THEN** el sistema responde `400` indicando que el nombre es obligatorio

#### Scenario: Nombre demasiado largo
- **WHEN** un administrador envía una categoría con un nombre de 101 caracteres
- **THEN** el sistema responde `400` indicando el máximo de 100 caracteres

#### Scenario: Descripción demasiado larga
- **WHEN** un administrador envía una categoría con una descripción de 501 caracteres
- **THEN** el sistema responde `400` indicando el máximo de 500 caracteres

#### Scenario: Nombre repetido en el alta
- **GIVEN** una categoría llamada «Backend»
- **WHEN** un administrador crea otra llamada « backend »
- **THEN** el sistema responde `409` indicando que ya existe una categoría con ese nombre
- **AND** no se crea ninguna categoría

#### Scenario: Renombrar a un nombre ya usado
- **GIVEN** las categorías «Backend» y «Frontend»
- **WHEN** un administrador renombra «Frontend» a «Backend»
- **THEN** el sistema responde `409` y «Frontend» no cambia

#### Scenario: Guardar una categoría sin cambiar su nombre
- **GIVEN** la categoría «Backend»
- **WHEN** un administrador la edita cambiando solo la descripción
- **THEN** el sistema acepta el cambio: el nombre no choca consigo misma

### Requirement: Reactivación de una categoría
El sistema SHALL permitir reactivar una categoría desactivada mediante `PUT /api/categories/{id}` con `IsActive = true`. Cuando `IsActive` no viene en la petición, SHALL conservar el estado actual de la categoría.

#### Scenario: Reactivar
- **GIVEN** una categoría desactivada
- **WHEN** un administrador envía `PUT /api/categories/{id}` con `IsActive = true`
- **THEN** la categoría queda activa y sus preguntas activas vuelven a contar en la generación

#### Scenario: Editar sin tocar el estado
- **GIVEN** una categoría desactivada
- **WHEN** un administrador la edita sin indicar `IsActive`
- **THEN** la categoría sigue desactivada

### Requirement: Pantalla de gestión de categorías
La interfaz SHALL ofrecer a los administradores una pantalla en `/admin/categorias`, enlazada desde su menú, que liste todas las categorías con su nombre, descripción, número de preguntas activas y estado; que permita buscar por nombre o descripción y ordenar por columna; que permita crear y editar una categoría en un diálogo, mostrando el mensaje del servidor si la rechaza; y que permita dar de baja una categoría, con confirmación, y reactivarla. Si el listado no se puede cargar, SHALL mostrar el aviso de error de carga con reintento.

#### Scenario: Alta de una tecnología nueva
- **WHEN** un administrador pulsa «Nueva categoría», escribe «Kubernetes» y guarda
- **THEN** la categoría aparece en el listado, activa y con 0 preguntas
- **AND** aparece en los desplegables del formulario de preguntas

#### Scenario: El nombre ya existe
- **WHEN** el administrador intenta crear una categoría con un nombre ya usado
- **THEN** el diálogo permanece abierto y muestra el mensaje del servidor

#### Scenario: Dar de baja con confirmación
- **WHEN** el administrador pulsa «Dar de baja» en una categoría
- **THEN** la pantalla pide confirmación y, al confirmar, marca la categoría como desactivada y la sigue mostrando en el listado con ese estado

#### Scenario: Reactivar
- **WHEN** el administrador pulsa «Reactivar» en una categoría desactivada
- **THEN** la categoría vuelve a estar activa

#### Scenario: Buscar
- **WHEN** el administrador escribe «dev» en el buscador
- **THEN** el listado muestra solo las categorías cuyo nombre o descripción contienen «dev», sin distinguir mayúsculas

### Requirement: Los formularios solo ofrecen categorías activas
Los formularios de preguntas, de generación de pruebas y de alta manual de pruebas SHALL ofrecer solo categorías activas. Al editar una pregunta cuya categoría está desactivada, el formulario SHALL mostrar esa categoría marcada como desactivada, y las demás categorías desactivadas SHALL NOT poder elegirse. El filtro del listado de preguntas SHALL ofrecer también las desactivadas, para poder consultar sus preguntas.

#### Scenario: Alta de una pregunta
- **GIVEN** una categoría desactivada
- **WHEN** un administrador abre el formulario de una pregunta nueva
- **THEN** el desplegable de categorías no la ofrece

#### Scenario: Editar una pregunta de una categoría desactivada
- **GIVEN** una pregunta cuya categoría está desactivada
- **WHEN** un administrador abre su edición
- **THEN** el desplegable muestra su categoría marcada como «(desactivada)» y seleccionada
