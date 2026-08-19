## MODIFIED Requirements

### Requirement: Validación del enlace y pantalla de bienvenida
Al abrir la ruta `/prueba/{Token}`, la interfaz SHALL validar el token contra el backend antes de mostrar ningún contenido de la prueba, y SHALL presentar una pantalla de bienvenida con el título de la prueba, el nombre del candidato, el número de preguntas y el tiempo límite únicamente cuando el token resulte válido.

#### Scenario: Token válido muestra información de la prueba
- **WHEN** el candidato abre un enlace con un token que existe, no ha expirado y no ha sido usado
- **THEN** la interfaz muestra la pantalla de bienvenida con el título de la prueba, el nombre del candidato, el número de preguntas y el tiempo límite en minutos, junto con un botón para comenzar

#### Scenario: Token inválido o expirado bloquea el acceso
- **WHEN** el candidato abre un enlace con un token inexistente, expirado o ya utilizado
- **THEN** la interfaz muestra una pantalla de error con el mensaje devuelto por el backend y SHALL NOT mostrar la pantalla de bienvenida ni ninguna pregunta de la prueba

### Requirement: Inicio del examen con temporizador visible
Al pulsar "Comenzar prueba" desde la pantalla de bienvenida, la interfaz SHALL iniciar la sesión de la prueba contra el backend y SHALL mostrar un temporizador en cuenta atrás visible en todo momento durante la resolución de la prueba.

#### Scenario: Inicio de sesión arranca el temporizador
- **WHEN** el candidato pulsa "Comenzar prueba"
- **THEN** la interfaz solicita el inicio de sesión al backend, transiciona a la pantalla de preguntas y arranca un temporizador en cuenta atrás inicializado con el tiempo límite de la prueba en minutos

#### Scenario: El temporizador cambia de aspecto según el tiempo restante
- **WHEN** el tiempo restante desciende por debajo de 5 minutos, y posteriormente por debajo de 1 minuto
- **THEN** el indicador visual del temporizador cambia progresivamente a un estilo de advertencia y luego a un estilo crítico, manteniendo el formato mm:ss en todo momento

### Requirement: Presentación de preguntas de test y abiertas
Durante la resolución de la prueba, la interfaz SHALL renderizar cada pregunta según su tipo — opciones de selección única para preguntas tipo test, o un área de texto libre para preguntas abiertas — y SHALL permitir navegar libremente entre preguntas sin perder las respuestas ya introducidas.

#### Scenario: Pregunta tipo test muestra opciones seleccionables
- **WHEN** la pregunta actual es de tipo test
- **THEN** la interfaz muestra cada opción de respuesta como un elemento seleccionable individualmente, resaltando visualmente la opción que el candidato ya haya marcado

#### Scenario: Pregunta abierta muestra campo de texto libre
- **WHEN** la pregunta actual es de tipo abierta
- **THEN** la interfaz muestra un área de texto editable precargada con el borrador de respuesta existente, si lo hubiera

#### Scenario: Navegación conserva las respuestas
- **WHEN** el candidato navega hacia la pregunta anterior, la siguiente, o selecciona una pregunta desde el mini-mapa de navegación
- **THEN** la interfaz conserva en memoria las respuestas ya introducidas para todas las preguntas y las vuelve a mostrar si el candidato regresa a ellas

### Requirement: Auto-guardado de respuestas
La interfaz SHALL enviar cada respuesta al backend en el momento en que el candidato la registra, sin esperar al envío final de la prueba: inmediatamente al seleccionar una opción en preguntas tipo test, y al perder el foco del campo de texto en preguntas abiertas.

#### Scenario: Selección de una opción de test se guarda de inmediato
- **WHEN** el candidato selecciona una opción de respuesta en una pregunta tipo test
- **THEN** la interfaz envía esa respuesta al backend para la sesión activa sin requerir ninguna acción adicional del candidato

#### Scenario: Respuesta abierta se guarda al perder el foco
- **WHEN** el candidato termina de escribir en el área de texto de una pregunta abierta y el campo pierde el foco
- **THEN** la interfaz envía el contenido actual del campo al backend como respuesta guardada para esa pregunta

### Requirement: Auto-envío al agotarse el tiempo
La interfaz SHALL enviar automáticamente la prueba completa, con todas las respuestas registradas hasta ese momento, en el instante en que el temporizador alcanza cero, sin requerir confirmación del candidato.

#### Scenario: El tiempo se agota durante la resolución
- **WHEN** el temporizador en cuenta atrás llega a cero mientras el candidato aún está respondiendo la prueba
- **THEN** la interfaz detiene el temporizador y envía automáticamente la prueba con el conjunto de respuestas disponible en ese momento, sin mostrar el diálogo de confirmación manual

### Requirement: Envío manual antes de agotar el tiempo
La interfaz SHALL permitir al candidato finalizar y enviar la prueba manualmente en cualquier momento antes de que se agote el tiempo, mostrando previamente un diálogo de confirmación con el número de preguntas respondidas.

#### Scenario: Candidato solicita finalizar desde la última pregunta
- **WHEN** el candidato pulsa "Finalizar prueba" estando en la última pregunta y con tiempo restante
- **THEN** la interfaz muestra un diálogo de confirmación indicando cuántas preguntas de las totales han sido respondidas, antes de enviar nada al backend

#### Scenario: Candidato confirma el envío manual
- **WHEN** el candidato confirma el envío desde el diálogo de confirmación
- **THEN** la interfaz detiene el temporizador, envía el conjunto completo de respuestas al backend y transiciona a la pantalla de resultado final

### Requirement: Pantalla de confirmación con resultado final
Tras un envío exitoso (manual o automático), la interfaz SHALL mostrar una pantalla final con el resultado obtenido y un aviso de que el detalle llegará también por email, sin permitir volver a las preguntas de la prueba.

#### Scenario: Prueba enviada muestra el resultado
- **WHEN** el envío de la prueba se completa correctamente
- **THEN** la interfaz muestra si el candidato aprobó o no, el porcentaje de puntuación, los puntos obtenidos sobre el total, y un mensaje indicando que recibirá los resultados por email
