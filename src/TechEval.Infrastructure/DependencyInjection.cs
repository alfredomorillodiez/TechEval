using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TechEval.Domain.Entities;
using TechEval.Domain.Interfaces.Repositories;
using TechEval.Domain.Interfaces.Services;
using TechEval.Application.Services;
using TechEval.Infrastructure.Ai;
using TechEval.Infrastructure.BackgroundJobs;
using TechEval.Infrastructure.Data;
using TechEval.Infrastructure.Email;
using TechEval.Infrastructure.Repositories;
using TechEval.Infrastructure.Security;

namespace TechEval.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly("TechEval.Infrastructure")));

        // Repositories
        services.AddScoped(typeof(IRepository<>), typeof(BaseRepository<>));
        services.AddScoped<IQuestionRepository, QuestionRepository>();
        services.AddScoped<IExamRepository, ExamRepository>();
        services.AddScoped<IExamTokenRepository, ExamTokenRepository>();
        services.AddScoped<IExamResultRepository, ExamResultRepository>();
        services.AddScoped<IQuestionGenerationJobRepository, QuestionGenerationJobRepository>();
        services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();

        // Services
        services.Configure<EmailSettings>(configuration.GetSection("Email"));
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddScoped<ITokenService, TokenService>();

        // IA — generación de preguntas (Ollama local)
        services.Configure<OllamaSettings>(configuration.GetSection("Ollama"));
        services.AddScoped<IQuestionGenerationAiService>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<OllamaSettings>>().Value;
            var http = new HttpClient
            {
                BaseAddress = new Uri(settings.BaseUrl),
                Timeout = TimeSpan.FromMinutes(5) // modelo local CPU-bound, puede tardar
            };
            var logger = sp.GetRequiredService<ILogger<OllamaQuestionGenerationService>>();
            return new OllamaQuestionGenerationService(http, Options.Create(settings), logger);
        });

        return services;
    }
}
