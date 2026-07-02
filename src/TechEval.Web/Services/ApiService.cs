using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TechEval.Application.DTOs;
using TechEval.Domain.Enums;

namespace TechEval.Web.Services;

public class ApiService
{
    private readonly HttpClient _http;
    private readonly ILogger<ApiService> _logger;

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
    public Task<AuthResultDto?> LoginAsync(LoginDto dto)
        => PostAsync<LoginDto, AuthResultDto>("api/auth/login", dto);

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

    public Task<QuestionDto?> CreateQuestionAsync(CreateQuestionDto dto)
        => PostAsync<CreateQuestionDto, QuestionDto>("api/questions", dto);

    public Task<QuestionDto?> UpdateQuestionAsync(int id, UpdateQuestionDto dto)
        => PutAsync<UpdateQuestionDto, QuestionDto>($"api/questions/{id}", dto);

    public Task<bool> DeleteQuestionAsync(int id)
        => DeleteAsync($"api/questions/{id}");

    // Exams
    public Task<List<ExamSummaryDto>?> GetExamsAsync()
        => GetAsync<List<ExamSummaryDto>>("api/exams");

    public Task<ExamDto?> GetExamAsync(int id)
        => GetAsync<ExamDto>($"api/exams/{id}");

    public Task<ExamDto?> CreateExamAsync(CreateExamDto dto)
        => PostAsync<CreateExamDto, ExamDto>("api/exams", dto);

    public Task<ExamDto?> GenerateExamAsync(GenerateExamDto dto)
        => PostAsync<GenerateExamDto, ExamDto>("api/exams/generate", dto);

    public Task<ExamDto?> UpdateExamAsync(int id, UpdateExamDto dto)
        => PutAsync<UpdateExamDto, ExamDto>($"api/exams/{id}", dto);

    public Task<bool> DeleteExamAsync(int id)
        => DeleteAsync($"api/exams/{id}");

    public async Task<string?> SendExamAsync(SendExamDto dto)
    {
        var response = await _http.PostAsJsonAsync("api/exams/send", dto, JsonOptions);
        if (!response.IsSuccessStatusCode) return null;
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return result.GetProperty("token").GetString();
    }

    public async Task<BulkSendResultDto?> SendExamBulkAsync(BulkSendExamDto dto)
    {
        var response = await _http.PostAsJsonAsync("api/exams/send-bulk", dto, JsonOptions);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<BulkSendResultDto>(JsonOptions);
    }

    // Results
    public Task<List<ExamResultSummaryDto>?> GetResultsAsync()
        => GetAsync<List<ExamResultSummaryDto>>("api/results");

    public Task<List<ExamResultSummaryDto>?> GetResultsByExamAsync(int examId)
        => GetAsync<List<ExamResultSummaryDto>>($"api/results/exam/{examId}");

    public Task<ExamResultDto?> GetResultDetailAsync(int id)
        => GetAsync<ExamResultDto>($"api/results/{id}");

    public Task<DashboardStatsDto?> GetDashboardAsync()
        => GetAsync<DashboardStatsDto>("api/results/dashboard");

    // Exam session (público)
    public Task<ExamTokenValidationDto?> ValidateTokenAsync(string token)
        => GetAsync<ExamTokenValidationDto>($"api/exam/validate/{token}");

    public Task<ExamSessionInfoDto?> StartSessionAsync(string token)
        => PostAsync<object, ExamSessionInfoDto>($"api/exam/start/{token}", new { });

    public async Task SaveAnswerAsync(int sessionId, SubmitAnswerDto dto)
    {
        await _http.PostAsJsonAsync($"api/exam/answer/{sessionId}", dto, JsonOptions);
    }

    public Task<ExamResultDto?> SubmitExamAsync(SubmitExamDto dto)
        => PostAsync<SubmitExamDto, ExamResultDto>("api/exam/submit", dto);

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
