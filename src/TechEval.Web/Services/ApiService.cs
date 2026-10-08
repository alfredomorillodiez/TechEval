using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TechEval.Application.DTOs;
using TechEval.Domain.Enums;

namespace TechEval.Web.Services;

/// <summary>Resultado de guardar una respuesta. Ver `ApiService.SaveAnswerAsync`.</summary>
public enum SaveOutcome { Saved, Rejected, Failed }

/// <summary>Resultado de renovar una reserva. Ver `ApiService.RenewReviewReservationAsync`.</summary>
public enum ReservationRenewal { Renewed, Lost, Unreachable }

public class ApiService
{
    private readonly HttpClient _http;
    private readonly ILogger<ApiService> _logger;

    // Lo que ve la persona cuando la petición no llegó (código 0). El texto de la excepción
    // es del navegador («Failed to fetch») y no dice nada útil.
    private const string NetworkError = "No se pudo conectar con el servidor. Comprueba tu conexión.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ApiService(HttpClient http, ILogger<ApiService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public void SetAuthToken(string token)
        => _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

    public void ClearAuthToken()
        => _http.DefaultRequestHeaders.Authorization = null;

    // Auth
    /// <summary>
    /// Con el código y el mensaje: un 401 (credenciales incorrectas) no es lo mismo que un 429
    /// (demasiados intentos), un fallo de red (código 0) o un error del servidor.
    /// </summary>
    public Task<(AuthResultDto? Result, int StatusCode, string? Error)> LoginAsync(LoginDto dto)
        => SendAsync<AuthResultDto>(HttpMethod.Post, "api/auth/login", dto);

    // Categories
    public Task<List<CategoryDto>?> GetCategoriesAsync()
        => GetAsync<List<CategoryDto>>("api/categories");

    public Task<CategoryDto?> CreateCategoryAsync(CreateCategoryDto dto)
        => PostAsync<CreateCategoryDto, CategoryDto>("api/categories", dto);

    public Task<CategoryDto?> UpdateCategoryAsync(int id, UpdateCategoryDto dto)
        => PutAsync<UpdateCategoryDto, CategoryDto>($"api/categories/{id}", dto);

    public Task<bool> DeleteCategoryAsync(int id)
        => DeleteAsync($"api/categories/{id}");

    // Questions
    public Task<List<QuestionSummaryDto>?> GetQuestionsAsync(
        int? categoryId = null, DifficultyLevel? difficulty = null, QuestionType? type = null)
    {
        var q = new List<string>();
        if (categoryId.HasValue) q.Add($"categoryId={categoryId}");
        if (difficulty.HasValue) q.Add($"difficulty={difficulty}");
        if (type.HasValue) q.Add($"type={type}");
        var qs = q.Any() ? "?" + string.Join("&", q) : "";
        return GetAsync<List<QuestionSummaryDto>>($"api/questions{qs}");
    }

    public Task<QuestionDto?> GetQuestionAsync(int id)
        => GetAsync<QuestionDto>($"api/questions/{id}");

    // Con el código y el mensaje: el formulario tiene que decir por qué falla un alta o una
    // edición, y un 409 (la pregunta ya tiene respuestas) no es un fallo de red.
    public Task<(QuestionDto? Result, int StatusCode, string? Error)> CreateQuestionAsync(CreateQuestionDto dto)
        => SendAsync<QuestionDto>(HttpMethod.Post, "api/questions", dto);

    public Task<(QuestionDto? Result, int StatusCode, string? Error)> UpdateQuestionAsync(int id, UpdateQuestionDto dto)
        => SendAsync<QuestionDto>(HttpMethod.Put, $"api/questions/{id}", dto);

    public Task<bool> DeleteQuestionAsync(int id)
        => DeleteAsync($"api/questions/{id}");

    // Exams
    public Task<List<ExamSummaryDto>?> GetExamsAsync()
        => GetAsync<List<ExamSummaryDto>>("api/exams");

