## Why

Se probó `deepseek-r1:7b` con crítica automática (cambio previo `ai-question-quality`) contra Ollama real: mejoró la validez estructural, pero la etapa de crítica no demostró ser confiable (en pruebas reales dejó pasar una pregunta con distractores casi idénticos entre sí) y el tiempo por pregunta se disparó a varios minutos. Al probar `qwen2.5-coder:14b` (un modelo instruct especializado en código, sin fase de razonamiento) con el mismo hardware, los resultados fueron más rápidos (~45-75s por pregunta) y de mejor calidad estructural en una muestra de 8 preguntas variadas, sin necesitar la complejidad de crítica ni de manejo de razonamiento. Aparte, esa misma muestra reveló un problema distinto: para la categoría `iECS` (plataforma interna de Grupo Pronet), cualquier modelo genérico alucina contenido porque no tiene forma de conocer un sistema propietario — no es un problema de calidad del modelo, es un límite estructural que hay que resolver excluyendo esa categoría de la generación automática.

## What Changes

- **BREAKING**: se elimina la etapa de crítica de calidad introducida en `ai-question-quality` (segunda llamada al modelo evaluando ambigüedad/distractores/coherencia) — no demostró ser confiable en la práctica y duplicaba el tiempo por pregunta sin garantía de beneficio.
- El modelo de generación configurado por defecto pasa de `deepseek-r1:7b` a `qwen2.5-coder:14b`; el prompt se simplifica (ya no pide razonar antes de responder, porque el nuevo modelo no es de razonamiento), conservando la persona/criterios de calidad y la instrucción de no incluir comentarios dentro del JSON, que siguen siendo útiles independientemente del modelo.
- `Category` gana un indicador `AllowsAiGeneration` (por defecto `true`); se marca `false` para la categoría `iECS` mediante un cambio de datos puntual (no hay pantalla de administración de categorías todavía).
- El selector de categoría en el generador de preguntas por IA deja de listar categorías no aptas.
- `POST /api/question-generation/jobs` rechaza cualquier solicitud que indique una categoría no apta para generación por IA, incluso si se hace directamente contra la API sin pasar por la interfaz.
- La creación manual de preguntas para `iECS` (o cualquier categoría no apta) no se ve afectada — la restricción es solo sobre la generación automática por IA.

## Capabilities

### New Capabilities
(ninguna)

### Modified Capabilities
- `ai-question-generation`: se elimina la etapa de crítica de calidad (y sus requirements/escenarios asociados); se añade la restricción de categorías no aptas para generación por IA, tanto en la validación de la solicitud como en el selector de la interfaz.

## Impact

- **Infraestructura de IA**: `OllamaQuestionGenerationService` pierde `CritiqueAsync`/`BuildCritiquePrompt`/`CritiqueVerdict` y vuelve a un flujo de una sola llamada (generación → validación estructural); `BuildPrompt` se simplifica quitando la instrucción de razonar. `OllamaSettings.Model` cambia su valor por defecto.
- **Dominio**: `Category` gana la propiedad `AllowsAiGeneration` (bool, default `true`).
- **Aplicación/API**: `IQuestionGenerationService.CreateJobAsync` (o el punto donde se valida la solicitud) rechaza categorías no aptas; `CategoryDto` expone `AllowsAiGeneration` para que el frontend pueda filtrar.
- **Frontend**: `GenerateQuestions.razor` filtra el selector de categoría usando el nuevo campo.
- **Datos**: cambio puntual (script SQL, no migración EF ya que el proyecto no usa migraciones) para añadir la columna `AllowsAiGeneration` a `Categories` y marcar `iECS` como `false`.
- **Configuración**: `appsettings.json`/`appsettings.Development.json` actualizan `Ollama:Model`; requiere que `qwen2.5-coder:14b` esté descargado en la instancia de Ollama correspondiente (ya está en la de desarrollo local, probado en esta sesión).
