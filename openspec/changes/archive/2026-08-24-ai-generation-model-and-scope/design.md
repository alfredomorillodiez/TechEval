## Context

El cambio previo `ai-question-quality` (ya archivado) diagnosticó correctamente que `"format": "json"` suprimía el razonamiento de `deepseek-r1:7b`, e implementó: prompt sin `format:json`, parámetros de generación configurables, parseo tolerante a bloque `<think>`, y una etapa de crítica (segunda llamada al modelo) que evaluaba ambigüedad/distractores/coherencia antes de aceptar una pregunta.

Se validó ese pipeline contra Ollama real, tanto con llamadas sueltas como con un job real de 3 preguntas a través de la app completa. Resultado: la crítica dejó pasar la única pregunta que llegó a evaluar, pese a que esa pregunta tenía exactamente los problemas que debía detectar (distractores casi idénticos, texto con mezcla de idiomas). El riesgo ya estaba anotado en el diseño de ese cambio ("el crítico puede ser tan poco confiable como el generador") y se confirmó en la práctica.

Se probó entonces `qwen2.5-coder:14b` (modelo instruct especializado en código, sin fase de razonamiento) con el mismo hardware, mismo prompt base (persona + criterios de calidad + instrucción anti-comentarios) y mismos parámetros de generación. Sobre una muestra de 8 preguntas (7 categorías/dificultades distintas + la primera prueba de validación), los resultados fueron:
- Más rápidos: ~45-110s por pregunta, sin fase de razonamiento que consumir (más rápido incluso que la versión de `deepseek-r1:7b` con razonamiento habilitado, que tomaba 170-254s).
- JSON siempre válido, sin comentarios ni contradicciones internas — el problema de "el modelo se corrige a mitad de camino dentro del JSON" no se observó ni una vez.
- Contenido mayormente sólido: 2 preguntas sin reparos, 2 con imprecisión técnica menor (afirmaciones parcialmente simplificadas sobre semántica de `async`/Azure Traffic Manager), 1 con un distractor discutible (SAML como "incorrecto" en un contexto donde también es válido), 1 con un error factual real (etiquetado cruzado de Integración Continua vs. Entrega Continua), y 1 con contenido casi con certeza alucinado sobre `iECS` (categoría interna de Grupo Pronet, sin datos de entrenamiento posibles sobre ese sistema propietario).

El hallazgo de `iECS` es distinto en naturaleza a los demás: no es un problema de calidad del modelo que mejore con más parámetros o mejor prompt — es un límite estructural (el modelo no puede saber lo que nunca vio), y la única mitigación real es no pedirle que genere contenido sobre esa categoría.

## Goals / Non-Goals

**Goals:**
- Simplificar el pipeline de generación volviendo a una sola llamada al modelo, eliminando la complejidad de crítica que no demostró valor.
- Cambiar el modelo configurado por defecto a `qwen2.5-coder:14b`, manteniendo las mejoras de prompt (persona, criterios de distractores distintos, anti-comentarios) que resultaron útiles independientemente del modelo.
- Excluir la categoría `iECS` (y cualquier categoría futura marcada igual) de la generación automática por IA, en la interfaz y en el endpoint.

**Non-Goals:**
- No se implementa una pantalla de administración de categorías — el flag se activa/desactiva por ahora mediante un cambio de datos puntual.
- No se reintroduce ningún mecanismo de crítica o verificación automática de calidad — el filtro de calidad sigue siendo, como ya era antes de `ai-question-quality`, la revisión humana en `QuestionReview.razor`.
- No se resuelve el problema de fondo de "el modelo no conoce sistemas propietarios" para otras posibles categorías futuras más allá de excluirlas de la generación — no se explora aquí ninguna forma de darle al modelo conocimiento real de esos sistemas (p. ej. RAG sobre documentación interna).
- No se cambia el mecanismo de configuración de `OllamaSettings` (`Temperature`, `RepeatPenalty`, `NumCtx`, `NumPredict` siguen existiendo y aplicando igual, independientemente del modelo configurado).

## Decisions

