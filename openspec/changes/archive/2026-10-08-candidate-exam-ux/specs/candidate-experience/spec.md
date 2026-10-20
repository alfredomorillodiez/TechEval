## ADDED Requirements

### Requirement: Progreso real de la prueba
La interfaz SHALL mostrar al candidato cuántas preguntas ha respondido sobre el total, con el formato «N de M respondidas», y la barra de progreso SHALL avanzar con ese número, no con la posición de la pregunta en pantalla. Una pregunta cuenta como respondida cuando tiene una opción elegida o un texto no vacío.

#### Scenario: Responder avanza el progreso
- **GIVEN** una prueba de 30 preguntas con 11 respondidas
- **WHEN** el candidato responde una más
- **THEN** el encabezado muestra «12 de 30 respondidas» y la barra refleja 12/30

#### Scenario: Cambiar de pregunta no mueve el progreso
- **GIVEN** una prueba con 5 de 30 preguntas respondidas, y el candidato en la pregunta 20
- **WHEN** el candidato navega a la pregunta 1
- **THEN** el encabezado sigue mostrando «5 de 30 respondidas»

#### Scenario: Una abierta en blanco no cuenta
- **GIVEN** una pregunta abierta cuyo texto es una cadena vacía
- **WHEN** se calcula el progreso
- **THEN** esa pregunta no cuenta como respondida

### Requirement: Marcar preguntas para revisar
La interfaz SHALL permitir al candidato marcar y desmarcar cualquier pregunta para revisarla después. El mapa de preguntas SHALL distinguir las marcadas mediante un símbolo además del color. La marca SHALL conservarse en el navegador durante la sesión de la prueba, incluso tras una recarga. La marca SHALL NOT enviarse al servidor ni influir en la nota.

#### Scenario: Marcar una pregunta
- **WHEN** el candidato pulsa «Marcar para revisar» en una pregunta
- **THEN** el botón pasa a «Quitar marca» y el mapa muestra la pregunta con el símbolo de marca

#### Scenario: La marca sobrevive a una recarga
- **GIVEN** una pregunta marcada en una sesión en curso
- **WHEN** el candidato recarga la página y vuelve a la prueba
- **THEN** la pregunta sigue marcada

#### Scenario: Una pregunta marcada y respondida se ve las dos cosas
- **GIVEN** una pregunta respondida y marcada
- **WHEN** el candidato mira el mapa
- **THEN** el mapa indica a la vez que está respondida y que está marcada

#### Scenario: El almacén del navegador no está disponible
- **GIVEN** un navegador que rechaza el almacenamiento local
- **WHEN** el candidato marca una pregunta
- **THEN** la marca funciona mientras la página siga abierta y la prueba no falla

### Requirement: «Finalizar» siempre visible
La interfaz SHALL mostrar el botón «Finalizar prueba» en el encabezado fijo, en todas las preguntas. El diálogo de confirmación SHALL indicar cuántas preguntas están sin responder y cuántas están marcadas para revisar.

#### Scenario: Finalizar desde cualquier pregunta
- **GIVEN** el candidato en la pregunta 5 de 30
- **WHEN** pulsa «Finalizar prueba» del encabezado
- **THEN** la interfaz muestra el diálogo de confirmación sin cambiar de pregunta

#### Scenario: El diálogo avisa de lo pendiente
- **GIVEN** 12 preguntas respondidas de 30 y 3 marcadas
- **WHEN** se abre el diálogo de confirmación
- **THEN** el diálogo indica «12 de 30 respondidas», 18 sin responder y 3 marcadas para revisar

### Requirement: Estado del guardado y aviso de falta de conexión
La interfaz SHALL mostrar el estado del autoguardado en el encabezado: «Guardando…» mientras haya una escritura en vuelo, «Guardado» con la hora del último guardado correcto, o «Sin guardar» si hay respuestas que no han llegado al servidor. Un autoguardado que falla por red, por `429` o por un error del servidor SHALL reintentarse solo, a intervalos de unos segundos y cuando el navegador informe de que vuelve la conexión. Un rechazo del servidor que un reintento no cambia (`400`, `403`, `409`) SHALL NOT reintentarse. Mientras el navegador no tenga conexión, o haya respuestas sin guardar por un fallo, la interfaz SHALL mostrar un aviso fijo que diga que las respuestas no se están guardando y que no se cierre la página.

#### Scenario: Guardado correcto
- **WHEN** el candidato elige una opción y el servidor responde `200`
- **THEN** el indicador muestra «Guardado» con la hora

#### Scenario: Se corta la red
- **GIVEN** una prueba en curso
- **WHEN** el navegador pierde la conexión
- **THEN** aparece el aviso fijo de falta de conexión
- **AND** el candidato puede seguir respondiendo

#### Scenario: La red vuelve
- **GIVEN** respuestas sin guardar por un corte de red
- **WHEN** el navegador recupera la conexión
- **THEN** la interfaz reintenta guardar las respuestas pendientes
- **AND** al lograrlo, el aviso desaparece y el indicador muestra «Guardado»

#### Scenario: Un guardado falla con el navegador en línea
- **GIVEN** un autoguardado que recibe un `500`
- **WHEN** falla
- **THEN** el indicador muestra «Sin guardar» y aparece el aviso
- **AND** la interfaz reintenta a los pocos segundos

#### Scenario: Un rechazo definitivo no se reintenta
- **GIVEN** un autoguardado que recibe un `409` porque el plazo venció
- **WHEN** el servidor lo rechaza
- **THEN** la interfaz SHALL NOT reintentarlo
- **AND** el borrador sigue en pantalla

#### Scenario: Una respuesta nueva sustituye a la pendiente
- **GIVEN** una respuesta de la pregunta 3 pendiente de guardar por un fallo
- **WHEN** el candidato la cambia y el guardado tiene éxito
- **THEN** se envía solo el contenido más reciente de esa pregunta

### Requirement: Un envío fallido no cierra la prueba
Si el envío de la prueba falla, la interfaz SHALL permanecer en la prueba, conservar todas las respuestas, mostrar un aviso que diga que la prueba no se ha enviado y ofrecer reintentarlo. Cuando el envío lo dispara el temporizador al llegar a cero, la interfaz SHALL reintentarlo sola cada pocos segundos. Si el servidor rechaza el envío con un mensaje, la interfaz SHALL mostrarlo. La interfaz SHALL NOT mostrar la pantalla de resultado sin un acuse del servidor.

#### Scenario: Falla el envío manual
- **GIVEN** un candidato que confirma el envío y la red falla
- **WHEN** la petición no llega
- **THEN** la interfaz muestra que la prueba no se ha enviado, conserva las respuestas y deja reintentar
- **AND** no aparece la pantalla de resultado

#### Scenario: Falla el auto-envío por tiempo agotado
- **GIVEN** el temporizador a cero y la red caída
- **WHEN** el auto-envío falla
- **THEN** la interfaz lo reintenta sola cada pocos segundos hasta que el servidor responde

#### Scenario: El servidor rechaza el envío con un mensaje
- **GIVEN** un envío que el servidor rechaza con `400` y un mensaje
- **WHEN** la respuesta llega
- **THEN** la interfaz muestra ese mensaje y deja al candidato corregir y reenviar
