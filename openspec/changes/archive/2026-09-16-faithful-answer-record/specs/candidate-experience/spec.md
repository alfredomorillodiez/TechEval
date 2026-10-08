## MODIFIED Requirements

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
