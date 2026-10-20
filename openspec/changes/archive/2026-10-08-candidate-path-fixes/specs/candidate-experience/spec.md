## MODIFIED Requirements

### Requirement: Validación del enlace y pantalla de bienvenida
Al abrir la ruta `/prueba/{Token}`, la interfaz SHALL validar el token contra el backend antes de mostrar ningún contenido de la prueba. Si el token resulta válido, el backend SHALL además aprovisionar (o reutilizar) la cuenta del alumno y autenticarlo automáticamente, y la interfaz SHALL almacenar la sesión autenticada devuelta antes de presentar la pantalla de bienvenida con el título de la prueba, el nombre del candidato, el número de preguntas y el tiempo límite. Si el token corresponde a una prueba ya empezada y sin terminar, la interfaz SHALL omitir la pantalla de bienvenida y devolver al candidato directamente a las preguntas.

Abrir el enlace SHALL NOT poner en marcha el tiempo de la prueba: la interfaz SHALL mostrar la bienvenida con los datos de la validación, sin solicitar la sesión de la prueba, que se solicita al pulsar "Comenzar prueba". La bienvenida SHALL avisar de que el tiempo empieza a contar al pulsar ese botón y de que cerrar la página después no lo detiene.

#### Scenario: Token válido muestra información de la prueba y autentica al alumno
- **WHEN** el candidato abre un enlace con un token que existe, no ha expirado y no tiene sesión previa
- **THEN** la interfaz recibe y almacena una sesión autenticada para el alumno, y muestra la pantalla de bienvenida con el título de la prueba, el nombre del candidato, el número de preguntas y el tiempo límite en minutos, junto con un botón para comenzar

#### Scenario: Abrir el enlace no consume tiempo de la prueba
- **GIVEN** un candidato que abre el enlace de una prueba de 60 minutos y cierra la página en la bienvenida, sin pulsar "Comenzar prueba"
- **WHEN** vuelve a abrir el enlace dos horas después, con la invitación todavía vigente
- **THEN** la interfaz muestra de nuevo la pantalla de bienvenida, y no las preguntas ni el resultado
- **AND** al pulsar "Comenzar prueba", el temporizador arranca en 60 minutos

#### Scenario: Token de una prueba ya empezada devuelve al candidato a las preguntas
- **WHEN** el candidato vuelve a abrir el enlace de una prueba cuya sesión sigue en curso
- **THEN** la interfaz SHALL NOT mostrar la pantalla de bienvenida ni pedir que pulse "Comenzar prueba"
- **AND** la interfaz muestra directamente la pantalla de preguntas, con el temporizador ya en marcha

#### Scenario: Token inválido o expirado bloquea el acceso
- **WHEN** el candidato abre un enlace con un token inexistente, un token expirado sin sesión empezada, o un token cuya prueba ya se envió
- **THEN** la interfaz muestra una pantalla de error con el mensaje devuelto por el backend y SHALL NOT mostrar la pantalla de bienvenida ni ninguna pregunta de la prueba, ni almacenar ninguna sesión autenticada

## ADDED Requirements

### Requirement: Límite visible en la respuesta abierta
La interfaz SHALL impedir que el área de texto de una pregunta abierta contenga más de 4000 caracteres, también al pegar texto, y SHALL mostrar cuántos caracteres lleva la respuesta respecto a ese máximo. Al llegar al máximo, la interfaz SHALL avisar de que el área no admite más texto.

#### Scenario: Contador de caracteres
- **WHEN** el candidato escribe una respuesta abierta de 120 caracteres
- **THEN** la interfaz muestra que lleva 120 de 4000 caracteres

#### Scenario: Texto pegado que supera el máximo
- **GIVEN** el área de texto vacía de una pregunta abierta
- **WHEN** el candidato pega un texto de 5000 caracteres
- **THEN** el área conserva los primeros 4000 caracteres
- **AND** la interfaz avisa de que se ha llegado al máximo
- **AND** el auto-guardado de esa respuesta no falla
