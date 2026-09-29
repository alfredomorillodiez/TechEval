## 1. Planificador

- [x] 1.1 Crear `DifficultyPlan`, `LevelShortage` y `DifficultyPlanner.Plan` en `src/TechEval.Application/Services/` con los tres pasos de design.md D1. Verificar que compila.
- [x] 1.2 Pruebas del planificador con los ejemplos de la spec `exam-management`: reparto exacto 30/50/20; mayor resto 33/33/34; empate 50/50/0 con 3; nivel al 0 %; porcentajes que no suman 100 o fuera de rango; faltan avanzadas (8/15/2, 20/30/50 → avanzado necesita 5, hay 2); faltan dos niveles (1/20/1, 30/40/30); sobran de otros niveles (50/1/0, 0/100/0 → faltante en intermedio). Verificar que pasan.

## 2. Banco y generación

- [x] 2.1 Añadir `CountByDifficultyAsync(categoryIds)` a `IQuestionRepository` y `QuestionRepository`, con los tres niveles siempre. Verificar con una prueba en memoria que no cuenta las preguntas dadas de baja y que devuelve 0 en un nivel vacío.
- [x] 2.2 Añadir `DifficultyPercentages` a `GenerateExamDto` como último parámetro opcional, y `LevelCountDto` (design.md D3). Verificar que compila sin tocar a los llamadores actuales.
- [x] 2.3 En `ExamService.GenerateAsync`, el camino con reparto de design.md D2: `400` con nivel único y reparto a la vez, disponibilidad, planificador, rechazo con un mensaje por nivel, selección por nivel y mezcla. Verificar con pruebas: el reparto de la prueba coincide con el plan; un nivel que no alcanza rechaza aunque sobren de otros; no se usan preguntas de otras categorías; el nivel único y la generación sin nivel siguen como hoy.
- [x] 2.4 Añadir `GET /api/questions/availability` en `QuestionsController`. Verificar en Swagger la respuesta con los tres niveles y que un token de evaluador recibe `403`.

## 3. Web

- [x] 3.1 Añadir a `ApiService` la disponibilidad por nivel, y hacer que `GenerateExamAsync` devuelva el código y el mensaje de error de la API. Verificar que compila.
- [x] 3.2 En `GenerateExam.razor`, el modo «Reparto por nivel» con porcentajes, vista previa calculada con `DifficultyPlanner`, marca de los niveles que no alcanzan, bloqueo si no suma 100 o si falta algún nivel, y el mensaje del servidor si rechaza (design.md D5). Verificar a mano los tres escenarios de la spec `admin-console` sobre la vista previa.
- [x] 3.3 En `ExamDetail.razor`, mostrar el reparto real por nivel con número y porcentaje. Verificar a mano con una prueba generada con reparto.

## 4. Documentación y cierre

- [x] 4.1 Actualizar `README.md` y `documentacion.md`: el reparto por nivel, el redondeo, el rechazo cuando un nivel no alcanza y el endpoint de disponibilidad. Verificar que los ejemplos del texto coinciden con los de la spec.
- [x] 4.2 Ejecutar la batería completa y `openspec validate difficulty-distribution --strict`. Verificar que pasan.
- [x] 4.3 Recorrido en local contra una base de prueba (nunca `TechEvalDb`): generar desde la Web una prueba con un reparto que se cumple, comprobar que la vista previa bloquea un reparto que un nivel no alcanza, y que el servidor rechaza ese mismo reparto si se envía directamente a la API. Verificar el reparto de la prueba creada contra la base de datos.
