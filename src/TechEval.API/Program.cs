using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using TechEval.API;
using TechEval.API.Middleware;
using TechEval.Application.Services;
using TechEval.Infrastructure;
using TechEval.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Antes que nada: un despliegue sin secretos para aquí, en vez de arrancar con los valores
// de desarrollo, que están publicados en el repositorio.
if (!builder.Environment.IsDevelopment())
    StartupSecrets.Validate(builder.Configuration);

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/techeval-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();
builder.Host.UseSerilog();

// Controllers
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Infrastructure (DB + Repos + Email + JWT Token)
builder.Services.AddInfrastructure(builder.Configuration);

// Application services
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IQuestionService, QuestionService>();
builder.Services.AddScoped<IExamService, ExamService>();
builder.Services.AddScoped<IExamTokenService, ExamTokenService>();
builder.Services.AddScoped<IResultService, ResultService>();
builder.Services.AddScoped<IStudentPortalService, StudentPortalService>();
builder.Services.AddScoped<IOpenQuestionReviewService, OpenQuestionReviewService>();

// JWT Auth
var jwtKey = builder.Configuration["Jwt:SecretKey"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// CORS para Blazor
builder.Services.AddCors(o => o.AddPolicy("BlazorPolicy", p =>
{
    if (builder.Environment.IsDevelopment())
        p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    else
    {
        var origins = (builder.Configuration["AllowedOrigins"] ?? "http://localhost:5001")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        p.WithOrigins(origins).AllowAnyMethod().AllowAnyHeader();
    }
}));

// Swagger
builder.Services.AddTechEvalRateLimiting();

// Detrás de un proxy inverso, todas las peticiones llegan con la dirección del proxy y el
// cupo se comparte entre todos los candidatos. Con la lista de proxies de confianza vacía
// —el valor por defecto— la cabecera se ignora, que es lo correcto sin proxy delante:
// confiar en `X-Forwarded-For` de cualquiera permite inventarse el origen y saltarse el
// límite. Ver el apartado del README sobre el despliegue detrás de un proxy.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownProxies.Clear();
    o.KnownNetworks.Clear();
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TechEval API",
        Version = "v1",
        Description = "Plataforma de evaluación técnica — API REST"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
    // Incluir comentarios XML
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// Middleware pipeline
app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "TechEval API v1"));
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseCors("BlazorPolicy");

// Antes de la autenticación: el rechazo por ritmo no debe costar ni una verificación de
// contraseña, que es justo el gasto del que protege.
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Seed DB on startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // Sin valor por defecto: fuera de desarrollo, StartupSecrets ya paró el arranque si
    // falta. En desarrollo lo trae appsettings.Development.json.
    var adminPassword = builder.Configuration["AdminPassword"]
        ?? throw new InvalidOperationException(
            "Falta `AdminPassword`. Sin ella no se puede sembrar el administrador.");

    await DbSeeder.SeedAsync(context, PasswordHasher.Hash(adminPassword));
}

app.Run();