**1. Eliminar la etapa de crítica en vez de reforzarla.**
Alternativas consideradas: reforzar el prompt de crítica, o correrla por consenso (2-3 veces, exigir mayoría). Se descartan por ahora: ambas siguen dependiendo del mismo modelo pequeño para juzgarse a sí mismo, y el cambio de modelo ya resuelve gran parte del problema original (JSON limpio, sin necesidad de que el modelo "piense" para producir distractores distintos) sin pagar el costo de una segunda llamada. Si en el futuro `qwen2.5-coder:14b` muestra una tasa de error inaceptable, la crítica puede reconsiderarse — pero no se reintroduce especulativamente ahora.

**2. Cambiar el prompt de generación para quitar la instrucción de razonar, no solo el modelo.**
`qwen2.5-coder:14b` no es un modelo de razonamiento; pedirle "puedes razonar brevemente antes de responder" no lo perjudica pero tampoco aporta nada. Se simplifica el prompt quitando esa frase, manteniendo persona + criterios de calidad + instrucción anti-comentarios, que demostraron valor en ambos modelos probados.

**3. `AllowsAiGeneration` como propiedad de `Category`, no como lista hardcodeada de nombres.**
Decisión ya tomada en la exploración: reutilizable para futuras categorías propias de Pronet, evita comparar por nombre de categoría en el código.

**4. La restricción se aplica en dos capas: filtrado en la UI del generador + rechazo en el endpoint.**
El filtrado en la UI es la experiencia normal (el admin nunca ve `iECS` como opción al generar por IA). El rechazo en el endpoint es defensa en profundidad: alguien podría llamar `POST /api/question-generation/jobs` directamente con el `CategoryId` de `iECS` sin pasar por la UI, y el sistema debe rechazarlo igual.

**5. La restricción es solo sobre generación por IA, no sobre creación manual.**
`AllowsAiGeneration = false` no afecta `CategoriesController`, `QuestionsController` ni la creación/edición manual de preguntas — un administrador sigue pudiendo crear preguntas de `iECS` a mano con conocimiento real del sistema.

## Risks / Trade-offs

- **[Riesgo] `qwen2.5-coder:14b` sigue produciendo errores de contenido ocasionales** (visto en la muestra: imprecisiones técnicas menores, un distractor discutible, un error factual real de CI/CD) → Mitigación: la revisión humana en `QuestionReview.razor` sigue siendo el filtro final, como ya era el diseño original antes de introducir la crítica. No se promete "cero errores", solo una tasa de error menor y más rápida de producir que antes.
- **[Riesgo] Modelo más grande consume más RAM** (~9GB en disco vs. 4.7GB del anterior) → Mitigación: ya validado empíricamente en el hardware de desarrollo (31.7GB RAM total, 8 núcleos) sin problemas; a vigilar si se despliega en un entorno con menos memoria disponible.
- **[Riesgo] Quitar la crítica es un cambio de comportamiento observable** (los admins que ya vieron mensajes de rechazo por crítica dejarán de verlos) → Mitigación: se documenta como **BREAKING** en el proposal; el comportamiento de fallo por validación estructural (JSON inválido, cantidad de respuestas incorrecta, ninguna/más de una marcada correcta) no cambia.
- **[Riesgo] Otras categorías internas no identificadas hoy** → Mitigación: el usuario confirmó explícitamente que de las 9 categorías existentes, solo `iECS` es específica de Pronet; el flag queda disponible para marcar otras en el futuro sin cambios de código adicionales.

## Migration Plan

1. Añadir la columna `AllowsAiGeneration` (bit, default 1) a `Categories` vía script SQL (no hay migraciones EF en este proyecto — se sigue el mismo patrón usado en cambios anteriores: `scripts/add_*.sql` no destructivo).
2. Marcar `AllowsAiGeneration = 0` para la categoría `iECS` en ese mismo script.
3. Actualizar `appsettings.json`/`appsettings.Development.json`: `Ollama:Model` → `qwen2.5-coder:14b`.
4. Desplegar el código actualizado (prompt simplificado, sin crítica; filtrado de categorías).
5. Sin pasos de rollback de datos necesarios — la columna nueva es aditiva y no destructiva; revertir el código basta para volver al comportamiento anterior si hiciera falta.
