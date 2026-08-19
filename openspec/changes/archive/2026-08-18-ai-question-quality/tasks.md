## 1. Configuración de generación

- [x] 1.1 Añadir a `OllamaSettings` (src/TechEval.Infrastructure/Ai/OllamaSettings.cs) los campos de generación: `Temperature`, `RepeatPenalty`, `NumCtx`, `NumPredict`, con valores por defecto razonables (`Temperature=0.6` — decisión explícita del usuario, `RepeatPenalty=1.3`)
- [x] 1.2 Exponer esos valores en `appsettings.json`/`appsettings.Development.json` bajo la sección `Ollama` existente

## 2. Generación sin forzar JSON

- [x] 2.1 Quitar `"format": "json"` de la solicitud a `POST /api/generate` en `OllamaQuestionGenerationService.GenerateQuestionAsync`
- [x] 2.2 Incluir el objeto `options` (`temperature`, `repeat_penalty`, `num_ctx`, `num_predict`) en la solicitud, leyendo los valores de `OllamaSettings` — con `[JsonPropertyName]` explícito en cada campo, porque Ollama espera snake_case y el resto del archivo usa PascalCase que solo coincide por azar (case-insensitive) en campos de una palabra
- [x] 2.3 Reescribir `BuildPrompt` con persona ("examinador técnico senior"), criterios explícitos (distractores claramente distintos entre sí y de la correcta; una única respuesta defendible; evitar memorización trivial fuera de dificultad básica), y pedir el JSON al final de la respuesta (no como única salida permitida)

## 3. Parseo tolerante a razonamiento

- [x] 3.1 En `ParseAndValidate` (vía el nuevo helper `ExtractJsonPayload`), detectar un bloque `<think>...</think>` si está presente y extraer el JSON del contenido posterior a `</think>`
- [x] 3.2 Si no hay bloque `<think>`, mantener el comportamiento actual (buscar el JSON en la respuesta completa)
- [x] 3.3 Mantener sin cambios la validación estructural existente (4 respuestas, exactamente 1 correcta, textos no vacíos)

## 4. Etapa de crítica

- [x] 4.1 Diseñar el prompt de crítica: recibe la pregunta candidata (enunciado + opciones o respuesta de referencia) y pide un veredicto explícito de aprobación/rechazo evaluando ambigüedad, distinguibilidad de distractores y coherencia del texto, con motivo concreto en caso de rechazo
- [x] 4.2 Implementar la llamada de crítica dentro de `OllamaQuestionGenerationService.GenerateQuestionAsync`, después de que la pregunta candidata pase la validación estructural
- [x] 4.3 Si la crítica rechaza: devolver `GeneratedQuestionResult.Fail(<motivo de la crítica>)` (no un mensaje genérico)
- [x] 4.4 Si la crítica aprueba: devolver `GeneratedQuestionResult.Ok(...)` como hoy, sin cambios adicionales
- [x] 4.5 Confirmar que `QuestionGenerationWorker.ProcessJobAsync` no necesita cambios (sigue tratando el resultado combinado igual que un resultado de generación simple) — confirmado: el contrato de `IQuestionGenerationAiService.GenerateQuestionAsync`/`GeneratedQuestionResult` no cambió

## 5. Validación

- [x] 5.1 Probar manualmente contra Ollama real (fuera del pipeline) y luego a través de la app completa (job real #7, 3 preguntas, categoría SQL/"transacciones"/intermedia): el baseline original reproducía el problema reportado literalmente (distractores "A"/"B"/"C"/"D" sin sentido); con el cambio, 2/3 ítems fallaron por validación estructural (motivo claro, no genérico) y 1/3 pasó generación+crítica pero, inspeccionado a mano, todavía tenía distractores muy parecidos entre sí y texto con mezcla de idiomas — **la crítica no demostró atrapar de forma fiable los problemas que debía atrapar**, con esta muestra pequeña (n=3). Decisión del usuario: dejar el cambio así por ahora y observar en uso real, sin bloquear el cierre por esto.
- [x] 5.2 Confirmado con el job real: los ítems fallidos por validación estructural muestran un motivo concreto ("El modelo no marcó exactamente una respuesta como correcta") en vez de uno genérico, visible vía `GET /api/question-generation/pending-items`. No se observó ningún rechazo proveniente específicamente de la etapa de crítica en esta muestra (los 2 fallos fueron estructurales, no de crítica), por lo que ese mensaje específico queda sin ejercitar con datos reales todavía — el código lo emite (`GeneratedQuestionResult.Fail(<motivo de la crítica>)`), pero no hay evidencia empírica aún de cómo luce en la práctica.
- [x] 5.3 Confirmado con el job real: el job #7 quedó en `Completed` con 1 éxito y 2 fallos, sin bloquear el procesamiento de los demás ítems.
- [x] 5.4 Medido contra Ollama real: llamada de generación sola entre 128s (baseline sin cambios) y 170-254s (con razonamiento habilitado); con la etapa de crítica se estima un total de varios minutos por pregunta. El usuario confirmó explícitamente que este rango (varios minutos, más alto de lo inicialmente estimado en "10-30s extra") es aceptable.
