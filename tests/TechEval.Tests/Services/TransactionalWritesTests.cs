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

namespace TechEval.Tests.Services;

/// <summary>
/// Atomicidad de las dos operaciones que escriben varias entidades. `BaseRepository`
/// confirma en cada llamada, así que sin transacción un fallo a mitad deja escrituras
/// sueltas: puntuaciones sin resultado cerrado, o un resultado con la sesión abierta.
/// </summary>
public class TransactionalWritesTests
{
    // ---------- Corrección de abiertas ----------

    private sealed class ReviewFixture
    {
        public Mock<IExamResultRepository> ResultRepo { get; } = new();
        public Mock<IExamRepository> ExamRepo { get; } = new();
        public Mock<IRepository<UserAnswer>> AnswerRepo { get; } = new();
        public Mock<IEmailService> Email { get; } = new();
        public Mock<IResultService> ResultService { get; } = new();
        public FakeUnitOfWork Uow { get; } = new();
        public OpenQuestionReviewService Sut { get; }

        public List<UserAnswer> Abiertas { get; } = new();

        public ReviewFixture()
        {
            var abierta1 = new UserAnswer
            {
                Id = 10, QuestionId = 1, OpenAnswer = "una",
                Question = new Question { Id = 1, Type = QuestionType.OpenEnded, Points = 5 }
            };
            var abierta2 = new UserAnswer
            {
                Id = 11, QuestionId = 2, OpenAnswer = "otra",
                Question = new Question { Id = 2, Type = QuestionType.OpenEnded, Points = 5 }
            };
            Abiertas.AddRange(new[] { abierta1, abierta2 });

            var result = new ExamResult
            {
                Id = 1, ExamId = 7, ExamSessionId = 1,
                CandidateName = "Ana", CandidateEmail = "ana@test.com",
                TotalPoints = 10, ObtainedPoints = 0,
                Status = ExamResultStatus.PendingReview,
                Exam = new Exam { Id = 7, Title = "Prueba", PassingScorePercentage = 50 },
                ExamSession = new ExamSession { Id = 1, UserAnswers = new List<UserAnswer> { abierta1, abierta2 } }
            };

            ResultRepo.Setup(r => r.GetForReviewAsync(1, default)).ReturnsAsync(result);
            ExamRepo.Setup(r => r.GetByIdAsync(7, default))
                .ReturnsAsync(new Exam { Id = 7, PassingScorePercentage = 50 });
            ResultService.Setup(r => r.GetDetailAsync(1, default))
                .ReturnsAsync(new ExamResultDto(
                    1, "Ana", "ana@test.com", "Prueba", 10, 8, 80m, true,
                    ExamResultStatus.Reviewed, DateTime.UtcNow, new List<AnswerReviewDto>()));

            Sut = new OpenQuestionReviewService(
                ResultRepo.Object, ExamRepo.Object, AnswerRepo.Object,
                Email.Object, ResultService.Object, Uow);
        }

        public SubmitReviewDto CorreccionValida() => new(new List<ReviewAnswerInputDto>
        {
            new(10, 4, "bien"),
            new(11, 4, "bien")
        });
    }

    [Fact]
    public async Task Correccion_SeEscribeDentroDeUnaTransaccion()
    {
        var f = new ReviewFixture();

        await f.Sut.SubmitReviewAsync(1, f.CorreccionValida(), reviewedByUserId: 9);

        f.Uow.Executed.Should().BeTrue();
        f.Uow.Committed.Should().BeTrue();
        f.Uow.RolledBack.Should().BeFalse();
    }

