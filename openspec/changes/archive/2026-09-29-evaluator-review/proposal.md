## Why

Desde `user-roles` existe el rol `Evaluador`, pero no puede hacer nada: entra y ve una bienvenida sin datos. Toda la corrección de preguntas abiertas sigue en manos del administrador, que además ve la identidad de cada candidato mientras le pone nota.

Este cambio da al evaluador su trabajo. Corrige las pruebas que el administrador le asigna, sin saber de quién son, y consulta después lo que corrigió. Con varios correctores, hace falta además que dos personas no corrijan el mismo resultado a la vez.

## What Changes

- **Asignación por prueba.** El administrador asigna y quita evaluadores desde el detalle de una prueba. Una prueba puede tener varios. Solo se asignan usuarios con rol `Evaluador` y activos. Si una prueba no tiene evaluador, sus resultados solo los corrige el administrador, y el dashboard avisa de esas pruebas.
- **Cola del evaluador.** `/evaluacion` deja de ser una bienvenida vacía y pasa a ser la cola de resultados pendientes de las pruebas asignadas, del más antiguo al más reciente.
- **Corrección a ciegas.** El evaluador ve el enunciado, la respuesta, la referencia y las señales de integridad, pero nunca el nombre ni el email del candidato. El servidor no los envía: la cola, el detalle, la respuesta del envío y el historial usan DTO propios, sin esos campos. El candidato aparece con un alias estable por resultado. Las fechas llegan sin hora, y las señales de integridad llevan el tiempo desde el inicio de la prueba en lugar de la hora del reloj.
- **Comprobación en el servidor.** La asignación se comprueba en la cola, en el detalle, en el envío y en las señales. Un resultado de una prueba no asignada responde `404`, como si no existiera. Un evaluador no ve nunca un resultado suyo como candidato.
- **Reserva de 30 minutos.** Abrir una corrección reserva el resultado para quien la abre. Mientras dura la reserva, nadie más puede abrir ni enviar esa corrección. La pantalla la renueva mientras sigue abierta. Enviar la corrección o cancelarla la libera. La reserva vale también para el administrador, que además puede liberar la reserva de otro.
- **Historial propio.** `/evaluacion/historial` lista los resultados que corrigió el evaluador, a ciegas, con un detalle de solo lectura de sus puntos y comentarios. Lo sigue viendo aunque pierda la asignación de la prueba.
- **Cambio de rol.** Cuando un evaluador pasa a administrador, sus asignaciones se borran y sus reservas se liberan.
- **Trazabilidad para el administrador.** El detalle de un resultado muestra quién lo corrigió.

Fuera de alcance, y anotado como trabajo posterior:

- Avisar por correo al evaluador cuando entra un resultado en su cola.
- Repartir los resultados de forma automática entre los evaluadores de una prueba.
- Una segunda corrección, o la revisión por el administrador de la nota que puso un evaluador.
- Ocultar la identidad al administrador.

## Capabilities

### New Capabilities

- `evaluator-review`: la corrección por parte del evaluador — cola de las pruebas asignadas, detalle y envío a ciegas, señales de integridad sin hora del reloj, historial propio y su interfaz en `/evaluacion`.

### Modified Capabilities

- `exam-management`: se añade la asignación de evaluadores a una prueba.
- `open-question-review`: se añade la reserva de un resultado mientras alguien lo corrige, que aplica también a la cola y a la corrección del administrador.
- `admin-console`: la bienvenida vacía del evaluador se retira; la barra lateral del evaluador gana el historial; el detalle de la prueba gestiona sus evaluadores; el dashboard avisa de las pruebas con preguntas abiertas sin evaluador; la cola del administrador muestra las reservas y permite liberarlas.
- `authentication`: se añade la autorización por rol `Evaluador` en los endpoints de evaluación, y el escenario del evaluador en los endpoints de gestión deja de remitir a un cambio posterior.
- `user-management`: el paso de evaluador a administrador borra sus asignaciones y libera sus reservas.
- `exam-results`: el detalle de un resultado corregido muestra quién lo corrigió.
- `deployment-ops`: guion aditivo para la tabla de asignaciones y las columnas de la reserva.

## Impact

- **Dominio**: nueva entidad `ExamEvaluator` (`ExamId`, `UserId`, `AssignedAt`, `AssignedByUserId`). `ExamResult` gana `ReservedByUserId` y `ReservedUntil`.
- **Base de datos**: nueva tabla `ExamEvaluators` y dos columnas en `ExamResults`. Cambian `scripts/create_database.sql` y un guion aditivo nuevo, `scripts/add_evaluator_columns.sql`.
- **Aplicación**: nuevo servicio de evaluación (cola, detalle, envío, historial y señales a ciegas). La lógica de validación y cierre de la corrección se comparte con `OpenQuestionReviewService`, que gana la reserva. Nuevo servicio o métodos para asignar evaluadores. `UserManagementService` limpia asignaciones y reservas al cambiar el rol. `ResultService` expone el nombre del corrector.
- **API**: nuevo `EvaluationController` en `api/evaluation` con la política nueva `Evaluacion` (rol `Evaluador`). Endpoints de asignación en `ExamsController`. Endpoints de reserva en `ReviewController`. Nada se añade a `ResultsController`, así que el problema de combinar `[Authorize]` de clase y de método no aparece.
- **DTO**: DTO propios del evaluador, sin campos de identidad. `DashboardStatsDto` gana las pruebas sin evaluador.
- **Web**: `/evaluacion` pasa a ser la cola; páginas nuevas de corrección y de historial; el formulario de corrección se extrae a un componente que comparten el administrador y el evaluador; el panel de integridad acepta tiempos relativos; `ExamDetail.razor` gana la tarjeta de evaluadores; el dashboard y la cola del administrador muestran avisos y reservas.
- **Pruebas**: servicio de evaluación (asignación, ceguera de cada DTO, `404` por prueba no asignada, exclusión de sus propios resultados), reserva (toma, renovación, caducidad, conflicto, liberación) y limpieza al cambiar el rol.
- **Documentación**: `README.md` y `documentacion.md` describen la asignación, la ceguera y sus límites, y la reserva.
- **Privacidad**: la ceguera no cubre lo que el candidato escribe. Si pone su nombre en una respuesta abierta, el evaluador lo lee. La documentación lo dice.
