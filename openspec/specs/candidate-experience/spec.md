# candidate-experience Specification

## Purpose
TBD - created by archiving change candidate-experience. Update Purpose after archive.

## Requirements

### Requirement: Validación del enlace y pantalla de bienvenida
Al abrir la ruta `/prueba/{Token}`, la interfaz SHALL validar el token contra el backend antes de mostrar ningún contenido de la prueba. Si el token resulta válido, el backend SHALL además aprovisionar (o reutilizar) la cuenta del alumno y autenticarlo automáticamente, y la interfaz SHALL almacenar la sesión autenticada devuelta antes de presentar la pantalla de bienvenida con el título de la prueba, el nombre del candidato, el número de preguntas y el tiempo límite. Si el token corresponde a una prueba ya empezada y sin terminar, la interfaz SHALL omitir la pantalla de bienvenida y devolver al candidato directamente a las preguntas.

#### Scenario: Token válido muestra información de la prueba y autentica al alumno
- **WHEN** el candidato abre un enlace con un token que existe, no ha expirado y no tiene sesión previa
- **THEN** la interfaz recibe y almacena una sesión autenticada para el alumno, y muestra la pantalla de bienvenida con el título de la prueba, el nombre del candidato, el número de preguntas y el tiempo límite en minutos, junto con un botón para comenzar

#### Scenario: Token de una prueba ya empezada devuelve al candidato a las preguntas
- **WHEN** el candidato vuelve a abrir el enlace de una prueba cuya sesión sigue en curso
- **THEN** la interfaz SHALL NOT mostrar la pantalla de bienvenida ni pedir que pulse "Comenzar prueba"
- **AND** la interfaz muestra directamente la pantalla de preguntas, con el temporizador ya en marcha

#### Scenario: Token inválido o expirado bloquea el acceso
- **WHEN** el candidato abre un enlace con un token inexistente, un token expirado sin sesión empezada, o un token cuya prueba ya se envió
- **THEN** la interfaz muestra una pantalla de error con el mensaje devuelto por el backend y SHALL NOT mostrar la pantalla de bienvenida ni ninguna pregunta de la prueba, ni almacenar ninguna sesión autenticada

### Requirement: Continuidad sin fricción del aprovisionamiento
La interfaz SHALL completar el aprovisionamiento y auto-login del alumno de forma transparente al abrir el enlace, sin solicitar en ningún momento que el candidato introduzca usuario o contraseña durante el flujo de resolución de la prueba.

#### Scenario: Apertura del enlace no requiere credenciales
- **WHEN** el candidato abre el enlace de una invitación, sea la primera vez o una vez que ya tiene cuenta de una prueba anterior
- **THEN** la interfaz avanza directamente a la pantalla de bienvenida sin mostrar ningún formulario de login

### Requirement: Inicio del examen con temporizador visible
Al pulsar "Comenzar prueba" desde la pantalla de bienvenida, o al reanudar una prueba ya empezada, la interfaz SHALL iniciar o recuperar la sesión de la prueba contra el backend y SHALL mostrar un temporizador en cuenta atrás visible en todo momento durante la resolución. El temporizador SHALL inicializarse con el tiempo restante que devuelve el servidor, nunca con el tiempo límite completo de la prueba.

#### Scenario: Inicio de sesión arranca el temporizador
- **WHEN** el candidato pulsa "Comenzar prueba"
- **THEN** la interfaz solicita el inicio de sesión al backend, transiciona a la pantalla de preguntas y arranca un temporizador en cuenta atrás inicializado con el tiempo restante devuelto por el servidor

#### Scenario: El temporizador continúa donde estaba tras una recarga
- **GIVEN** una prueba de 60 minutos empezada hace 12 minutos
- **WHEN** el candidato recarga la página
- **THEN** el temporizador arranca en 48 minutos aproximadamente, y no vuelve a 60

#### Scenario: Reanudación sin tiempo restante
- **GIVEN** una prueba cuyo tiempo límite ya ha transcurrido por completo
- **WHEN** el candidato vuelve a abrir el enlace
- **THEN** la interfaz muestra el temporizador a cero y envía la prueba de inmediato con las respuestas guardadas, sin pedir confirmación

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
La interfaz SHALL enviar cada respuesta al backend en el momento en que el candidato la registra, sin esperar al envío final de la prueba: inmediatamente al seleccionar una opción en preguntas tipo test, y en preguntas abiertas mientras el candidato escribe, con un retardo que agrupe las pulsaciones, además de al perder el foco del campo de texto.

#### Scenario: Selección de una opción de test se guarda de inmediato
- **WHEN** el candidato selecciona una opción de respuesta en una pregunta tipo test
- **THEN** la interfaz envía esa respuesta al backend para la sesión activa sin requerir ninguna acción adicional del candidato

#### Scenario: Respuesta abierta se guarda al perder el foco
- **WHEN** el candidato termina de escribir en el área de texto de una pregunta abierta y el campo pierde el foco
- **THEN** la interfaz envía el contenido actual del campo al backend como respuesta guardada para esa pregunta

#### Scenario: Respuesta abierta se guarda mientras se escribe
- **GIVEN** un candidato escribiendo en el área de texto de una pregunta abierta, sin haber salido del campo
- **WHEN** deja de teclear durante el retardo establecido
- **THEN** la interfaz SHALL enviar el contenido actual del campo al backend, de forma que cerrar el navegador a continuación no pierda lo escrito

