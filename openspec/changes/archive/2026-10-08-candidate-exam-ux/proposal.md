## Why

Mientras el candidato resuelve la prueba, la pantalla le oculta tres cosas que necesita y le da por bueno algo que puede haber fallado.

- **Progreso.** La barra y el contador del encabezado muestran en qué pregunta está, no cuántas ha respondido. Para saberlo tiene que abrir el diálogo de «Finalizar». No puede marcar una pregunta dudosa para volver a ella: el mapa solo distingue «respondida» de «no respondida».
- **Finalizar.** El botón solo existe en la última pregunta. Quien quiere cerrar la prueba desde la pregunta 5 tiene que recorrer las demás.
- **Guardado.** `SaveAnswerAsync` ignora cualquier respuesta del servidor y no captura el fallo de red. Un guardado que falla no se ve: el candidato cree que su texto está a salvo, y el envío final sí lleva todo, pero si la red sigue caída ese envío también falla.
- **Envío fallido.** `SubmitExamAsync` devuelve `null` si falla, y la pantalla pasa a «completada» con `_result` nulo: la página se rompe con una excepción y el candidato no sabe si su prueba llegó.

## What Changes

- **Progreso real.** El encabezado muestra «12 de 30 respondidas» y la barra avanza con las respuestas, no con la posición. Una pregunta cuenta como respondida si tiene opción elegida o texto no vacío.
- **Marcar para revisar.** Un botón en cada pregunta la marca o la desmarca. El mapa de preguntas la distingue (color y símbolo, no solo color) y el diálogo de finalizar avisa de cuántas quedan marcadas. La marca vive en el navegador, por sesión: sobrevive a una recarga y no viaja al servidor ni cuenta en la nota.
- **«Finalizar» siempre visible.** El botón está en el encabezado fijo, en todas las preguntas. El diálogo de confirmación dice cuántas están sin responder y cuántas marcadas.
- **Estado del guardado.** Un indicador en el encabezado dice «Guardando…», «Guardado» (con la hora) o «Sin guardar». Un autoguardado que falla por red, por `429` o por error del servidor se reintenta solo, cada pocos segundos y al volver la conexión. Un rechazo del servidor que un reintento no cambia (`400`, `403`, `409`) no se reintenta, y el indicador lo dice.
- **Aviso de falta de conexión.** Si el navegador informa de que no hay red, o un guardado falla, aparece un aviso fijo: «Sin conexión. Tus respuestas no se están guardando. No cierres la página.» Desaparece al guardar de nuevo.
- **Un envío fallido no cierra la prueba.** Si el envío falla, la pantalla sigue en la prueba, con un aviso claro y el botón para reintentar. El auto-envío por tiempo agotado se reintenta solo. Un `400` por longitud muestra el mensaje del servidor.

Fuera de alcance:

- Guardar las marcas en el servidor.
- Enviar las respuestas pendientes por una conexión nueva si el candidato cierra la página: el navegador no lo garantiza.
- El resto de pantallas con errores mal mostrados (cambio siguiente).

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `candidate-experience`: progreso real, marcar para revisar, «Finalizar» siempre visible, estado del guardado, aviso de falta de conexión y envío fallido sin cierre.

## Impact

- **Web**: `TakeExam.razor`, `ApiService` (`SaveAnswerAsync` devuelve un resultado, `SubmitExamAsync` devuelve código y mensaje), `wwwroot/js` (un script nuevo con los eventos de conexión y el almacén de marcas) e `index.html`.
- **API y dominio**: sin cambios.
- **Pruebas**: la Web no tiene proyecto de pruebas. La verificación es un recorrido en el navegador integrado contra una base de prueba, incluido un corte de red simulado.