    [Fact]
    public async Task Correccion_FalloAlCerrarElResultado_RevierteTodo()
    {
        var f = new ReviewFixture();
        f.ResultRepo.Setup(r => r.UpdateAsync(It.IsAny<ExamResult>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("conexión perdida"));

        var act = () => f.Sut.SubmitReviewAsync(1, f.CorreccionValida(), reviewedByUserId: 9);

        await act.Should().ThrowAsync<InvalidOperationException>();
        f.Uow.RolledBack.Should().BeTrue();
        f.Uow.Committed.Should().BeFalse();
    }

    [Fact]
    public async Task Correccion_FalloAlEscribirUnaPuntuacion_NoEnviaCorreoNiConfirma()
    {
        var f = new ReviewFixture();
        f.AnswerRepo.Setup(r => r.UpdateAsync(It.IsAny<UserAnswer>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("tiempo de espera agotado"));

        var act = () => f.Sut.SubmitReviewAsync(1, f.CorreccionValida(), reviewedByUserId: 9);

        await act.Should().ThrowAsync<InvalidOperationException>();
        f.Uow.RolledBack.Should().BeTrue();
        f.Email.Verify(e => e.SendExamResultAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<decimal>(), It.IsAny<bool>(), default), Times.Never);
    }

    [Fact]
    public async Task Correccion_RechazadaPorInvalida_NiSiquieraAbreLaTransaccion()
    {
        var f = new ReviewFixture();

        // Falta la segunda respuesta abierta: la validación va antes de escribir nada.
        var incompleta = new SubmitReviewDto(new List<ReviewAnswerInputDto> { new(10, 4, null) });

        var act = () => f.Sut.SubmitReviewAsync(1, incompleta, reviewedByUserId: 9);

        await act.Should().ThrowAsync<InvalidReviewException>();
        f.Uow.Executed.Should().BeFalse();
    }

    // ---------- Envío de la prueba ----------

    private sealed class SubmitFixture
    {
        public Mock<IExamTokenRepository> TokenRepo { get; } = new();
        public Mock<IExamRepository> ExamRepo { get; } = new();
        public Mock<IRepository<ExamSession>> SessionRepo { get; } = new();
        public Mock<IRepository<UserAnswer>> AnswerRepo { get; } = new();
        public Mock<IExamResultRepository> ResultRepo { get; } = new();
        public Mock<IRepository<User>> UserRepo { get; } = new();
        public Mock<IEmailService> Email { get; } = new();
        public Mock<ITokenService> Tokens { get; } = new();
        public FakeUnitOfWork Uow { get; } = new();
        public ExamTokenService Sut { get; }

        public List<UserAnswer> Escritas { get; } = new();

        public SubmitFixture()
        {
            SessionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExamSession, bool>>>(), default))
                .ReturnsAsync(new List<ExamSession> { new() { Id = 1, ExamTokenId = 1 } });

            TokenRepo.Setup(r => r.GetByIdAsync(1, default))
                .ReturnsAsync(new ExamToken { Id = 1, Token = "tok" });

            AnswerRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserAnswer, bool>>>(), default))
                .ReturnsAsync(new List<UserAnswer>());
            AnswerRepo.Setup(r => r.AddAsync(It.IsAny<UserAnswer>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((UserAnswer a, CancellationToken _) => { Escritas.Add(a); return a; });

            ResultRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExamResult, bool>>>(), default))
                .ReturnsAsync(new List<ExamResult>());
            ResultRepo.Setup(r => r.AddAsync(It.IsAny<ExamResult>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ExamResult r, CancellationToken _) => { r.Id = 99; return r; });

            var exam = new Exam
            {
                Id = 7, Title = "Prueba", PassingScorePercentage = 70,
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

            TokenRepo.Setup(r => r.GetWithExamAndSessionAsync("tok", default))
                .ReturnsAsync(new ExamToken
                {
                    Id = 1, Token = "tok", ExamId = 7, Exam = exam,
                    CandidateName = "Ana", CandidateEmail = "ana@test.com"
                });
            ExamRepo.Setup(r => r.GetWithQuestionsAsync(7, default)).ReturnsAsync(exam);

            Sut = new ExamTokenService(
                TokenRepo.Object, ExamRepo.Object, SessionRepo.Object, AnswerRepo.Object,
                ResultRepo.Object, UserRepo.Object, Email.Object, Tokens.Object, Uow);
        }

        public SubmitExamDto Envio() => new(1, new List<SubmitAnswerDto> { new(1, 11, null) });
    }

    [Fact]
    public async Task Envio_SeEscribeDentroDeUnaTransaccion()
    {
        var f = new SubmitFixture();

        await f.Sut.SubmitExamAsync(f.Envio());

        f.Uow.Executed.Should().BeTrue();
        f.Uow.Committed.Should().BeTrue();
    }

    [Fact]
    public async Task Envio_FalloAlEscribirElResultado_RevierteLasRespuestas()
    {
        var f = new SubmitFixture();
        f.ResultRepo.Setup(r => r.AddAsync(It.IsAny<ExamResult>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("conexión perdida"));

        var act = () => f.Sut.SubmitExamAsync(f.Envio());

        await act.Should().ThrowAsync<InvalidOperationException>();
        f.Uow.RolledBack.Should().BeTrue();
        f.Uow.Committed.Should().BeFalse();
    }

    [Fact]
    public async Task Envio_FalloAlCerrarLaSesion_RevierteElResultado()
    {
        // Esta es la ventana que los arreglos de reanudación y de envío repetido tuvieron
        // que tolerar: resultado escrito con la sesión todavía InProgress.
        var f = new SubmitFixture();
        f.SessionRepo.Setup(r => r.UpdateAsync(It.IsAny<ExamSession>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("tiempo de espera agotado"));

        var act = () => f.Sut.SubmitExamAsync(f.Envio());

        await act.Should().ThrowAsync<InvalidOperationException>();
        f.Uow.RolledBack.Should().BeTrue();
    }

    [Fact]
    public async Task Envio_FalloAlCerrarLaSesion_NoNotificaAlCandidato()
    {
        var f = new SubmitFixture();
        f.SessionRepo.Setup(r => r.UpdateAsync(It.IsAny<ExamSession>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("fallo"));

        var act = () => f.Sut.SubmitExamAsync(f.Envio());
        await act.Should().ThrowAsync<InvalidOperationException>();

        f.Email.Verify(e => e.SendExamResultAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<decimal>(), It.IsAny<bool>(), default), Times.Never);
    }

    [Fact]
    public async Task Envio_ElCorreoSeMandaConLaTransaccionYaConfirmada()
    {
        var f = new SubmitFixture();
        bool confirmadaAlNotificar = false;

        f.Email.Setup(e => e.SendExamResultAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<bool>(), default))
            .Callback(() => confirmadaAlNotificar = f.Uow.Committed)
            .Returns(Task.CompletedTask);

        await f.Sut.SubmitExamAsync(f.Envio());

        confirmadaAlNotificar.Should().BeTrue(
            "mantener la transacción abierta mientras se espera al SMTP bloquearía filas");
    }

    [Fact]
    public async Task Envio_Repetido_NoAbreNingunaTransaccion()
    {
        var f = new SubmitFixture();
        f.ResultRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExamResult, bool>>>(), default))
            .ReturnsAsync(new List<ExamResult>
            {
                new()
                {
                    Id = 99, ExamSessionId = 1, ExamId = 7,
                    CandidateName = "Ana", CandidateEmail = "ana@test.com",
                    TotalPoints = 5, ObtainedPoints = 5, ScorePercentage = 100m,
                    Passed = true, Status = ExamResultStatus.Reviewed
                }
            });

        await f.Sut.SubmitExamAsync(f.Envio());

        f.Uow.Executed.Should().BeFalse("la salida anticipada no escribe nada");
    }
}
