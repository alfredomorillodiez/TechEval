using System.Linq.Expressions;
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

/// <summary>
/// Segundo envío de la misma prueba. El temporizador y un clic del candidato pueden
/// coincidir, así que el reenvío llega solo: tiene que devolver lo ya guardado.
/// </summary>
public class ExamResubmissionTests
{
    private readonly Mock<IExamTokenRepository> _tokenRepo = new();
    private readonly Mock<IExamRepository> _examRepo = new();
    private readonly Mock<IRepository<ExamSession>> _sessionRepo = new();
    private readonly Mock<IRepository<UserAnswer>> _answerRepo = new();
    private readonly Mock<IExamResultRepository> _resultRepo = new();
    private readonly Mock<IRepository<User>> _userRepo = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly FakeUnitOfWork _uow = new();

    private readonly ExamSession _session = new() { Id = 1, ExamTokenId = 1, Status = SessionStatus.Completed };

    private readonly ExamTokenService _sut;

    public ExamResubmissionTests()
    {
        _sessionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExamSession, bool>>>(), default))
            .ReturnsAsync(new List<ExamSession> { _session });

        _tokenRepo.Setup(r => r.GetByIdAsync(1, default))
            .ReturnsAsync(new ExamToken { Id = 1, Token = "tok" });

        var exam = new Exam
        {
            Id = 7,
            Title = "Prueba",
            PassingScorePercentage = 70,
            ExamQuestions = new List<ExamQuestion>
            {
                new()
                {
                    QuestionId = 1, Order = 1,
                    Question = new Question
                    {
                        Id = 1, Points = 5, Type = QuestionType.MultipleChoice,
                        Answers = new List<Answer> { new() { Id = 11, IsCorrect = true } }
                    }
                }
            }
        };

        _tokenRepo.Setup(r => r.GetWithExamAndSessionAsync("tok", default))
            .ReturnsAsync(new ExamToken
            {
                Id = 1, Token = "tok", ExamId = 7, Exam = exam, UserId = 5,
                CandidateName = "Ana", CandidateEmail = "ana@test.com"
            });
        _examRepo.Setup(r => r.GetWithQuestionsAsync(7, default)).ReturnsAsync(exam);

        _sut = new ExamTokenService(
            _tokenRepo.Object, _examRepo.Object, _sessionRepo.Object, _answerRepo.Object,
            _resultRepo.Object, _userRepo.Object, _email.Object, _tokens.Object, _uow);
    }

    /// <summary>Deja guardado un resultado previo para esa sesión.</summary>
    private ExamResult ResultadoPrevio(
        ExamResultStatus estado = ExamResultStatus.Reviewed,
        SessionStatus estadoSesion = SessionStatus.Completed)
    {
        _session.Status = estadoSesion;

        var result = new ExamResult
        {
            Id = 99,
            ExamSessionId = 1,
            ExamId = 7,
            CandidateName = "Ana",
            CandidateEmail = "ana@test.com",
            TotalPoints = 5,
            ObtainedPoints = 5,
            ScorePercentage = 100m,
            Passed = estado == ExamResultStatus.Reviewed ? true : null,
            Status = estado,
            CompletedAt = new DateTime(2026, 9, 16, 10, 0, 0, DateTimeKind.Utc)
        };

        _resultRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExamResult, bool>>>(), default))
            .ReturnsAsync(new List<ExamResult> { result });

        return result;
    }

    [Fact]
    public async Task SegundoEnvio_DevuelveElAcuseDelResultadoYaGuardado()
    {
        var previo = ResultadoPrevio();

        var receipt = await _sut.SubmitExamAsync(new SubmitExamDto(1, new List<SubmitAnswerDto>
        {
            new(1, 11, null)
        }), userId: 5);

        receipt.ResultId.Should().Be(previo.Id);
        receipt.ExamTitle.Should().Be("Prueba");
        receipt.ScorePercentage.Should().Be(100m);
        receipt.Passed.Should().BeTrue();
        receipt.CompletedAt.Should().Be(previo.CompletedAt);
    }

    [Fact]
    public async Task SegundoEnvio_DevuelveLaFechaMarcadaComoUtc()
    {
        // SQL Server devuelve DateTime sin zona. Sin marcarla, el acuse releído diría una
        // hora distinta del acuse original para el mismo envío.
        ResultadoPrevio();
        _resultRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExamResult, bool>>>(), default))
            .ReturnsAsync(new List<ExamResult>
            {
                new()
                {
                    Id = 99, ExamSessionId = 1, ExamId = 7,
                    CandidateName = "Ana", CandidateEmail = "ana@test.com",
                    TotalPoints = 5, ObtainedPoints = 5, ScorePercentage = 100m,
                    Passed = true, Status = ExamResultStatus.Reviewed,
                    CompletedAt = new DateTime(2026, 9, 16, 10, 0, 0, DateTimeKind.Unspecified)
                }
            });

        var receipt = await _sut.SubmitExamAsync(new SubmitExamDto(1, new List<SubmitAnswerDto>()), userId: 5);

        receipt.CompletedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task SegundoEnvio_NoCreaUnSegundoResultado()
    {
        ResultadoPrevio();

        await _sut.SubmitExamAsync(new SubmitExamDto(1, new List<SubmitAnswerDto> { new(1, 11, null) }), userId: 5);

        _resultRepo.Verify(r => r.AddAsync(It.IsAny<ExamResult>(), default), Times.Never);
    }

    [Fact]
    public async Task SegundoEnvio_NoTocaLasRespuestasNiLaSesion()
    {
        ResultadoPrevio();

        await _sut.SubmitExamAsync(new SubmitExamDto(1, new List<SubmitAnswerDto> { new(1, 11, null) }), userId: 5);

        _answerRepo.Verify(r => r.AddAsync(It.IsAny<UserAnswer>(), default), Times.Never);
        _answerRepo.Verify(r => r.UpdateAsync(It.IsAny<UserAnswer>(), default), Times.Never);
        _sessionRepo.Verify(r => r.UpdateAsync(It.IsAny<ExamSession>(), default), Times.Never);
    }

    [Fact]
    public async Task SegundoEnvio_NoReenviaCorreoAlCandidato()
    {
        ResultadoPrevio();

        await _sut.SubmitExamAsync(new SubmitExamDto(1, new List<SubmitAnswerDto> { new(1, 11, null) }), userId: 5);

        _email.Verify(e => e.SendExamResultAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<decimal>(), It.IsAny<bool>(), default), Times.Never);
        _email.Verify(e => e.SendExamPendingReviewAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task SegundoEnvio_ConRespuestasDistintas_NoAlteraElResultadoOriginal()
    {
        var previo = ResultadoPrevio();

        // El candidato manda otra cosa: la prueba ya está corregida y no se recalcula.
        var receipt = await _sut.SubmitExamAsync(new SubmitExamDto(1, new List<SubmitAnswerDto>
        {
            new(1, 999, null)
        }), userId: 5);

        receipt.ObtainedPoints.Should().Be(5);
        receipt.ScorePercentage.Should().Be(100m);
        previo.ObtainedPoints.Should().Be(5);
    }

    [Fact]
    public async Task SegundoEnvio_ConLaSesionAMedioCerrar_DevuelveElAcuseIgualmente()
    {
        // Resultado escrito pero sesión todavía InProgress: el envío anterior se interrumpió
        // entre las dos confirmaciones. Mirar el estado de la sesión no detectaría este caso.
        var previo = ResultadoPrevio(estadoSesion: SessionStatus.InProgress);

        var receipt = await _sut.SubmitExamAsync(new SubmitExamDto(1, new List<SubmitAnswerDto>
        {
            new(1, 11, null)
        }), userId: 5);

        receipt.ResultId.Should().Be(previo.Id);
        _resultRepo.Verify(r => r.AddAsync(It.IsAny<ExamResult>(), default), Times.Never);
    }

    [Fact]
    public async Task SegundoEnvio_DePruebaPendienteDeCorreccion_SigueSinCifras()
    {
        ResultadoPrevio(estado: ExamResultStatus.PendingReview);

        var receipt = await _sut.SubmitExamAsync(new SubmitExamDto(1, new List<SubmitAnswerDto>
        {
            new(1, 11, null)
        }), userId: 5);

        receipt.Status.Should().Be(ExamResultStatus.PendingReview);
        receipt.TotalPoints.Should().BeNull();
        receipt.ObtainedPoints.Should().BeNull();
        receipt.ScorePercentage.Should().BeNull();
        receipt.Passed.Should().BeNull();
    }
}