    public Task<ExamDto?> GetExamAsync(int id)
        => GetAsync<ExamDto>($"api/exams/{id}");

    // Con el código y el mensaje: una prueba rechazada por validación (0 minutos, sin preguntas)
    // tiene que decir qué falla, no «Error al crear la prueba».
    public Task<(ExamDto? Result, int StatusCode, string? Error)> CreateExamAsync(CreateExamDto dto)
        => SendAsync<ExamDto>(HttpMethod.Post, "api/exams", dto);

    /// <summary>Con el código y el mensaje: un reparto rechazado dice qué nivel no alcanza.</summary>
    public Task<(ExamDto? Result, int StatusCode, string? Error)> GenerateExamAsync(GenerateExamDto dto)
        => SendAsync<ExamDto>(HttpMethod.Post, "api/exams/generate", dto);

    public Task<List<LevelCountDto>?> GetQuestionAvailabilityAsync(IEnumerable<int>? categoryIds)
    {
        var ids = categoryIds?.ToList() ?? new();
        var qs = ids.Count > 0 ? "?" + string.Join("&", ids.Select(id => $"categoryIds={id}")) : "";
        return GetAsync<List<LevelCountDto>>($"api/questions/availability{qs}");
    }

    public Task<(ExamDto? Result, int StatusCode, string? Error)> UpdateExamAsync(int id, UpdateExamDto dto)
        => SendAsync<ExamDto>(HttpMethod.Put, $"api/exams/{id}", dto);

    public Task<bool> DeleteExamAsync(int id)
        => DeleteAsync($"api/exams/{id}");

    private sealed record SendTokenResponse(string Token);

    /// <summary>
    /// Con el código y el mensaje: un envío rechazado (prueba desactivada, correo inválido) tiene
    /// que decir por qué, y no «verifica la configuración de email».
    /// </summary>
    public async Task<(string? Token, int StatusCode, string? Error)> SendExamAsync(SendExamDto dto)
    {
        var (result, status, error) = await SendAsync<SendTokenResponse>(HttpMethod.Post, "api/exams/send", dto);
        return (result?.Token, status, error);
    }

    public Task<(BulkSendResultDto? Result, int StatusCode, string? Error)> SendExamBulkAsync(BulkSendExamDto dto)
        => SendAsync<BulkSendResultDto>(HttpMethod.Post, "api/exams/send-bulk", dto);

    // Results
    public Task<List<ExamResultSummaryDto>?> GetResultsAsync()
        => GetAsync<List<ExamResultSummaryDto>>("api/results");

    public Task<List<ExamResultSummaryDto>?> GetResultsByExamAsync(int examId)
        => GetAsync<List<ExamResultSummaryDto>>($"api/results/exam/{examId}");

    public Task<ExamResultDto?> GetResultDetailAsync(int id)
        => GetAsync<ExamResultDto>($"api/results/{id}");

    public Task<DashboardStatsDto?> GetDashboardAsync()
        => GetAsync<DashboardStatsDto>("api/results/dashboard");

    // Student portal
    public Task<List<PendingExamDto>?> GetPendingExamsAsync()
        => GetAsync<List<PendingExamDto>>("api/student/pending");

    public Task<List<CompletedExamDto>?> GetCompletedExamsAsync()
        => GetAsync<List<CompletedExamDto>>("api/student/completed");

    // Exam session (público)
    public Task<ExamTokenValidationDto?> ValidateTokenAsync(string token)
        => GetAsync<ExamTokenValidationDto>($"api/exam/validate/{token}");

    public Task<ExamSessionInfoDto?> StartSessionAsync(string token)
        => PostAsync<object, ExamSessionInfoDto>($"api/exam/start/{token}", new { });

