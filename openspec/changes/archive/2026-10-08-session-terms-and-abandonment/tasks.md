## 1. Dominio y base de datos

- [x] 1.1 `ExamSession.TimeLimitMinutesSnapshot`, `PassingScoreSnapshot`, `TimeLimitFor` y `PassingScoreFor` (design.md D1). Verificar que compila y que `SchemaDriftTests` falla hasta que el guion los tenga.
- [x] 1.2 Las dos columnas en `scripts/create_database.sql` y el guion aditivo `scripts/add_session_terms_columns.sql`, idempotente y con vuelta atrás comentada. Verificar contra una base de prueba (nunca `TechEvalDb`): crea las dos columnas nulables, no toca filas y se reejecuta sin error; y que `SchemaDriftTests` pasa.

## 2. Congelar las condiciones

- [x] 2.1 Guardar los valores en `StartSessionAsync` y usarlos en el plazo, el tiempo restante, el envío, `ReviewWorkflow` y `ExamIntegrityService` (design.md D2). Verificar con pruebas: acortar y alargar el tiempo con una sesión en curso, sesión nueva con el valor nuevo, nota de corte cambiada entre dos envíos, nota cambiada durante la corrección, sesión sin valores (usa los de la prueba), reanudar no vuelve a copiar.

## 3. Sesiones abandonadas

- [x] 3.1 `IExamTokenRepository.GetInProgressAsync` y `SessionMaintenanceService` (design.md D3). Verificar con pruebas: vencida hace más de la gracia → `Abandoned` con respuestas intactas y sin resultado; vencida hace menos → no; en plazo → no; con resultado → no; el tiempo guardado manda sobre el de la prueba; el fallo de una sesión no impide marcar las demás.
- [x] 3.2 `SessionSweepOptions` con `Normalize` y `AbandonedSessionWorker`, registrados en `Program.cs`; entradas en `appsettings.json` (design.md D3, D4). Verificar con pruebas de `Normalize` (defecto, negativo, 0) y arrancando la API: el trabajo marca una sesión vencida de la base de prueba.
- [x] 3.3 `ExamAbandonedException` y los rechazos de validación, autoguardado y envío (design.md D5). Verificar con pruebas: el mensaje de la validación no dice «completada»; el autoguardado y el envío dan `409`; el envío no crea resultado; el envío idempotente de una sesión con resultado sigue funcionando.

## 4. Lo que ve el administrador

- [x] 4.1 `ExamSummaryDto.AbandonedCount`, la insignia de `ExamList` y el aviso de `ExamDetail` (design.md D6). Verificar con una prueba del servicio y en el navegador con una sesión abandonada de la base de prueba.

## 5. Cierre

- [x] 5.1 Documentar el guion, `Sessions:AbandonAfterHours` y `Sessions:SweepMinutes` en `documentacion.md` y `README.md`, y avisar de que la primera ejecución marca las sesiones antiguas. Verificar con una búsqueda.
- [x] 5.2 Ejecutar la batería completa y `openspec validate session-terms-and-abandonment --strict`. Verificar que pasan.
- [x] 5.3 Recorrido en local contra una base de prueba (nunca `TechEvalDb`) y sin credenciales de correo: editar el tiempo con un candidato en curso y comprobar en la API y en la base que su plazo no cambia; dejar una sesión vencida y comprobar que el trabajo la marca y que `validate`, `answer` y `submit` responden lo especificado.
