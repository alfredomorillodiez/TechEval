using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechEval.Domain.Entities;

namespace TechEval.Infrastructure.Data.Configurations;

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Text).IsRequired().HasMaxLength(2000);
        builder.Property(q => q.SampleAnswer).HasMaxLength(4000);
        builder.Property(q => q.Type).IsRequired();
        builder.Property(q => q.Difficulty).IsRequired();
        builder.Property(q => q.Points).HasDefaultValue(1);
        builder.Property(q => q.QuestionReviewStatus)
            .IsRequired()
            .HasDefaultValue(TechEval.Domain.Enums.QuestionReviewStatus.Approved);

        builder.HasOne(q => q.Category)
            .WithMany(c => c.Questions)
            .HasForeignKey(q => q.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(q => q.Answers)
            .WithOne(a => a.Question)
            .HasForeignKey(a => a.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(q => q.CategoryId);
        builder.HasIndex(q => q.Difficulty);
        builder.HasIndex(q => q.IsActive);
        builder.HasIndex(q => q.QuestionReviewStatus);
    }
}

public class AnswerConfiguration : IEntityTypeConfiguration<Answer>
{
    public void Configure(EntityTypeBuilder<Answer> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Text).IsRequired().HasMaxLength(1000);
    }
}

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Description).HasMaxLength(500);
        builder.HasIndex(c => c.Name).IsUnique();
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(200);
        builder.Property(u => u.Username).HasMaxLength(200);
        builder.Property(u => u.Name).IsRequired().HasMaxLength(200);
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => u.Username).IsUnique().HasFilter("[Username] IS NOT NULL");
    }
}

public class ExamConfiguration : IEntityTypeConfiguration<Exam>
{
    public void Configure(EntityTypeBuilder<Exam> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Title).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(1000);
        builder.Ignore(e => e.TotalPoints); // computed

        builder.HasOne(e => e.CreatedByUser)
            .WithMany(u => u.CreatedExams)
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ExamQuestionConfiguration : IEntityTypeConfiguration<ExamQuestion>
{
    public void Configure(EntityTypeBuilder<ExamQuestion> builder)
    {
        builder.HasKey(eq => eq.Id);
        builder.HasIndex(eq => new { eq.ExamId, eq.QuestionId }).IsUnique();

        builder.HasOne(eq => eq.Exam)
            .WithMany(e => e.ExamQuestions)
            .HasForeignKey(eq => eq.ExamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(eq => eq.Question)
            .WithMany(q => q.ExamQuestions)
            .HasForeignKey(eq => eq.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ExamTokenConfiguration : IEntityTypeConfiguration<ExamToken>
{
    public void Configure(EntityTypeBuilder<ExamToken> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Token).IsRequired().HasMaxLength(200);
        builder.Property(t => t.CandidateName).IsRequired().HasMaxLength(200);
        builder.Property(t => t.CandidateEmail).IsRequired().HasMaxLength(200);
        builder.HasIndex(t => t.Token).IsUnique();
        builder.Ignore(t => t.IsExpired);
        builder.Ignore(t => t.IsValid);

        builder.HasOne(t => t.Exam)
            .WithMany(e => e.ExamTokens)
            .HasForeignKey(t => t.ExamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne(t => t.ExamSession)
            .WithOne(s => s.ExamToken)
            .HasForeignKey<ExamSession>(s => s.ExamTokenId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.UserId);
    }
}

public class ExamSessionConfiguration : IEntityTypeConfiguration<ExamSession>
{
    public void Configure(EntityTypeBuilder<ExamSession> builder)
    {
        builder.HasKey(s => s.Id);
        builder.HasMany(s => s.UserAnswers)
            .WithOne(a => a.ExamSession)
            .HasForeignKey(a => a.ExamSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.ExamResult)
            .WithOne(r => r.ExamSession)
            .HasForeignKey<ExamResult>(r => r.ExamSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserAnswerConfiguration : IEntityTypeConfiguration<UserAnswer>
{
    public void Configure(EntityTypeBuilder<UserAnswer> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.OpenAnswer).HasMaxLength(4000);
        builder.Property(a => a.ReviewerComment).HasMaxLength(2000);

        builder.HasOne(a => a.Question)
            .WithMany()
            .HasForeignKey(a => a.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.SelectedAnswer)
            .WithMany()
            .HasForeignKey(a => a.SelectedAnswerId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}

public class ExamResultConfiguration : IEntityTypeConfiguration<ExamResult>
{
    public void Configure(EntityTypeBuilder<ExamResult> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.CandidateName).IsRequired().HasMaxLength(200);
        builder.Property(r => r.CandidateEmail).IsRequired().HasMaxLength(200);
        builder.Property(r => r.ScorePercentage).HasPrecision(5, 2);
        builder.Property(r => r.Status)
            .IsRequired()
            .HasDefaultValue(TechEval.Domain.Enums.ExamResultStatus.Reviewed);

        builder.HasOne(r => r.Exam)
            .WithMany()
            .HasForeignKey(r => r.ExamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne(r => r.ReviewedByUser)
            .WithMany()
            .HasForeignKey(r => r.ReviewedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasIndex(r => r.CandidateEmail);
        builder.HasIndex(r => r.ExamId);
        builder.HasIndex(r => r.CompletedAt);
        builder.HasIndex(r => r.UserId);
        builder.HasIndex(r => r.ReviewedByUserId);
        builder.HasIndex(r => r.Status);
    }
}

public class QuestionGenerationJobConfiguration : IEntityTypeConfiguration<QuestionGenerationJob>
{
    public void Configure(EntityTypeBuilder<QuestionGenerationJob> builder)
    {
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Topic).IsRequired().HasMaxLength(500);
        builder.Property(j => j.Difficulty).IsRequired();
        builder.Property(j => j.Type).IsRequired();
        builder.Property(j => j.Status).IsRequired();

        builder.HasOne(j => j.Category)
            .WithMany()
            .HasForeignKey(j => j.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(j => j.CreatedByUser)
            .WithMany()
            .HasForeignKey(j => j.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(j => j.Status);
    }
}

public class QuestionGenerationJobItemConfiguration : IEntityTypeConfiguration<QuestionGenerationJobItem>
{
    public void Configure(EntityTypeBuilder<QuestionGenerationJobItem> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Status).IsRequired();
        builder.Property(i => i.ErrorMessage).HasMaxLength(2000);

        builder.HasOne(i => i.Job)
            .WithMany(j => j.Items)
            .HasForeignKey(i => i.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Question)
            .WithMany()
            .HasForeignKey(i => i.QuestionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(i => i.Status);
        builder.HasIndex(i => i.JobId);
    }
}
