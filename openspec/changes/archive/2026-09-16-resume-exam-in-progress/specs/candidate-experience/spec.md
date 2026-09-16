## MODIFIED Requirements

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

## ADDED Requirements

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
