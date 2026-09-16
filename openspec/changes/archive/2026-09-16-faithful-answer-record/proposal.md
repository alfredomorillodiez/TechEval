# faithful-answer-record

Que la respuesta de un candidato quede registrada tal como fue: con su hora correcta, sin perder lo escrito, y conservando lo que se le preguntó

## Why

Tres defectos de la auditoría comparten una raíz: el registro de una respuesta no es fiel a lo que ocurrió.

**D5 — la hora está mal.** `UserAnswer.AnsweredAt` usa `DateTime.Now`; todo lo demás del modelo usa `DateTime.UtcNow`. En un servidor con horario español, la marca de la respuesta queda una o dos horas por delante de la marca de la sesión que la contiene. Una respuesta parece anterior al examen, o posterior a su cierre.

**D6 — se pierde lo escrito.** El área de texto de las preguntas abiertas solo guarda al perder el foco. Si el navegador se cierra, o la conexión cae, mientras el candidato escribe y no ha salido del campo, esa respuesta se pierde entera. Las opciones de test sí se guardan al pulsarlas.

**D7 — el histórico se reescribe.** La ficha de resultados muestra el enunciado **actual** de la pregunta, no el que vio el candidato: se lee de `ua.Question.Text`. Lo mismo con la opción elegida, la correcta y los puntos máximos. Corregir una errata cambia, hacia atrás, lo que consta que se preguntó en cada examen ya cerrado.

D7 es hoy alcanzable por el arreglo de D3: hasta entonces editar una pregunta ya usada fallaba con un 500, así que el problema no llegaba a darse.

El código ya resuelve así el mismo problema para la nota. `UserAnswer.AwardedPoints` congela los puntos en el envío, y su comentario explica por qué: recalcular contra `Question.Points` daría notas distintas si la pregunta se edita más tarde. Este cambio extiende ese patrón al resto de lo que la ficha muestra.

## What Changes

- `AnsweredAt` pasa a `UtcNow` en los dos sitios que lo fijan.
- El área de texto de las preguntas abiertas guarda también mientras se escribe, con un retardo, además de al perder el foco.
- `UserAnswer` guarda, en el momento del envío, el enunciado de la pregunta, el texto de la opción elegida, el de la correcta y los puntos máximos. Las fichas de resultados y la pantalla de corrección leen esa copia.
- Script SQL aditivo e idempotente para las columnas nuevas, con relleno de las filas ya existentes.

## Capabilities

- `exam-taking` — el registro de la respuesta y su hora
- `candidate-experience` — el autoguardado de la respuesta abierta
- `exam-results` — la ficha de resultados deja de cambiar cuando se edita una pregunta
- `open-question-review` — la pantalla de corrección muestra lo que se preguntó

## Impact

**Base de datos.** Cuatro columnas nuevas en `UserAnswers`, todas opcionales. El relleno de las filas ya existentes usa el texto de hoy, que es justo lo que esas fichas muestran ahora: no empeora nada, pero tampoco recupera lo que se preguntó entonces, que no está guardado en ninguna parte.

**Sin corte de servicio.** El cambio es aditivo. Una API antigua contra el esquema nuevo sigue funcionando; una API nueva contra el esquema antiguo, no.

**Tráfico.** El autoguardado por escritura aumenta las llamadas a `POST /api/exam/answer/{id}`. El retardo las agrupa.
