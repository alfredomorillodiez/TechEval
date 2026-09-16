using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TechEval.Domain.Entities;
using TechEval.Domain.Interfaces.Repositories;
using TechEval.Domain.Interfaces.Services;
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
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(BaseRepository<>));
        services.AddScoped<IQuestionRepository, QuestionRepository>();
        services.AddScoped<IExamRepository, ExamRepository>();
        services.AddScoped<IExamTokenRepository, ExamTokenRepository>();
        services.AddScoped<IExamResultRepository, ExamResultRepository>();

        // Services
        services.Configure<EmailSettings>(configuration.GetSection("Email"));
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddScoped<ITokenService, TokenService>();

        return services;
    }
}
