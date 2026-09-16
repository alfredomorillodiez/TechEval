## 1. La hora de la respuesta (D5)

- [x] 1.1 Cambiar a `UtcNow` el valor por defecto de `UserAnswer.AnsweredAt` y la asignación de `ExamTokenService`. Verificar con una búsqueda que no queda ningún `DateTime.Now` en `src/`.
- [x] 1.2 Añadir una prueba que compruebe que `AnsweredAt` no queda por delante de `StartedAt`. Verificar que pasa.

## 2. Esquema de la copia (D7)

- [x] 2.1 Añadir a `UserAnswer` las cuatro propiedades de copia. Verificar que son opcionales, para que las filas antiguas sigan cargando.
- [x] 2.2 Configurarlas en `UserAnswerConfiguration` con sus longitudes. Verificar que el enunciado admite la misma longitud que `Question.Text`.
- [x] 2.3 Escribir `scripts/add_answer_snapshot_columns.sql`, aditivo e idempotente, con el relleno de las filas existentes. Verificar que reejecutarlo no da error.
- [x] 2.4 Ejecutar el guion contra la base de datos local. Verificar que las columnas existen y que las filas antiguas quedan rellenas.

## 3. Escritura de la copia (D7)

- [x] 3.1 Escribir la copia en `SubmitExamAsync`, dentro de la transacción, junto a `AwardedPoints`. Verificar que cubre tanto las preguntas de test como las abiertas.
- [x] 3.2 Comprobar que el envío tardío, que reutiliza la respuesta guardada, también escribe la copia. Verificar leyendo el método.
- [x] 3.3 Añadir pruebas de la escritura de la copia para los dos tipos de pregunta. Verificar que pasan.

## 4. Lectura de la copia (D7)

- [x] 4.1 Cambiar `ResultService.GetDetailAsync` para leer enunciado, opción elegida, opción correcta y puntos de la copia. Verificar que recurre a la pregunta actual solo si la copia está vacía.
- [x] 4.2 Cambiar `OpenQuestionReviewService.GetDetailAsync` para leer enunciado y puntos de la copia, dejando `SampleAnswer` en la pregunta actual. Verificar leyendo el método.
- [x] 4.3 Añadir la prueba que da nombre al defecto: editar la pregunta después del envío y comprobar que la ficha no cambia. Verificar que falla antes del arreglo.

## 5. Autoguardado mientras se escribe (D6)

- [x] 5.1 Añadir en `TakeExam.razor` el temporizador de retardo sobre `@oninput`, conservando `@onblur`. Verificar que el temporizador se libera al salir de la página.
- [ ] 5.2 Comprobar que escribir sin pausa produce un solo envío y no uno por carácter. Verificar en el navegador con la pestaña de red.
- [x] 5.3 Comprobar que un `409` por plazo vencido no borra lo escrito en pantalla. Verificar leyendo el manejador.

## 6. Cierre

- [x] 6.1 Ejecutar `dotnet test` completo. Verificar que las 124 pruebas anteriores siguen en verde.
- [x] 6.2 Reconstruir y arrancar la API. Comprobar contra la API real que editar una pregunta ya respondida no cambia la ficha del resultado. Verificar el antes y el después.
- [ ] 6.3 Recorrer una prueba en el navegador: escribir en una abierta, cerrar la pestaña sin salir del campo, volver a entrar. Verificar que el texto sigue ahí.
- [x] 6.4 Comprobar en la base de datos que `AnsweredAt` y `StartedAt` son comparables. Verificar con una consulta.
