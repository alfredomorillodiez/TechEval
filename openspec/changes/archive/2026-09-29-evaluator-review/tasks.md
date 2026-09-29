## 1. Esquema y dominio

- [x] 1.1 Crear la entidad `ExamEvaluator` (design.md D1) y añadir `ReservedByUserId`, `ReservedUntil` y la navegación `ReservedByUser` a `ExamResult`. Configurar en EF la clave compuesta, las FK (cascada solo desde `Exams`) y un índice por `UserId`. Verificar que compila.
- [x] 1.2 Crear `scripts/add_evaluator_columns.sql`, idempotente, con la tabla, las dos columnas, sus FK e índices, y el SQL para quitarlos en la cabecera. Verificar sobre una base de prueba nueva (nunca sobre `TechEvalDb`, y con una copia del guion sin su `USE`) que se ejecuta dos veces sin error y que los resultados existentes quedan sin reserva.
- [x] 1.3 Llevar la tabla y las columnas a `scripts/create_database.sql`, con `ExamEvaluators` en la lista de `DROP` antes que `Exams` y `Users`. Verificar que `SchemaDriftTests` pasa y que el guion completo se ejecuta dos veces seguidas sobre una base de prueba.

## 2. Reserva

- [x] 2.1 Añadir a `IExamResultRepository` `TryReserveAsync`, `TryReleaseForSubmitAsync`, `ReleaseAsync` y `ReleaseAllOfAsync`, con `ExecuteUpdateAsync` en la base relacional y la comprobación equivalente en memoria (design.md D2). Verificar que compila.
- [x] 2.2 Pruebas de la reserva en memoria: tomar un resultado libre; `409` con reserva ajena vigente; tomar una caducada; renovar; liberar la propia; el administrador libera una ajena; no se reserva un resultado corregido. Verificar que pasan.

## 3. Flujo de corrección compartido

- [x] 3.1 Extraer de `OpenQuestionReviewService` a `ReviewWorkflow` la construcción de las respuestas corregibles y la aplicación de la corrección, con la liberación condicional dentro de la transacción (design.md D3). Verificar que `OpenQuestionReviewServiceTests` y `TransactionalWritesTests` siguen pasando sin cambiar sus expectativas.
- [x] 3.2 Hacer que el detalle del administrador tome la reserva y que su envío exija tenerla o que esté libre. Añadir `POST` y `DELETE /api/review/{resultId}/reservation` en `ReviewController`, con la liberación forzada del administrador. Verificar con pruebas el `409` al administrador cuando un evaluador tiene la reserva.

## 4. Asignación

- [x] 4.1 Crear el servicio de asignación: listar, asignar (solo evaluadores activos, sin duplicar) y quitar. Añadir los tres endpoints de design.md D1 en `ExamsController`. Verificar con pruebas cada escenario del requisito de `exam-management`.
- [x] 4.2 En `UserManagementService.ChangeRoleAsync`, al dejar el rol `Evaluador`, borrar sus asignaciones y liberar sus reservas en la misma transacción (design.md D10). Verificar con pruebas el ascenso a administrador y que la desactivación conserva las asignaciones.

## 5. Evaluación a ciegas

- [x] 5.1 Crear `CandidateAlias` y los DTO del evaluador de design.md D5, con `DateOnly` en las fechas. Verificar que ningún tipo tiene propiedades de nombre ni de email del candidato.
- [x] 5.2 Añadir `GetForEvaluatorAsync` y `GetPendingForEvaluatorAsync` al repositorio con el filtro de design.md D4. Verificar con pruebas: prueba no asignada, resultado propio por `UserId` y por email, e inexistente devuelven nulo.
- [x] 5.3 Crear `EvaluationService`: cola, detalle con reserva, renovación, liberación, envío con `ReviewWorkflow` y el evaluador en `ReviewedByUserId`, e historial por `ReviewedByUserId` sin depender de la asignación. Verificar con pruebas los escenarios de la spec `evaluator-review` sobre cola, detalle, envío e historial.
- [x] 5.4 Añadir `GetEvaluatorReportAsync` a `ExamIntegrityService`, con `ElapsedSeconds` y `OccurredAt` nulo (design.md D6), y el `404` sin asignación. Verificar con una prueba el ejemplo de la spec: 750 segundos y ninguna hora.
- [x] 5.5 Prueba de ceguera: con un candidato de nombre y email conocidos, serializar la respuesta de cada operación del evaluador (cola, detalle, envío, señales, historial y su detalle) y comprobar que ni el nombre ni el email aparecen, y que no hay horas del reloj. Verificar que pasa.

## 6. API

- [x] 6.1 Añadir la política `Evaluacion` a `Policies.cs` y crear `EvaluationController` con los endpoints de design.md D7. Registrar los servicios. Verificar que compila.
- [x] 6.2 Ampliar `AuthorizationPolicyTests`: `Evaluacion` solo la pasa `Evaluador`; `EvaluationController` exige `Evaluacion`. Verificar que pasan.
- [x] 6.3 Añadir `ReviewedByName` y `ReviewedAt` a `ExamResultDto`, y `ExamsWithoutEvaluator` a `DashboardStatsDto` (design.md D9). Verificar con pruebas el corrector en el detalle y la lista de pruebas sin evaluador.

## 7. Web

- [x] 7.1 Extraer `Shared/ReviewForm.razor` de `ReviewResult.razor` y usarlo en la pantalla del administrador. Añadir la renovación periódica, «Cancelar» y el aviso de reserva ajena. Verificar a mano que la corrección del administrador sigue igual.
- [x] 7.2 Sustituir `Pages/Evaluator/Home.razor` por `Queue.razor` en `/evaluacion` y crear `Review.razor` en `/evaluacion/{resultId}` con `ReviewForm` y el panel de señales. Verificar a mano una corrección completa como evaluador.
- [x] 7.3 Crear `History.razor` y `HistoryDetail.razor`. Añadir «Historial» a la barra lateral del evaluador y la guarda de acceso en todas las páginas de `/evaluacion`. Verificar a mano.
- [x] 7.4 Hacer que `IntegrityPanel.razor` muestre `mm:ss desde el inicio` cuando no hay `OccurredAt`. Verificar que el panel del administrador sigue mostrando la hora.
- [x] 7.5 Añadir la tarjeta de evaluadores a `ExamDetail.razor`, el aviso al dashboard, y la reserva con «Liberar» a `PendingReviewList.razor`. Mostrar en `ResultDetail.razor` quién corrigió. Verificar a mano cada escenario de la spec `admin-console`.

## 8. Documentación y cierre

- [x] 8.1 Actualizar `README.md` y `documentacion.md`: la matriz de roles, la asignación, la ceguera y sus límites (lo que escribe el candidato; pruebas de un solo candidato), la reserva, el historial y el guion nuevo. Verificar que la matriz coincide con `Policies.cs`.
- [x] 8.2 Ejecutar la batería completa y `openspec validate evaluator-review --strict`. Verificar que pasan.
- [x] 8.3 Recorrido de extremo a extremo en local, con correo solo hacia un receptor local y direcciones `@example.test`: asignar un evaluador, que corrija a ciegas, que otro corrector reciba `409` mientras dura la reserva, que el historial siga tras quitar la asignación, y dos aperturas simultáneas contra SQL Server de las que solo una obtiene la reserva. Verificar cada paso contra la base de datos.
