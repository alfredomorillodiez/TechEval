## 1. Acceso a la API

- [x] 1.1 `ApiService.SaveAnswerAsync` devuelve `SaveOutcome` (`Saved`, `Rejected`, `Failed`) y nunca lanza (design.md D1). Verificar que la Web compila sin avisos nuevos.
- [x] 1.2 `ApiService.SubmitExamAsync` devuelve `(Receipt, StatusCode, Error)` con `SendAsync` (design.md D6). Verificar que la Web compila y que no queda ningún otro uso del método anterior.

## 2. Pantalla de la prueba

- [x] 2.1 Un único `IsAnswered(draft)`, progreso «N de M respondidas» en el encabezado y barra basada en ese número (design.md D5). Verificar en el navegador que al responder sube el número y que navegar no lo mueve.
- [x] 2.2 Marcar para revisar: botón por pregunta, símbolo en el mapa, almacén `examUi.getFlags/setFlags` con `try/catch` (design.md D4). Verificar en el navegador que la marca sobrevive a una recarga y que sin `localStorage` no rompe nada.
- [x] 2.3 «Finalizar prueba» en el encabezado fijo y diálogo con respondidas, sin responder y marcadas (design.md D5). Verificar en el navegador desde la pregunta 1.
- [x] 2.4 Estado del guardado (`_unsaved`, `_savesInFlight`, `_lastSavedAt`), reintento cada 5 s desde el temporizador existente y aviso fijo de falta de conexión con `examUi.registerConnection` (design.md D2, D3). Verificar en el navegador con un corte de red simulado: aparece el aviso, el indicador pasa a «Sin guardar», al volver la red guarda y todo se apaga; un `409` no se reintenta.
- [x] 2.5 Un envío fallido vuelve a la prueba con aviso y reintento, y el auto-envío por tiempo se reintenta solo (design.md D6). Verificar en el navegador con la API parada: no aparece la pantalla de resultado, las respuestas se conservan y, al levantar la API, el reintento envía.

## 3. Cierre

- [x] 3.1 Actualizar `documentacion.md` y `README.md` si describen la pantalla de la prueba. Verificar con una búsqueda.
- [x] 3.2 Ejecutar la batería completa y `openspec validate candidate-exam-ux --strict`. Verificar que pasan.
- [x] 3.3 Recorrido en el navegador integrado contra una base de prueba (nunca `TechEvalDb`), sin credenciales de correo. Comprobar también en la base de datos que lo guardado coincide con lo que dice el indicador.
