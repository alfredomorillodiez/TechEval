using FluentAssertions;
using Moq;
using Xunit;
using TechEval.Application.DTOs;
using TechEval.Application.Services;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;
using TechEval.Domain.Interfaces.Services;
using TechEval.Tests;

namespace TechEval.Tests.Services;

/// <summary>Corrección manual: validación atómica, recálculo y cierre del resultado.</summary>
public class OpenQuestionReviewServiceTests
{
    private readonly Mock<IExamResultRepository> _resultRepo = new();
    private readonly Mock<IExamRepository> _examRepo = new();
    private readonly Mock<IRepository<UserAnswer>> _answerRepo = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<IResultService> _resultService = new();
    private readonly FakeUnitOfWork _uow = new();

    private readonly OpenQuestionReviewService _sut;

    public OpenQuestionReviewServiceTests()
    {
        _examRepo.Setup(r => r.GetByIdAsync(7, default))
            .ReturnsAsync(new Exam { Id = 7, PassingScorePercentage = 70 });

        _resultService.Setup(r => r.GetDetailAsync(It.IsAny<int>(), default))
            .ReturnsAsync(new ExamResultDto(
                1, "Ana", "ana@test.com", "Prueba", 10, 8, 80m, true,
                ExamResultStatus.Reviewed, DateTime.UtcNow, new List<AnswerReviewDto>()));

        _sut = new OpenQuestionReviewService(
            _resultRepo.Object, _examRepo.Object, _answerRepo.Object,
            _email.Object, _resultService.Object, _uow);
    }

    /// <summary>Prueba de 10 pts: una de test de 5 acertada (congelada) y una abierta de 5.</summary>
    private ExamResult BuildPendingResult(
        ExamResultStatus status = ExamResultStatus.PendingReview,
        int testAwarded = 5,
        int openQuestionPoints = 5)
    {
        var testAnswer = new UserAnswer
        {
            Id = 100, QuestionId = 1, IsCorrect = true, AwardedPoints = testAwarded,
            Question = new Question { Id = 1, Points = 5, Type = QuestionType.MultipleChoice }
        };
        var openAnswer = new UserAnswer
        {
            Id = 200, QuestionId = 2, OpenAnswer = "Respuesta del candidato", AwardedPoints = null,
            Question = new Question { Id = 2, Points = openQuestionPoints, Type = QuestionType.OpenEnded }
        };

        return new ExamResult
        {
            Id = 1, ExamId = 7, Status = status,
            CandidateName = "Ana", CandidateEmail = "ana@test.com",
            Exam = new Exam { Id = 7, Title = "Prueba", PassingScorePercentage = 70 },
            TotalPoints = 10, ObtainedPoints = testAwarded, Passed = null,
            ExamSession = new ExamSession
            {
                Id = 1,
                UserAnswers = new List<UserAnswer> { testAnswer, openAnswer }
            }
        };
    }

    private void Setup(ExamResult result)
        => _resultRepo.Setup(r => r.GetForReviewAsync(1, default)).ReturnsAsync(result);

    [Fact]
    public async Task Review_Completa_RecalculaYCierraElResultado()
    {
        var result = BuildPendingResult();
        Setup(result);

        await _sut.SubmitReviewAsync(1,
            new SubmitReviewDto(new List<ReviewAnswerInputDto> { new(200, 3, "Le falta el caso borde") }),
            reviewedByUserId: 42);

        result.ObtainedPoints.Should().Be(8);          // 5 test + 3 abierta
        result.ScorePercentage.Should().Be(80m);
        result.Passed.Should().BeTrue();               // 80% >= 70%
        result.Status.Should().Be(ExamResultStatus.Reviewed);
        result.ReviewedByUserId.Should().Be(42);
        result.ReviewedAt.Should().NotBeNull();

        var open = result.ExamSession!.UserAnswers.Single(a => a.Id == 200);
        open.AwardedPoints.Should().Be(3);
        open.ReviewerComment.Should().Be("Le falta el caso borde");

        _email.Verify(e => e.SendExamResultAsync(
            "ana@test.com", "Ana", "Prueba", 80m, true, default), Times.Once);
    }

    [Fact]
    public async Task Review_Incompleta_SeRechazaSinPersistirNada()
    {
        var result = BuildPendingResult();
        Setup(result);

        var act = async () => await _sut.SubmitReviewAsync(1,
            new SubmitReviewDto(new List<ReviewAnswerInputDto>()), 42);

        await act.Should().ThrowAsync<InvalidReviewException>().WithMessage("*todas*");

        result.Status.Should().Be(ExamResultStatus.PendingReview);
        result.ExamSession!.UserAnswers.Single(a => a.Id == 200).AwardedPoints.Should().BeNull();
        _answerRepo.Verify(r => r.UpdateAsync(It.IsAny<UserAnswer>(), default), Times.Never);
    }

