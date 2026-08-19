using Microsoft.EntityFrameworkCore;
using TechEval.Domain.Entities;

namespace TechEval.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Answer> Answers => Set<Answer>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamQuestion> ExamQuestions => Set<ExamQuestion>();
    public DbSet<ExamToken> ExamTokens => Set<ExamToken>();
    public DbSet<ExamSession> ExamSessions => Set<ExamSession>();
    public DbSet<UserAnswer> UserAnswers => Set<UserAnswer>();
    public DbSet<ExamResult> ExamResults => Set<ExamResult>();
    public DbSet<QuestionGenerationJob> QuestionGenerationJobs => Set<QuestionGenerationJob>();
    public DbSet<QuestionGenerationJobItem> QuestionGenerationJobItems => Set<QuestionGenerationJobItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
