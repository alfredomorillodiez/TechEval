## MODIFIED Requirements

### Requirement: Bandeja de revisión de preguntas generadas
El sistema SHALL exponer un listado de `QuestionGenerationJobItem` pendientes de revisión, mostrando el contenido de la pregunta generada cuando el ítem fue exitoso o el mensaje de error cuando falló, y permitiendo identificar de qué job proviene cada uno mediante el tema (`Topic`) y el identificador del job mostrados junto a cada ítem en la interfaz.

#### Scenario: Listado incluye éxitos y fallos del mismo lote
- **GIVEN** un `QuestionGenerationJob` con algunos `QuestionGenerationJobItem` en `Succeeded` y otros en `Failed`
- **WHEN** un administrador consulta la bandeja de revisión
- **THEN** el sistema SHALL devolver todos los ítems del job que aún no fueron aprobados ni rechazados, mostrando el contenido de la pregunta para los exitosos y el mensaje de error para los fallidos

#### Scenario: Cada ítem muestra su job de origen
- **GIVEN** una bandeja de revisión con ítems provenientes de más de un `QuestionGenerationJob`
- **WHEN** un administrador visualiza la lista
- **THEN** el sistema SHALL mostrar, junto a cada ítem, el tema (`Topic`) y el identificador del job del que proviene, permitiendo distinguir a qué lote pertenece

## ADDED Requirements

### Requirement: Indicador de progreso de generación en curso
El sistema SHALL exponer el progreso agregado de generación por `QuestionGenerationJob` — incluyendo los `QuestionGenerationJobItem` aún en estado `Pending`, que no forman parte del listado de la bandeja de revisión — y SHALL mostrar este progreso de forma siempre visible en la ventana de revisión de preguntas, actualizándose automáticamente mientras la ventana permanece abierta.

#### Scenario: Job en curso con ítems aún generándose
- **GIVEN** un `QuestionGenerationJob` en estado `Running` con `RequestedCount = 5`, de los cuales 2 ítems están `Succeeded`, 1 `Failed` y 2 siguen `Pending`
- **WHEN** un administrador consulta el progreso de generación desde la ventana de revisión
- **THEN** el sistema SHALL indicar que el job sigue en curso, cuántas preguntas del lote ya están listas para revisar, cuántas fallaron y cuántas quedan por generar

#### Scenario: Actualización automática sin recargar la página
- **GIVEN** un administrador con la ventana de revisión abierta mientras un job está `Running`
- **WHEN** el servicio en segundo plano completa la generación de un nuevo ítem del job
- **THEN** el sistema SHALL reflejar el progreso actualizado en la ventana de revisión en un plazo de pocos segundos, sin que el administrador tenga que recargar la página manualmente

#### Scenario: Sin jobs pendientes de atención
- **GIVEN** que no existe ningún `QuestionGenerationJob` con ítems `Pending`, o `Succeeded` con `Question.QuestionReviewStatus = PendingReview`, o `Failed`
- **WHEN** un administrador consulta el progreso de generación
- **THEN** el sistema SHALL indicar que no hay generación en curso ni preguntas pendientes de revisión, sin listar jobs cuyos ítems ya fueron todos aprobados o rechazados
