## Context

`dev` incorporó un pipeline de generación de preguntas por IA (Ollama) con revisión previa a publicación: `QuestionGenerationController` → `QuestionGenerationService` (crea `QuestionGenerationJob`/`QuestionGenerationJobItem`) → `QuestionGenerationWorker` (hosted service, procesa uno a uno) → `OllamaQuestionGenerationService` (llama al modelo local vía HTTP). Las preguntas resultantes nacían en `QuestionReviewStatus = PendingReview` y requerían aprobación manual antes de ser elegibles para exámenes.

Por separado, y sin dependencia alguna del pipeline anterior, ya existe la generación de exámenes por selección aleatoria: `ExamsController.Generate` → `ExamService.GenerateAsync` → `QuestionRepository.GetRandomAsync` (`ORDER BY NEWID()` filtrando por `IsActive` y `QuestionReviewStatus == Approved`). Esta capacidad cubre exactamente el requisito de "seleccionar preguntas aleatoriamente de las que ya existen en BD" y no requiere cambios funcionales, salvo el ajuste de su filtro al retirar `QuestionReviewStatus`.

Se decide dejar de depender de un modelo de IA local. La rama de trabajo será `sin-ia-local`, creada desde `dev`.

## Goals / Non-Goals

**Goals:**
- Eliminar toda dependencia de un modelo de IA local (Ollama) del código, configuración e infraestructura Docker.
- Eliminar la posibilidad de crear preguntas por cualquier vía que no sea el alta manual.
- Mantener intacta la generación aleatoria de exámenes a partir de preguntas existentes en BD.
- Mantener intacta la revisión manual de respuestas abiertas de examen (`open-question-review`), que es un flujo de corrección de exámenes, no de generación de preguntas.
- Simplificar el modelo de datos retirando campos que quedan sin ningún consumidor real tras la eliminación (`Category.AllowsAiGeneration`, `Question.QuestionReviewStatus`).

**Non-Goals:**
- No se rediseña el flujo de alta manual de preguntas (`QuestionForm.razor`) ni el de generación de exámenes: ya cumplen el objetivo funcional pedido.
- No se toca `main`; todo el trabajo vive en la rama nueva `sin-ia-local`.
- No se sustituye Ollama por ningún otro proveedor de IA (ni local ni en la nube); es una eliminación, no una migración.

## Decisions

1. **Eliminar en vez de deshabilitar por flag.** Se quita el código y las tablas en lugar de dejarlos apagados detrás de un feature flag. Justificación: no hay indicación de que se vaya a reactivar la IA a corto plazo, y mantener código muerto (controlador, worker, servicio Ollama, entidades) añade superficie de mantenimiento sin beneficio. Alternativa descartada: flag de configuración `AiGeneration:Enabled` — se descarta porque igual habría que mantener y probar dos caminos.

2. **Retirar `Question.QuestionReviewStatus` en vez de dejarlo "muerto".** El campo solo podía tomar un valor distinto de `Approved` cuando una pregunta provenía de generación por IA. Sin ese origen, el campo es constante y los filtros `QuestionReviewStatus == Approved` en `QuestionRepository` (3 ocurrencias) son morralla lógica. Se retira el campo, el enum, la columna de BD, y esos filtros quedan apoyados solo en `IsActive`. Riesgo: si en el futuro se reintroduce cualquier flujo de "pregunta pendiente de aprobación", habrá que reintroducir el campo — aceptable, dado que no hay uso presente.

3. **Retirar `Category.AllowsAiGeneration` en vez de dejarlo.** Sin generación por IA no hay nada que habilitar o deshabilitar por categoría. Se retira entidad, DTO, columna y el script de seed que ponía `iECS` en `false`.

4. **Migración de BD por script explícito, no por EF Migrations.** El proyecto usa `scripts/create_database.sql` como fuente de verdad del esquema (no hay carpeta `Migrations/` de EF detectada). Se seguirá el mismo patrón: un script SQL de alteración (`DROP TABLE QuestionGenerationJobItem`, `DROP TABLE QuestionGenerationJob`, `ALTER TABLE Category DROP COLUMN AllowsAiGeneration`, `ALTER TABLE Question DROP COLUMN QuestionReviewStatus`) y actualización de `create_database.sql` para nuevas instalaciones. Se elimina `scripts/add_category_ai_generation_flag.sql` ya que su efecto queda revertido por completo.

5. **Orden de eliminación de tablas.** `QuestionGenerationJobItem` referencia a `QuestionGenerationJob` (FK) y probablemente a `Question` (la pregunta resultante). Se elimina primero `QuestionGenerationJobItem`, luego `QuestionGenerationJob`, respetando las FKs.

6. **(Descubierto durante la implementación) Baja lógica de preguntas no aprobadas antes de dropear `QuestionReviewStatus`.** La BD de desarrollo local tenía datos reales del pipeline de IA: 21 jobs, 101 items y 31 preguntas (`IsActive = true`) en estado `PendingReview` o `Rejected`. Dropear la columna sin más las habría vuelto elegibles para exámenes de golpe (el único filtro restante es `IsActive`). El script `remove_ai_generation.sql` las marca `IsActive = false` antes de eliminar la columna, preservando el registro pero manteniéndolas excluidas, igual que estaban.

## Risks / Trade-offs

- **[Riesgo] Pérdida de historial de preguntas generadas por IA ya aprobadas.** Las preguntas ya aprobadas (`Approved`) permanecen en la tabla `Question` sin cambios — no se pierden, solo se pierde el registro de *que* vinieron de un job de IA (tablas `QuestionGenerationJob*`). → Mitigación: si se necesita trazabilidad histórica, exportar esas tablas antes de dropearlas; no se considera necesario salvo que el usuario lo pida.
- **[Riesgo] Downtime de exámenes en curso durante el `docker-compose down`/`up` de la rama nueva.** → Mitigación: este es trabajo de rama de desarrollo, no de producción; no aplica un plan de despliegue en caliente.
- **[Trade-off] Reactivar IA en el futuro exigirá reconstruir el pipeline desde cero** (en vez de solo "reactivar un flag"). Aceptado conscientemente por la decisión 1.

## Migration Plan

1. Crear rama `sin-ia-local` desde `dev`.
2. Eliminar código (dominio → aplicación → infraestructura → API → web), de adentro hacia afuera, verificando compilación en cada capa.
3. Actualizar `docker-compose.yml` y archivos de configuración.
4. Escribir y aplicar el script SQL de eliminación de columnas/tablas contra la BD de desarrollo local.
5. Actualizar `scripts/create_database.sql` para reflejar el esquema final.
6. Ejecutar la suite de tests (`tests/TechEval.Tests`), prestando atención a `ExamServiceTests.cs` por si referencia `QuestionReviewStatus`.
7. Verificación manual: levantar el stack sin `ollama`, confirmar que `/admin/questions` no ofrece generación por IA, que el alta manual funciona, y que `/admin/pruebas/generate` sigue generando exámenes con preguntas aleatorias de BD.

No aplica rollback automatizado: al ser una rama de desarrollo, revertir es volver a `dev`.

## Open Questions

(ninguna pendiente — alcance confirmado con el usuario)