#### Scenario: Escribir sin pausa no lanza una llamada por pulsación
- **GIVEN** un candidato escribiendo de forma continua en una pregunta abierta
- **WHEN** encadena pulsaciones sin pausas mayores que el retardo
- **THEN** la interfaz SHALL agrupar esas pulsaciones en un solo envío, en lugar de llamar al backend una vez por carácter

#### Scenario: El plazo agotado detiene el autoguardado
- **GIVEN** un candidato escribiendo en una pregunta abierta cuando el plazo del examen ya ha vencido
- **WHEN** el autoguardado por escritura intenta enviar el contenido
- **THEN** el backend SHALL rechazarlo igual que rechaza cualquier otra escritura fuera de plazo, y la interfaz SHALL NOT tratar ese rechazo como pérdida de la respuesta

### Requirement: Auto-envío al agotarse el tiempo
La interfaz SHALL enviar automáticamente la prueba completa, con todas las respuestas registradas hasta ese momento, en el instante en que el temporizador alcanza cero, sin requerir confirmación del candidato. La interfaz SHALL NOT lanzar un segundo envío mientras haya uno en curso.

#### Scenario: El tiempo se agota durante la resolución
- **WHEN** el temporizador en cuenta atrás llega a cero mientras el candidato aún está respondiendo la prueba
- **THEN** la interfaz detiene el temporizador y envía automáticamente la prueba con el conjunto de respuestas disponible en ese momento, sin mostrar el diálogo de confirmación manual

#### Scenario: El tiempo se agota con un envío manual ya en vuelo
- **GIVEN** que el candidato ha pulsado "Finalizar" y su envío todavía no ha respondido
- **WHEN** el temporizador alcanza cero
- **THEN** la interfaz SHALL NOT lanzar un segundo envío
- **AND** el candidato ve el resultado del envío que ya estaba en curso

### Requirement: Envío manual antes de agotar el tiempo
La interfaz SHALL permitir al candidato finalizar y enviar la prueba manualmente en cualquier momento antes de que se agote el tiempo, mostrando previamente un diálogo de confirmación con el número de preguntas respondidas.

#### Scenario: Candidato solicita finalizar desde la última pregunta
- **WHEN** el candidato pulsa "Finalizar prueba" estando en la última pregunta y con tiempo restante
- **THEN** la interfaz muestra un diálogo de confirmación indicando cuántas preguntas de las totales han sido respondidas, antes de enviar nada al backend

#### Scenario: Candidato confirma el envío manual
- **WHEN** el candidato confirma el envío desde el diálogo de confirmación
- **THEN** la interfaz detiene el temporizador, envía el conjunto completo de respuestas al backend y transiciona a la pantalla de resultado final

### Requirement: Pantalla de confirmación con resultado final
Tras un envío exitoso (manual o automático), la interfaz SHALL mostrar una pantalla final sin permitir volver a las preguntas de la prueba. Cuando el resultado queda `Reviewed`, la pantalla SHALL mostrar el resultado obtenido y un aviso de que el detalle llegará también por email. Cuando el resultado queda `PendingReview`, la pantalla SHALL indicar que la prueba se ha recibido correctamente y que contiene preguntas que requieren corrección manual, informando de que recibirá el resultado por email cuando esté corregida, y NO SHALL mostrar puntuación, porcentaje, veredicto ni las respuestas correctas.

#### Scenario: Prueba sin abiertas pendientes muestra el resultado
- **WHEN** el envío de la prueba se completa correctamente y el resultado queda con estado `Reviewed`
- **THEN** la interfaz muestra si el candidato aprobó o no, el porcentaje de puntuación, los puntos obtenidos sobre el total, y un mensaje indicando que recibirá los resultados por email

#### Scenario: Prueba pendiente de corrección muestra únicamente el acuse
- **WHEN** el envío de la prueba se completa correctamente y el resultado queda con estado `PendingReview`
- **THEN** la interfaz muestra un mensaje de prueba recibida y pendiente de corrección manual, sin porcentaje, sin puntos, sin veredicto de aprobado o suspenso y sin ningún indicador visual de éxito o fracaso

#### Scenario: La respuesta de envío no revela el solucionario
- **WHEN** el candidato envía la prueba y el sistema responde con la confirmación
- **THEN** la respuesta del endpoint de envío NO SHALL incluir el texto de las respuestas correctas ni la evaluación de acierto por pregunta, con independencia del estado del resultado

### Requirement: Restauración de las respuestas al reanudar
Al reanudar una prueba en curso, la interfaz SHALL cargar en sus borradores las respuestas que el servidor devuelve como ya guardadas, de modo que el candidato vea marcado lo que respondió antes de la interrupción. La interfaz SHALL NOT enviar valores vacíos para las preguntas que ya tenían respuesta guardada.

#### Scenario: Las respuestas previas aparecen marcadas
- **GIVEN** una prueba en curso en la que el candidato ya respondió las tres primeras preguntas
- **WHEN** el candidato recarga la página y vuelve a la prueba
- **THEN** las tres respuestas aparecen seleccionadas o escritas en su pregunta correspondiente
- **AND** las preguntas no respondidas aparecen en blanco

#### Scenario: El envío tras una reanudación conserva el trabajo previo
- **GIVEN** una prueba reanudada con respuestas previas restauradas
- **WHEN** el candidato envía la prueba sin volver a tocar esas preguntas
- **THEN** el envío incluye las respuestas restauradas con su contenido original
- **AND** ninguna respuesta previamente guardada queda vacía tras el envío
