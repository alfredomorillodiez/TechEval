## Context

`OllamaQuestionGenerationService` (src/TechEval.Infrastructure/Ai/OllamaQuestionGenerationService.cs) hace una única llamada a `POST /api/generate` de Ollama por pregunta, con `"format": "json"` y sin ningún `options` configurado (usa los defaults de Ollama: `temperature` ~0.8, sin `repeat_penalty` explícito). El prompt (`BuildPrompt`) es escueto: pide el JSON directamente, sin persona, sin ejemplos, sin criterios de calidad. `ParseAndValidate` solo valida estructura (4 respuestas, exactamente 1 correcta, textos no vacíos) — nunca evalúa calidad de contenido.

`deepseek-r1:7b` es un modelo de razonamiento: normalmente emite un bloque `<think>...</think>` antes de la respuesta final, donde ocurre la deliberación real. `"format": "json"` en Ollama fuerza decodificación con gramática restringida desde el primer token, lo que en la práctica suprime esa fase de razonamiento. Se identifica esto como la causa más probable de los tres síntomas reportados: texto raro/repetido (degeneración de decoding al forzar JSON sin haber "terminado de pensar", agravado por no tener `repeat_penalty`), distractores muy parecidos entre sí (generar 4 opciones distinguibles requiere deliberación), y ambigüedad (sin espacio para verificar que existe una única respuesta defendible).

Restricción de infraestructura: el servicio `ollama` en `docker-compose.yml` no reserva GPU (inferencia CPU-only; de ahí el `HttpClient` con `Timeout = TimeSpan.FromMinutes(5)` ya configurado en `DependencyInjection.cs`). El usuario acepta explícitamente 10-30 segundos adicionales por pregunta, ya que `QuestionGenerationWorker` procesa en segundo plano sin que nadie espere de forma síncrona.

## Goals / Non-Goals

**Goals:**
- Reducir ambigüedad, distractores parecidos y texto degenerado en las preguntas generadas, dejando que el modelo razone antes de comprometerse a una respuesta estructurada.
- Añadir una etapa de crítica que capture estos problemas antes de que la pregunta llegue a la bandeja de revisión humana, con un motivo de rechazo específico y accionable.
- Mantener el contrato externo de `IQuestionGenerationAiService.GenerateQuestionAsync` (mismo método, mismo `GeneratedQuestionResult`) para no tocar `QuestionGenerationWorker` ni la UI de revisión.

**Non-Goals:**
- No se evalúa ni se cambia el tamaño/familia del modelo (`deepseek-r1:7b` se mantiene) — eso queda como experimento futuro separado.
- No se implementa reintento automático ni bucle de corrección tras un rechazo del crítico.
- No se modifica `QuestionReview.razor` más allá de que el mensaje de error mostrado sea más específico (ya lo muestra hoy, solo cambia su contenido).
- No se toca la validación estructural existente (4 respuestas / 1 correcta / textos no vacíos) — sigue vigente, la crítica es una capa adicional, no un reemplazo.

## Decisions

**1. Quitar `"format": "json"` de la llamada de generación; usar un prompt que pida el JSON explícitamente al final de la respuesta.**
Alternativa descartada: mantener `format: "json"` y solo ajustar el prompt — se descarta porque la restricción de gramática es estructural (ocurre a nivel de decodificación, no de contenido del prompt) y seguiría suprimiendo el razonamiento sin importar cuán bien redactado esté el prompt.

**2. Parseo tolerante de `<think>`:** si la respuesta contiene `<think>...</think>`, se extrae el contenido posterior a `</think>` para buscar el JSON ahí; si no hay bloque `<think>`, se busca el JSON en la respuesta completa (compatibilidad con otros modelos/tags que no razonan).
Alternativa descartada: exigir siempre un bloque `<think>` — se descarta porque `OllamaSettings.Model` es configurable, y forzar esa expectativa acoplaría el parseo a un modelo específico.

**3. Parámetros de generación explícitos vía `options` de Ollama:** `temperature` baja (orientativo 0.3-0.4), `repeat_penalty` (orientativo 1.3), `num_ctx`/`num_predict` ampliados para no truncar el bloque `<think>`. Estos valores se exponen en `OllamaSettings` (no hardcodeados) para poder ajustarlos empíricamente sin recompilar.
Alternativa descartada: dejar los defaults de Ollama — ya se comprobó que producen los síntomas reportados.

**4. Etapa de crítica como una segunda llamada independiente al mismo modelo, dentro de `OllamaQuestionGenerationService.GenerateQuestionAsync`** (no como un servicio separado ni como un paso visible para `QuestionGenerationWorker`).
El worker sigue llamando una sola vez a `GenerateQuestionAsync`; internamente, ese método ahora hace generación + crítica y devuelve un único `GeneratedQuestionResult` (éxito o fallo con motivo). Esto mantiene sin cambios el contrato de `IQuestionGenerationAiService`, `QuestionGenerationWorker` y la UI de revisión.
Alternativa descartada: exponer la crítica como un paso separado y visible en el `QuestionGenerationJobItem` (ej. un estado intermedio `PendingCritique`) — se descarta por ahora porque añade complejidad de estados sin que el usuario haya pedido visibilidad granular de esa etapa; si en el futuro se quiere mostrar el detalle de la crítica, se puede extender sin romper este diseño.

**5. El motivo de rechazo del crítico se convierte en el `ErrorMessage` del `GeneratedQuestionResult.Fail(...)`,** igual que hoy ocurre con los fallos de parseo/estructura — no se introduce un campo nuevo, se reutiliza el mecanismo existente de `QuestionGenerationJobItem.ErrorMessage`.

**6. Sin reintento automático tras un rechazo del crítico.** Decisión explícita del usuario: prefiere predictibilidad (un intento, resultado claro) sobre ahorro de trabajo manual. `QuestionGenerationWorker.ProcessJobAsync` no cambia su lógica de control.

## Risks / Trade-offs

- **[Riesgo] Doble llamada al modelo duplica el tiempo de generación por pregunta** → Mitigación: aceptado explícitamente por el usuario (10-30s adicionales tolerables), y el procesamiento ya es en segundo plano sin espera síncrona.
- **[Riesgo] El bloque `<think>` de un modelo de razonamiento puede ser largo o, en casos raros, entrar en bucle** → Mitigación: `num_predict` ampliado pero acotado (no ilimitado) actúa como límite superior; si el modelo no converge dentro de ese límite, la respuesta fallará el parseo como hoy (fallo estructural), sin bloquear el worker indefinidamente gracias al timeout de 5 minutos ya configurado.
- **[Riesgo] El crítico (mismo modelo) puede ser tan poco confiable como el generador, dando falsos rechazos o falsas aprobaciones** → Mitigación: se acepta como limitación conocida de esta primera iteración; el filtro humano en `QuestionReview.razor` sigue existiendo como última línea de defensa. Si en la práctica el crítico resulta poco fiable, es una señal para reconsiderar el modelo (ver Non-Goals) en una iteración futura, no algo que este cambio necesite resolver de raíz.
- **[Riesgo] Valores de `temperature`/`repeat_penalty`/`num_ctx`/`num_predict` elegidos "a ojo" pueden no ser óptimos** → Mitigación: se exponen como configuración (no hardcodeados) precisamente para poder ajustarlos empíricamente después de observar resultados reales, sin necesidad de otro cambio de código.

## Open Questions

- Ninguna bloqueante para implementar; los valores concretos de `temperature`/`repeat_penalty`/`num_ctx`/`num_predict` se afinarán empíricamente una vez desplegado, observando la tasa de rechazo del crítico y la calidad percibida en revisión.