    /// <summary>
    /// Guarda una respuesta y dice qué pasó, para que la pantalla pueda mostrarlo. Nunca lanza:
    /// un fallo de red no debe romper la prueba. Un rechazo que un reintento no cambia (400,
    /// 403, 409) es `Rejected`; todo lo demás (red, 429, 5xx) es `Failed` y se reintenta.
    /// </summary>
    public async Task<SaveOutcome> SaveAnswerAsync(int sessionId, SubmitAnswerDto dto)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"api/exam/answer/{sessionId}", dto, JsonOptions);
            if (response.IsSuccessStatusCode) return SaveOutcome.Saved;

            return response.StatusCode is System.Net.HttpStatusCode.BadRequest
                    or System.Net.HttpStatusCode.Forbidden
                    or System.Net.HttpStatusCode.Conflict
                ? SaveOutcome.Rejected
                : SaveOutcome.Failed;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "No se pudo guardar una respuesta; se reintentará.");
            return SaveOutcome.Failed;
        }
    }

    /// <summary>
    /// Envía una señal de integridad. Devuelve true cuando no hay que reintentarla: llegó, o
    /// el servidor la rechazó por una razón que un reintento no cambia (400, 403, 409).
    /// Devuelve false ante un fallo de red, un 429 o un error del servidor. Nunca lanza:
    /// una señal perdida no debe interrumpir la prueba.
    /// </summary>
    public async Task<bool> RecordIntegrityEventAsync(int sessionId, IntegrityEventInputDto dto)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"api/exam/integrity/{sessionId}", dto, JsonOptions);
            return response.IsSuccessStatusCode
                || response.StatusCode is System.Net.HttpStatusCode.BadRequest
                    or System.Net.HttpStatusCode.Forbidden
                    or System.Net.HttpStatusCode.Conflict;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "No se pudo enviar una señal de integridad; se reintentará.");
            return false;
        }
    }

    public Task<IntegrityReportDto?> GetIntegrityReportAsync(int resultId)
        => GetAsync<IntegrityReportDto>($"api/results/{resultId}/integrity");

    /// <summary>
    /// Con el código y el mensaje: un envío rechazado (por ejemplo, una respuesta demasiado
    /// larga) tiene que decir por qué, y un fallo de red no puede parecer un envío hecho.
    /// </summary>
    public Task<(ExamSubmissionReceiptDto? Result, int StatusCode, string? Error)> SubmitExamAsync(SubmitExamDto dto)
        => SendAsync<ExamSubmissionReceiptDto>(HttpMethod.Post, "api/exam/submit", dto);

    // Corrección manual de preguntas abiertas
    public Task<List<PendingReviewSummaryDto>?> GetPendingReviewsAsync()
        => GetAsync<List<PendingReviewSummaryDto>>("api/review/pending");

    /// <summary>
    /// Abrir el detalle reserva el resultado. Con el código, la pantalla distingue un 409
    /// (ya corregido, o reservado por otra persona) de un 404.
    /// </summary>
    public Task<(PendingReviewDetailDto? Result, int StatusCode, string? Error)> GetReviewDetailAsync(int resultId)
        => SendAsync<PendingReviewDetailDto>(HttpMethod.Get, $"api/review/{resultId}", null);

    public async Task<ReservationRenewal> RenewReviewReservationAsync(int resultId)
        => ToRenewal((await SendAsync<object>(HttpMethod.Post, $"api/review/{resultId}/reservation", null)).StatusCode);

    /// <summary>
    /// Una reserva perdida (otra persona la tiene, o el resultado ya no está) no es lo mismo que
    /// una renovación que no llegó: la primera cierra el formulario, la segunda no puede tocarlo.
    /// </summary>
    private static ReservationRenewal ToRenewal(int status) => status switch
    {
        200 => ReservationRenewal.Renewed,
        403 or 404 or 409 => ReservationRenewal.Lost,
        _ => ReservationRenewal.Unreachable
    };

    public async Task<bool> ReleaseReviewReservationAsync(int resultId)
        => (await SendAsync<object>(HttpMethod.Delete, $"api/review/{resultId}/reservation", null)).StatusCode == 204;

    // Evaluación a ciegas (rol Evaluador)
    public Task<List<EvaluatorQueueItemDto>?> GetEvaluationQueueAsync()
        => GetAsync<List<EvaluatorQueueItemDto>>("api/evaluation/queue");

    public Task<(EvaluatorReviewDetailDto? Result, int StatusCode, string? Error)> GetEvaluationDetailAsync(int resultId)
        => SendAsync<EvaluatorReviewDetailDto>(HttpMethod.Get, $"api/evaluation/{resultId}", null);

    public async Task<ReservationRenewal> RenewEvaluationReservationAsync(int resultId)
        => ToRenewal((await SendAsync<ReservationDto>(HttpMethod.Post, $"api/evaluation/{resultId}/reservation", null)).StatusCode);

    public async Task<bool> ReleaseEvaluationReservationAsync(int resultId)
        => (await SendAsync<object>(HttpMethod.Delete, $"api/evaluation/{resultId}/reservation", null)).StatusCode == 204;

    public Task<(EvaluatorReviewOutcomeDto? Result, int StatusCode, string? Error)> SubmitEvaluationAsync(
        int resultId, SubmitReviewDto dto)
        => SendAsync<EvaluatorReviewOutcomeDto>(HttpMethod.Post, $"api/evaluation/{resultId}", dto);

    public Task<IntegrityReportDto?> GetEvaluationIntegrityAsync(int resultId)
        => GetAsync<IntegrityReportDto>($"api/evaluation/{resultId}/integrity");

    public Task<List<EvaluatorHistoryItemDto>?> GetEvaluationHistoryAsync()
        => GetAsync<List<EvaluatorHistoryItemDto>>("api/evaluation/history");

    public Task<EvaluatorHistoryDetailDto?> GetEvaluationHistoryDetailAsync(int resultId)
        => GetAsync<EvaluatorHistoryDetailDto>($"api/evaluation/history/{resultId}");

    // Evaluadores de una prueba
    public Task<List<ExamEvaluatorDto>?> GetExamEvaluatorsAsync(int examId)
        => GetAsync<List<ExamEvaluatorDto>>($"api/exams/{examId}/evaluators");

    public Task<(List<ExamEvaluatorDto>? Result, int StatusCode, string? Error)> AssignEvaluatorAsync(int examId, int userId)
        => SendAsync<List<ExamEvaluatorDto>>(HttpMethod.Post, $"api/exams/{examId}/evaluators/{userId}", null);

    public Task<(List<ExamEvaluatorDto>? Result, int StatusCode, string? Error)> UnassignEvaluatorAsync(int examId, int userId)
        => SendAsync<List<ExamEvaluatorDto>>(HttpMethod.Delete, $"api/exams/{examId}/evaluators/{userId}", null);

    /// <summary>
    /// Devuelve el resultado corregido, o el código de estado cuando falla: la pantalla
    /// necesita distinguir un 409 (ya corregido por otro admin) de un error cualquiera.
    /// </summary>
    public async Task<(ExamResultDto? Result, int StatusCode, string? Error)> SubmitReviewAsync(
        int resultId, SubmitReviewDto dto)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"api/review/{resultId}", dto, JsonOptions);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("POST api/review/{Id} → HTTP {Status}: {Body}",
                    resultId, (int)response.StatusCode, body);
                return (null, (int)response.StatusCode, ExtractError(body));
            }
            var result = await response.Content.ReadFromJsonAsync<ExamResultDto>(JsonOptions);
            return (result, 200, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POST api/review/{Id} failed", resultId);
            return (null, 0, NetworkError);
        }
    }

    // Users
    public Task<List<UserDto>?> GetUsersAsync(UserRole? role = null, bool? active = null, string? search = null)
    {
        var q = new List<string>();
        if (role.HasValue) q.Add($"role={role}");
        if (active.HasValue) q.Add($"active={active.Value.ToString().ToLowerInvariant()}");
        if (!string.IsNullOrWhiteSpace(search)) q.Add($"q={Uri.EscapeDataString(search.Trim())}");
        var qs = q.Any() ? "?" + string.Join("&", q) : "";
        return GetAsync<List<UserDto>>($"api/users{qs}");
    }

    public Task<(UserActionResultDto? Result, int StatusCode, string? Error)> CreateUserAsync(CreateUserDto dto)
        => SendAsync<UserActionResultDto>(HttpMethod.Post, "api/users", dto);

    public Task<(UserActionResultDto? Result, int StatusCode, string? Error)> ChangeUserRoleAsync(int id, UserRole role)
        => SendAsync<UserActionResultDto>(HttpMethod.Put, $"api/users/{id}/role", new ChangeRoleDto(role));

    public Task<(UserDto? Result, int StatusCode, string? Error)> DeactivateUserAsync(int id)
        => SendAsync<UserDto>(HttpMethod.Post, $"api/users/{id}/deactivate", null);

    public Task<(UserDto? Result, int StatusCode, string? Error)> ActivateUserAsync(int id)
        => SendAsync<UserDto>(HttpMethod.Post, $"api/users/{id}/activate", null);

    public Task<(UserActionResultDto? Result, int StatusCode, string? Error)> ResetUserAccessAsync(int id)
        => SendAsync<UserActionResultDto>(HttpMethod.Post, $"api/users/{id}/reset-access", null);

    // Enlace para fijar la contraseña (público)
    public Task<PasswordSetupInfoDto?> CheckPasswordSetupAsync(string token)
        => GetAsync<PasswordSetupInfoDto>($"api/auth/password-setup/{Uri.EscapeDataString(token)}");

    public async Task<(int StatusCode, string? Error)> SetPasswordAsync(SetPasswordDto dto)
    {
        var (_, status, error) = await SendAsync<object>(HttpMethod.Post, "api/auth/password-setup", dto);
        return (status, error);
    }

    /// <summary>
    /// Petición que devuelve el código y el mensaje de error de la API, para las pantallas
    /// que tienen que distinguir un 409 o un 400 de un fallo cualquiera. Un 204 da Result nulo.
    /// </summary>
    private async Task<(T? Result, int StatusCode, string? Error)> SendAsync<T>(
        HttpMethod method, string url, object? body)
    {
        try
        {
            using var request = new HttpRequestMessage(method, url);
            if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);

            var response = await _http.SendAsync(request);
            var status = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode)
            {
                var text = await response.Content.ReadAsStringAsync();
                _logger.LogError("{Method} {Url} → HTTP {Status}: {Body}", method, url, status, text);
                return (default, status, ExtractError(text));
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return (default, status, null);
            return (await response.Content.ReadFromJsonAsync<T>(JsonOptions), status, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Method} {Url} failed", method, url);
            return (default, 0, NetworkError);
        }
    }

    // Lee `error` (respuestas propias de los controladores) o `detail` (ProblemDetails de
    // ErrorHandlingMiddleware), que es donde llega el mensaje de las excepciones de negocio.
    private static string? ExtractError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var e)) return e.GetString();
            return doc.RootElement.TryGetProperty("detail", out var d) ? d.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    // HTTP helpers
    private async Task<TResponse?> GetAsync<TResponse>(string url)
    {
        try
        {
            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("GET {Url} → HTTP {Status}: {Body}", url, (int)response.StatusCode, body);
                return default;
            }
            return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GET {Url} failed", url);
            return default;
        }
    }

    private async Task<TResponse?> PostAsync<TRequest, TResponse>(string url, TRequest body)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(url, body, JsonOptions);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("POST {Url} → HTTP {Status}: {Body}", url, (int)response.StatusCode, responseBody);
                return default;
            }
            return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POST {Url} failed", url);
            return default;
        }
    }

    private async Task<TResponse?> PutAsync<TRequest, TResponse>(string url, TRequest body)
    {
        try
        {
            var response = await _http.PutAsJsonAsync(url, body, JsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PUT {Url} failed", url);
            return default;
        }
    }

    private async Task<bool> DeleteAsync(string url)
    {
        try
        {
            var response = await _http.DeleteAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DELETE {Url} failed", url);
            return false;
        }
    }
}
