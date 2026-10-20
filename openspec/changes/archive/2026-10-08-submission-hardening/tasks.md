## 1. Servidor

- [x] 1.1 Capturar el fallo del correo en `SubmitExamAsync`, en las dos ramas, con un método privado y sin capturar `OperationCanceledException` (design.md D1). Verificar con pruebas: con el servicio de correo lanzando, una prueba de test devuelve el acuse con cifras; una prueba con abiertas devuelve el acuse pendiente sin cifras; en ambas el resultado y la sesión cerrada siguen persistidos; con `OperationCanceledException` la excepción llega a quien llama.
- [x] 1.2 Crear `ExamAlreadySubmittedException` y comprobar en `SaveDraftAnswerAsync`, entre la propiedad y el plazo, que la sesión no está enviada (design.md D2). Verificar con pruebas: sesión con resultado → `ExamAlreadySubmittedException` sin escribir nada; sesión no `InProgress` → lo mismo; sesión ajena y enviada → `SessionAccessDeniedException`; sesión enviada y fuera de plazo con texto de 4001 → `ExamAlreadySubmittedException`; sesión en curso → guarda como antes.
- [x] 1.3 Hacer que `BaseRepository.AddAsync` desacople la entidad si `SaveChangesAsync` falla, y que la inserción de `SaveDraftAnswerAsync` relea y actualice si falla y la fila ya existe (design.md D3). Verificar con pruebas: la inserción falla y la relectura encuentra la fila → se actualiza con el contenido de la segunda petición y no se lanza nada; la inserción falla y la relectura no encuentra nada → se relanza el fallo original; la inserción falla con `OperationCanceledException` → se relanza.
- [x] 1.4 Declarar en `UserAnswerConfiguration` el índice único `IX_UserAnswers_ExamSessionId_QuestionId` y el índice `IX_UserAnswers_QuestionId` (design.md D3). Verificar con una prueba que el modelo de EF tiene un índice único sobre esas dos propiedades y otro sobre `QuestionId`, con esos nombres.

## 2. Base de datos

- [x] 2.1 Añadir los dos índices a `scripts/create_database.sql`, después del `CREATE TABLE dbo.UserAnswers`. Verificar con una prueba de deriva que el guion contiene los dos nombres de índice del modelo.
- [x] 2.2 Crear `scripts/add_useranswers_unique_index.sql` según design.md D3: idempotente, con comprobación de duplicados antes del índice único, que se detiene sin borrar nada, y con bloque de vuelta atrás comentado. Verificar contra una base de prueba (nunca `TechEvalDb`): sin duplicados crea los dos índices y no toca filas; al reejecutar no falla; con dos filas duplicadas termina con error, lista la pareja y no crea el índice ni borra nada.

## 3. Documentación y cierre

- [x] 3.1 Citar `add_useranswers_unique_index.sql` en `documentacion.md` y `README.md`, donde se listan los guiones aditivos. Verificar con una búsqueda que aparece junto a los demás.
- [x] 3.2 Ejecutar la batería completa y `openspec validate submission-hardening --strict`. Verificar que pasan.
- [x] 3.3 Recorrido en local contra una base de prueba (nunca `TechEvalDb`) y sin credenciales de correo. Comprobar en la base de datos, además de en la API: el envío responde `200` con el correo fallando y deja `ExamResults` y la sesión `Completed`; un `answer` posterior da `409` y `UserAnswers` no cambia; dos `answer` simultáneos de la misma pregunta dejan una sola fila; una inserción directa de una segunda fila para la misma pareja falla por el índice.
