using TechEval.Domain.Enums;

namespace TechEval.Domain.Common;

/// <summary>
/// Filtros de los resultados. <c>Outcome</c> es <c>pending</c>, <c>passed</c> o <c>failed</c>; otro
/// valor o vacío no filtra. El rango de nota deja fuera a los pendientes, que no tienen nota
/// definitiva. <c>From</c> es inclusivo y <c>To</c> exclusivo, los dos en UTC.
/// </summary>
public sealed record ResultFilter(
    int? ExamId = null,
    string? Outcome = null,
    int? ScoreMin = null,
    int? ScoreMax = null,
    DateTime? From = null,
    DateTime? To = null);

/// <summary><c>Active</c> nulo no filtra por estado.</summary>
public sealed record QuestionFilter(
    int? CategoryId = null,
    DifficultyLevel? Difficulty = null,
    QuestionType? Type = null,
    bool? Active = null);

/// <summary><c>Active</c> nulo no filtra por estado.</summary>
public sealed record ExamFilter(bool? Active = null);

/// <summary>Una fila del listado de pruebas, con los recuentos calculados en la propia consulta.</summary>
public sealed record ExamSummaryRow(
    int Id,
    string Title,
    int TimeLimitMinutes,
    int PassingScorePercentage,
    bool IsActive,
    DateTime CreatedAt,
    int QuestionCount,
    int TotalPoints,
    int InvitationCount,
    int ResultCount,
    int AbandonedCount);

/// <summary>Identificador y título de una prueba, para los desplegables.</summary>
public sealed record ExamOption(int Id, string Title);

/// <summary>
/// Cifras de los resultados, sobre todos o sobre los de una prueba. Las notas y la distribución
/// salen solo de los corregidos: un pendiente lleva una puntuación parcial. Los seis tramos de la
/// distribución son 0–49, 50–59, 60–69, 70–79, 80–89 y 90–100.
/// </summary>
public sealed record ResultStats(
    int Total,
    int Reviewed,
    int Pending,
    int Passed,
    decimal AverageScore,
    decimal MaxScore,
    decimal MinScore,
    IReadOnlyList<int> Distribution);
