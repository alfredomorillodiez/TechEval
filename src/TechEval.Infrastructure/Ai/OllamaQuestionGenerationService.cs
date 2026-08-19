using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Services;

namespace TechEval.Infrastructure.Ai;

public class OllamaQuestionGenerationService : IQuestionGenerationAiService
{
    private readonly HttpClient _http;
    private readonly OllamaSettings _settings;
    private readonly ILogger<OllamaQuestionGenerationService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OllamaQuestionGenerationService(
        HttpClient http, IOptions<OllamaSettings> settings, ILogger<OllamaQuestionGenerationService> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<GeneratedQuestionResult> GenerateQuestionAsync(
        string topic, string categoryName, DifficultyLevel difficulty, QuestionType type, CancellationToken ct = default)
    {
        var prompt = BuildPrompt(topic, categoryName, difficulty, type);
        var rawResponse = await CallModelAsync(prompt, ct);
        if (rawResponse is null)
            return GeneratedQuestionResult.Fail("No se pudo contactar al modelo de IA.");

        var candidate = ParseAndValidate(rawResponse, type);
        if (!candidate.Success)
            return candidate;

        var (approved, reason) = await CritiqueAsync(candidate, type, ct);
        return approved ? candidate : GeneratedQuestionResult.Fail(reason);
    }

    private async Task<string?> CallModelAsync(string prompt, CancellationToken ct)
    {
        try
        {
            var request = new OllamaGenerateRequest(
                _settings.Model, prompt, false,
                new OllamaOptions(_settings.Temperature, _settings.RepeatPenalty, _settings.NumCtx, _settings.NumPredict));

            var httpResponse = await _http.PostAsJsonAsync("api/generate", request, ct);
            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Ollama respondió con error HTTP {Status} en {BaseUrl}", (int)httpResponse.StatusCode, _settings.BaseUrl);
                return null;
            }

            var response = await httpResponse.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken: ct);
            return response?.Response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al llamar a Ollama en {BaseUrl}", _settings.BaseUrl);
            return null;
        }
    }

    private static string BuildPrompt(string topic, string categoryName, DifficultyLevel difficulty, QuestionType type)
    {
        var difficultyEs = difficulty switch
        {
            DifficultyLevel.Basic => "básica",
            DifficultyLevel.Intermediate => "intermedia",
            DifficultyLevel.Advanced => "avanzada",
            _ => difficulty.ToString()
        };

        var persona =
            "Eres un examinador técnico senior diseñando preguntas de examen para candidatos a un puesto técnico. " +
            "Antes de responder, piensa cuidadosamente qué hace buena a una pregunta: debe tener una única " +
            "respuesta defendible como correcta, sin ambigüedad; si tiene opciones de respuesta, cada opción " +
            "incorrecta debe ser claramente distinguible en significado de las demás — evita distractores que " +
            "digan básicamente lo mismo con otras palabras. Evita preguntas de pura memorización si la dificultad " +
            "no es básica.\n\n";

        return type == QuestionType.MultipleChoice
            ? persona +
              $"Genera una pregunta de opción múltiple en español sobre \"{topic}\", para la categoría " +
              $"\"{categoryName}\", de dificultad {difficultyEs}. " +
              "Puedes razonar brevemente antes de responder. Al final de tu respuesta, incluye ÚNICAMENTE un " +
              "objeto JSON con esta forma exacta: " +
              "{\"questionText\": \"<enunciado>\", \"answers\": [" +
              "{\"text\": \"<opción 1>\", \"isCorrect\": true|false}, " +
              "{\"text\": \"<opción 2>\", \"isCorrect\": true|false}, " +
              "{\"text\": \"<opción 3>\", \"isCorrect\": true|false}, " +
              "{\"text\": \"<opción 4>\", \"isCorrect\": true|false}]}. " +
              "Debe haber exactamente 4 opciones, exactamente una con \"isCorrect\": true, y las 4 deben ser " +
              "claramente distintas entre sí en significado. " + JsonPurityInstruction
            : persona +
              $"Genera una pregunta de respuesta abierta en español sobre \"{topic}\", para la categoría " +
              $"\"{categoryName}\", de dificultad {difficultyEs}. " +
              "Puedes razonar brevemente antes de responder. Al final de tu respuesta, incluye ÚNICAMENTE un " +
              "objeto JSON con esta forma exacta: {\"questionText\": \"<enunciado>\", " +
              "\"sampleAnswer\": \"<respuesta de referencia para el evaluador>\"} " + JsonPurityInstruction;
    }

    private static string BuildCritiquePrompt(GeneratedQuestionResult candidate, QuestionType type)
    {
        var content = type == QuestionType.MultipleChoice
            ? $"Enunciado: \"{candidate.QuestionText}\"\nOpciones:\n" +
              string.Join("\n", candidate.Answers.Select(a =>
                  $"- \"{a.Text}\" (marcada como {(a.IsCorrect ? "correcta" : "incorrecta")})"))
            : $"Enunciado: \"{candidate.QuestionText}\"\nRespuesta de referencia: \"{candidate.SampleAnswer}\"";

        return "Eres un revisor técnico senior evaluando la calidad de una pregunta de examen ya generada. " +
               "Evalúa ESTRICTAMENTE estos tres problemas posibles:\n" +
               "1. Ambigüedad: ¿existe más de una opción que podría defenderse razonablemente como correcta?\n" +
               "2. Distractores poco distinguibles: si es de opción múltiple, ¿alguna opción incorrecta es " +
               "prácticamente indistinguible en significado de otra opción?\n" +
               "3. Coherencia: ¿el texto tiene errores, repeticiones o resulta confuso o mal formado?\n\n" +
               $"{content}\n\n" +
               "Puedes razonar brevemente antes de responder. Al final de tu respuesta, incluye ÚNICAMENTE un " +
               "objeto JSON con esta forma exacta: {\"approved\": true|false, \"reason\": \"<motivo breve si " +
               "approved es false; cadena vacía si es true>\"}. " + JsonPurityInstruction;
    }

    private const string JsonPurityInstruction =
        "Una vez que empieces a escribir ese objeto JSON, escríbelo completo de una sola vez, sin dudar ni " +
        "corregirte a mitad de camino, y no le agregues comentarios (por ejemplo, texto que empiece con \"//\") " +
        "ni ninguna explicación dentro o después del JSON: debe ser JSON válido por sí mismo, nada más.";

    private async Task<(bool Approved, string Reason)> CritiqueAsync(
        GeneratedQuestionResult candidate, QuestionType type, CancellationToken ct)
    {
        var rawResponse = await CallModelAsync(BuildCritiquePrompt(candidate, type), ct);
        if (rawResponse is null)
            return (false, "No se pudo contactar al modelo de IA durante la etapa de crítica.");

        CritiqueVerdict? verdict;
        try
        {
            verdict = JsonSerializer.Deserialize<CritiqueVerdict>(ExtractJsonPayload(rawResponse), JsonOptions);
        }
        catch (JsonException)
        {
            return (false, "La respuesta de la etapa de crítica no es un JSON válido.");
        }

        if (verdict is null)
            return (false, "La etapa de crítica no devolvió un veredicto.");

        if (verdict.Approved)
            return (true, string.Empty);

        return (false, string.IsNullOrWhiteSpace(verdict.Reason)
            ? "La etapa de crítica rechazó la pregunta sin especificar un motivo."
            : verdict.Reason);
    }

    /// <summary>
    /// Extrae el objeto JSON de la respuesta cruda del modelo, descartando el bloque de razonamiento
    /// (&lt;think&gt;...&lt;/think&gt;) cuando esté presente, y cualquier comentario estilo "//" que el
    /// modelo haya intercalado dentro del JSON (observado en la práctica: modelos de razonamiento a veces
    /// "piensan en voz alta" dentro de la respuesta estructurada). Tolera modelos que no emitan bloque de
    /// razonamiento ni comentarios.
    /// </summary>
    private static string ExtractJsonPayload(string raw)
    {
        var text = raw;
        var thinkEnd = text.IndexOf("</think>", StringComparison.OrdinalIgnoreCase);
        if (thinkEnd >= 0)
            text = text[(thinkEnd + "</think>".Length)..];

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        var candidate = start >= 0 && end > start ? text[start..(end + 1)] : text;

        return StripLineComments(candidate);
    }

    /// <summary>
    /// Elimina comentarios "// ..." hasta fin de línea, respetando el contenido de strings JSON
    /// (para no corromper valores que legítimamente contengan "//", como una URL).
    /// </summary>
    private static string StripLineComments(string json)
    {
        var result = new System.Text.StringBuilder(json.Length);
        var inString = false;
        var escaped = false;

        for (var i = 0; i < json.Length; i++)
        {
            var c = json[i];

            if (inString)
            {
                result.Append(c);
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') inString = false;
                continue;
            }

            if (c == '"')
            {
                inString = true;
                result.Append(c);
                continue;
            }

            if (c == '/' && i + 1 < json.Length && json[i + 1] == '/')
            {
                var lineEnd = json.IndexOf('\n', i);
                i = lineEnd >= 0 ? lineEnd - 1 : json.Length - 1;
                continue;
            }

            result.Append(c);
        }

        return result.ToString();
    }

    private static GeneratedQuestionResult ParseAndValidate(string rawResponse, QuestionType type)
    {
        OllamaQuestionPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<OllamaQuestionPayload>(ExtractJsonPayload(rawResponse), JsonOptions);
        }
        catch (JsonException)
        {
            return GeneratedQuestionResult.Fail("La respuesta del modelo no es un JSON válido.");
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.QuestionText))
            return GeneratedQuestionResult.Fail("La respuesta del modelo no incluye un enunciado.");

        if (type == QuestionType.MultipleChoice)
        {
            var answers = payload.Answers ?? new();
            if (answers.Count != 4)
                return GeneratedQuestionResult.Fail($"El modelo devolvió {answers.Count} respuestas en lugar de 4.");
            if (answers.Count(a => a.IsCorrect) != 1)
                return GeneratedQuestionResult.Fail("El modelo no marcó exactamente una respuesta como correcta.");
            if (answers.Any(a => string.IsNullOrWhiteSpace(a.Text)))
                return GeneratedQuestionResult.Fail("El modelo devolvió una o más respuestas vacías.");

            return GeneratedQuestionResult.Ok(payload.QuestionText, null,
                answers.Select(a => new GeneratedAnswer { Text = a.Text, IsCorrect = a.IsCorrect }).ToList());
        }

        if (string.IsNullOrWhiteSpace(payload.SampleAnswer))
            return GeneratedQuestionResult.Fail("El modelo no incluyó una respuesta de referencia.");

        return GeneratedQuestionResult.Ok(payload.QuestionText, payload.SampleAnswer, new());
    }

    private record OllamaGenerateRequest(string Model, string Prompt, bool Stream, OllamaOptions Options);

    private record OllamaOptions(
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("repeat_penalty")] double RepeatPenalty,
        [property: JsonPropertyName("num_ctx")] int NumCtx,
        [property: JsonPropertyName("num_predict")] int NumPredict);

    private record OllamaGenerateResponse(string Response);

    private class OllamaQuestionPayload
    {
        public string QuestionText { get; set; } = string.Empty;
        public string? SampleAnswer { get; set; }
        public List<OllamaAnswerPayload>? Answers { get; set; }
    }

    private class OllamaAnswerPayload
    {
        public string Text { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }

    private class CritiqueVerdict
    {
        public bool Approved { get; set; }
        public string? Reason { get; set; }
    }
}
