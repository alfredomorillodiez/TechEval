## Why

Las preguntas generadas por IA (`deepseek-r1:7b` vía Ollama) llegan con demasiada frecuencia a revisión con ambigüedad, distractores muy parecidos entre sí y texto raro/repetido. La causa raíz identificada: `"format": "json"` fuerza al modelo a comprometerse a una respuesta estructurada desde el primer token, sin dejarlo razonar — justo lo que un modelo de razonamiento necesita para producir distractores distintos y evitar ambigüedad. Se acepta un costo adicional de 10-30 segundos por pregunta (la generación ya corre en segundo plano) a cambio de mejor calidad.

## What Changes

- La llamada de generación deja de forzar `"format": "json"`; el prompt se reescribe con persona, criterios explícitos de calidad (distractores claramente distintos entre sí, una única respuesta defendible como correcta, evitar memorización trivial fuera de dificultad básica) y parámetros de generación configurables (`temperature`, `repeat_penalty`, `num_ctx`, `num_predict`) en vez de los defaults de Ollama.
- El parseo separa el bloque `<think>...</think>` (si está presente) del JSON final, tolerando modelos que no emitan ese bloque.
- **Nueva etapa de crítica**: tras generar una pregunta candidata, una segunda llamada al modelo evalúa ambigüedad, distinguibilidad de los distractores y coherencia del texto, devolviendo un veredicto de aprobación o rechazo con motivo concreto.
- Un rechazo del crítico marca el `QuestionGenerationJobItem` como `Failed` con el motivo real de rechazo (reemplazando mensajes genéricos como "no parseó"), sin reintento automático — el administrador decide manualmente si vuelve a solicitar esa pregunta.
- Una aprobación del crítico sigue el flujo existente sin cambios (persistencia con `QuestionReviewStatus = PendingReview`).
- **Fuera de alcance** (anotado como seguimiento futuro, sin tareas en este cambio): evaluar un modelo más grande (`deepseek-r1:14b` o similar). Se descarta aquí porque Ollama corre en CPU sin GPU reservada y probablemente excedería el presupuesto de latencia aceptado.

## Capabilities

### New Capabilities
(ninguna)

### Modified Capabilities
- `ai-question-generation`: el pipeline de generación gana una etapa de crítica antes de aceptar una pregunta como exitosa; los parámetros y el prompt de generación cambian para permitir razonamiento explícito del modelo; los mensajes de fallo pasan a incluir el motivo de rechazo del crítico cuando aplica.

## Impact

- **Infraestructura de IA**: `OllamaQuestionGenerationService` (nueva llamada de crítica, nuevo parseo de `<think>`, nuevo prompt, nuevos parámetros de `options`); `OllamaSettings` (nuevos campos de configuración de generación).
- **Dominio/Aplicación**: `GeneratedQuestionResult` (o equivalente) necesita transportar el motivo de rechazo del crítico hasta `QuestionGenerationJobItem.ErrorMessage`; `QuestionGenerationWorker` no cambia su estructura de control (sigue siendo un intento por ítem, sin reintentos), pero el resultado que recibe de `IQuestionGenerationAiService` ahora refleja el veredicto combinado generación+crítica.
- **Latencia**: cada pregunta pasa de 1 a 2 llamadas al modelo; el usuario aceptó explícitamente 10-30 segundos adicionales por pregunta dado que el procesamiento ya es en segundo plano.
- **Sin cambios de UI**: `QuestionReview.razor` sigue mostrando éxito/fallo igual que hoy; el único cambio visible es que el mensaje de error de los ítems fallidos por rechazo del crítico es más específico.