    [Fact]
    public async Task Review_PuntosFueraDeRango_SeRechaza()
    {
        Setup(BuildPendingResult());

        var act = async () => await _sut.SubmitReviewAsync(1,
            new SubmitReviewDto(new List<ReviewAnswerInputDto> { new(200, 9, null) }), 42);   // máx 5

        await act.Should().ThrowAsync<InvalidReviewException>().WithMessage("*entre 0 y 5*");
        _answerRepo.Verify(r => r.UpdateAsync(It.IsAny<UserAnswer>(), default), Times.Never);
    }

    [Fact]
    public async Task Review_PuntosNegativos_SeRechaza()
    {
        Setup(BuildPendingResult());

        var act = async () => await _sut.SubmitReviewAsync(1,
            new SubmitReviewDto(new List<ReviewAnswerInputDto> { new(200, -1, null) }), 42);

        await act.Should().ThrowAsync<InvalidReviewException>();
    }

    [Fact]
    public async Task Review_RespuestaAjenaAlResultado_SeRechaza()
    {
        Setup(BuildPendingResult());

        var act = async () => await _sut.SubmitReviewAsync(1,
            new SubmitReviewDto(new List<ReviewAnswerInputDto>
            {
                new(200, 3, null),
                new(999, 1, null)   // de otra sesión
            }), 42);

        await act.Should().ThrowAsync<InvalidReviewException>().WithMessage("*no pertenecen*");
        _answerRepo.Verify(r => r.UpdateAsync(It.IsAny<UserAnswer>(), default), Times.Never);
    }

    [Fact]
    public async Task Review_SobreResultadoYaCorregido_DevuelveConflicto()
    {
        Setup(BuildPendingResult(status: ExamResultStatus.Reviewed));

        var act = async () => await _sut.SubmitReviewAsync(1,
            new SubmitReviewDto(new List<ReviewAnswerInputDto> { new(200, 3, null) }), 42);

        await act.Should().ThrowAsync<AlreadyReviewedException>();
        _answerRepo.Verify(r => r.UpdateAsync(It.IsAny<UserAnswer>(), default), Times.Never);
    }

    [Fact]
    public async Task Review_FalloDelEmail_NoRevierteLaCorreccion()
    {
        var result = BuildPendingResult();
        Setup(result);

        _email.Setup(e => e.SendExamResultAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<bool>(), default))
            .ThrowsAsync(new InvalidOperationException("SMTP caído"));

        await _sut.SubmitReviewAsync(1,
            new SubmitReviewDto(new List<ReviewAnswerInputDto> { new(200, 3, null) }), 42);

        result.Status.Should().Be(ExamResultStatus.Reviewed);
        result.ObtainedPoints.Should().Be(8);
    }

    [Fact]
    public async Task Review_EditarPuntosDeLaPreguntaDespuesDelEnvio_NoAlteraElResultado()
    {
        var result = BuildPendingResult();
        Setup(result);

        // Alguien reevalúa la pregunta de test de 5 a 50 puntos DESPUÉS del envío.
        // AwardedPoints congeló los 5 originales, así que el recálculo no debe moverse.
        result.ExamSession!.UserAnswers.Single(a => a.Id == 100).Question!.Points = 50;

        await _sut.SubmitReviewAsync(1,
            new SubmitReviewDto(new List<ReviewAnswerInputDto> { new(200, 3, null) }), 42);

        result.ObtainedPoints.Should().Be(8);          // no 53
        result.ScorePercentage.Should().Be(80m);       // coherente con TotalPoints = 10
    }

    [Fact]
    public async Task GetDetail_IncluyeLasAbiertasEnBlancoPreasignadasACero()
    {
        var result = BuildPendingResult();
        var blank = new UserAnswer
        {
            Id = 300, QuestionId = 3, OpenAnswer = "   ", AwardedPoints = 0,
            Question = new Question
            {
                Id = 3, Points = 4, Type = QuestionType.OpenEnded,
                SampleAnswer = "Respuesta de referencia"
            }
        };
        result.ExamSession!.UserAnswers.Add(blank);
        Setup(result);

        var detail = await _sut.GetDetailAsync(1);

        detail!.Answers.Should().HaveCount(2);   // ambas abiertas, no solo la respondida
        var blankDto = detail.Answers.Single(a => a.UserAnswerId == 300);
        blankDto.AwardedPoints.Should().Be(0);
        blankDto.SampleAnswer.Should().Be("Respuesta de referencia");
    }

    [Fact]
    public async Task GetDetail_ResultadoYaCorregido_DevuelveConflicto()
    {
        Setup(BuildPendingResult(status: ExamResultStatus.Reviewed));

        var act = async () => await _sut.GetDetailAsync(1);

        await act.Should().ThrowAsync<AlreadyReviewedException>();
    }
}
