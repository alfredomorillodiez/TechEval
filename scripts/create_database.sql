-- ============================================================
-- TechEval Platform — Script de creación de base de datos
-- SQL Server 2019 / 2022
--
-- Uso:
--   1. Ejecutar en SQL Server Management Studio (SSMS) o
--      sqlcmd -S <servidor> -i scripts/create_database.sql
--   2. Alternativa a las migraciones de EF Core.
--      Si ya usas "dotnet ef database update", no ejecutes esto.
--
-- Contraseña admin por defecto: Admin@123!
--   (SHA-256 hex: 6cf0ea55e5fd5e692e007b16339a83f4319370cdb8b6193c1630820119cbba50)
--   Cámbiala en producción: UPDATE Users SET PasswordHash = '<nuevo_hash>' WHERE Email = 'admin@techeval.com'
-- ============================================================

SET QUOTED_IDENTIFIER ON;
GO

USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'TechEvalDb')
BEGIN
    CREATE DATABASE TechEvalDb
        COLLATE SQL_Latin1_General_CP1_CI_AS;
    PRINT 'Base de datos TechEvalDb creada.';
END
ELSE
    PRINT 'La base de datos TechEvalDb ya existe.';
GO

USE TechEvalDb;
GO

-- ============================================================
-- Eliminar tablas (orden inverso de dependencias)
-- ============================================================
IF OBJECT_ID('dbo.QuestionGenerationJobItems', 'U') IS NOT NULL DROP TABLE dbo.QuestionGenerationJobItems;
IF OBJECT_ID('dbo.QuestionGenerationJobs',     'U') IS NOT NULL DROP TABLE dbo.QuestionGenerationJobs;
IF OBJECT_ID('dbo.ExamResults',   'U') IS NOT NULL DROP TABLE dbo.ExamResults;
IF OBJECT_ID('dbo.UserAnswers',   'U') IS NOT NULL DROP TABLE dbo.UserAnswers;
IF OBJECT_ID('dbo.ExamSessions',  'U') IS NOT NULL DROP TABLE dbo.ExamSessions;
IF OBJECT_ID('dbo.ExamTokens',    'U') IS NOT NULL DROP TABLE dbo.ExamTokens;
IF OBJECT_ID('dbo.ExamQuestions', 'U') IS NOT NULL DROP TABLE dbo.ExamQuestions;
IF OBJECT_ID('dbo.Exams',         'U') IS NOT NULL DROP TABLE dbo.Exams;
IF OBJECT_ID('dbo.Answers',       'U') IS NOT NULL DROP TABLE dbo.Answers;
IF OBJECT_ID('dbo.Questions',     'U') IS NOT NULL DROP TABLE dbo.Questions;
IF OBJECT_ID('dbo.Categories',    'U') IS NOT NULL DROP TABLE dbo.Categories;
IF OBJECT_ID('dbo.Users',         'U') IS NOT NULL DROP TABLE dbo.Users;
GO

-- ============================================================
-- 1. Users
-- ============================================================
CREATE TABLE dbo.Users (
    Id           INT           NOT NULL IDENTITY(1,1),
    Email        NVARCHAR(200) NOT NULL,
    Username     NVARCHAR(200) NULL,        -- alumnos: parte local del email (ej. alejandro.robles)
    PasswordHash NVARCHAR(MAX) NOT NULL,   -- SHA-256 hex lowercase
    Name         NVARCHAR(200) NOT NULL,
    IsAdmin      BIT           NOT NULL CONSTRAINT DF_Users_IsAdmin    DEFAULT 0,
    IsActive     BIT           NOT NULL CONSTRAINT DF_Users_IsActive   DEFAULT 1,
    CreatedAt    DATETIME2     NOT NULL CONSTRAINT DF_Users_CreatedAt  DEFAULT GETUTCDATE(),
    UpdatedAt    DATETIME2     NULL,

    CONSTRAINT PK_Users PRIMARY KEY (Id)
);
GO

CREATE UNIQUE INDEX IX_Users_Email ON dbo.Users (Email);
CREATE UNIQUE INDEX IX_Users_Username ON dbo.Users (Username) WHERE Username IS NOT NULL;
GO

-- ============================================================
-- 2. Categories
-- ============================================================
CREATE TABLE dbo.Categories (
    Id          INT           NOT NULL IDENTITY(1,1),
    Name        NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500) NOT NULL CONSTRAINT DF_Categories_Desc DEFAULT '',
    IsActive    BIT           NOT NULL CONSTRAINT DF_Categories_IsActive  DEFAULT 1,
    CreatedAt   DATETIME2     NOT NULL CONSTRAINT DF_Categories_CreatedAt DEFAULT GETUTCDATE(),
    UpdatedAt   DATETIME2     NULL,

    CONSTRAINT PK_Categories PRIMARY KEY (Id)
);
GO

CREATE UNIQUE INDEX IX_Categories_Name ON dbo.Categories (Name);
GO

-- ============================================================
-- 3. Questions
--    Type:               1 = MultipleChoice | 2 = OpenEnded
--    Difficulty:         1 = Basic | 2 = Intermediate | 3 = Advanced
--    QuestionReviewStatus: 1 = Approved | 2 = PendingReview | 3 = Rejected
-- ============================================================
CREATE TABLE dbo.Questions (
    Id                   INT            NOT NULL IDENTITY(1,1),
    Text                 NVARCHAR(2000) NOT NULL,
    Type                 INT            NOT NULL,
    Difficulty           INT            NOT NULL,
    CategoryId           INT            NOT NULL,
    Points               INT            NOT NULL CONSTRAINT DF_Questions_Points    DEFAULT 1,
    IsActive             BIT            NOT NULL CONSTRAINT DF_Questions_IsActive  DEFAULT 1,
    QuestionReviewStatus INT            NOT NULL CONSTRAINT DF_Questions_ReviewStatus DEFAULT 1,
    SampleAnswer         NVARCHAR(4000) NULL,       -- Respuesta de referencia para preguntas abiertas
    CreatedAt            DATETIME2      NOT NULL CONSTRAINT DF_Questions_CreatedAt DEFAULT GETUTCDATE(),
    UpdatedAt            DATETIME2      NULL,

    CONSTRAINT PK_Questions PRIMARY KEY (Id),
    CONSTRAINT FK_Questions_Categories FOREIGN KEY (CategoryId)
        REFERENCES dbo.Categories (Id) ON DELETE NO ACTION ON UPDATE NO ACTION
);
GO

CREATE INDEX IX_Questions_CategoryId     ON dbo.Questions (CategoryId);
CREATE INDEX IX_Questions_Difficulty     ON dbo.Questions (Difficulty);
CREATE INDEX IX_Questions_IsActive       ON dbo.Questions (IsActive);
CREATE INDEX IX_Questions_ReviewStatus   ON dbo.Questions (QuestionReviewStatus);
GO

-- ============================================================
-- 4. Answers
-- ============================================================
CREATE TABLE dbo.Answers (
    Id         INT            NOT NULL IDENTITY(1,1),
    QuestionId INT            NOT NULL,
    Text       NVARCHAR(1000) NOT NULL,
    IsCorrect  BIT            NOT NULL CONSTRAINT DF_Answers_IsCorrect DEFAULT 0,
    [Order]    INT            NOT NULL CONSTRAINT DF_Answers_Order     DEFAULT 0,

    CONSTRAINT PK_Answers PRIMARY KEY (Id),
    CONSTRAINT FK_Answers_Questions FOREIGN KEY (QuestionId)
        REFERENCES dbo.Questions (Id) ON DELETE CASCADE ON UPDATE NO ACTION
);
GO

-- ============================================================
-- 5. Exams
-- ============================================================
CREATE TABLE dbo.Exams (
    Id                     INT            NOT NULL IDENTITY(1,1),
    Title                  NVARCHAR(200)  NOT NULL,
    Description            NVARCHAR(1000) NOT NULL CONSTRAINT DF_Exams_Desc    DEFAULT '',
    TimeLimitMinutes       INT            NOT NULL CONSTRAINT DF_Exams_Time    DEFAULT 60,
    PassingScorePercentage INT            NOT NULL CONSTRAINT DF_Exams_Passing DEFAULT 70,
    IsActive               BIT            NOT NULL CONSTRAINT DF_Exams_IsActive DEFAULT 1,
    CreatedByUserId        INT            NOT NULL,
    CreatedAt              DATETIME2      NOT NULL CONSTRAINT DF_Exams_CreatedAt DEFAULT GETUTCDATE(),
    UpdatedAt              DATETIME2      NULL,

    CONSTRAINT PK_Exams PRIMARY KEY (Id),
    CONSTRAINT FK_Exams_Users FOREIGN KEY (CreatedByUserId)
        REFERENCES dbo.Users (Id) ON DELETE NO ACTION ON UPDATE NO ACTION
);
GO

-- ============================================================
-- 6. ExamQuestions  (tabla pivote Examen ↔ Pregunta)
-- ============================================================
CREATE TABLE dbo.ExamQuestions (
    Id         INT NOT NULL IDENTITY(1,1),
    ExamId     INT NOT NULL,
    QuestionId INT NOT NULL,
    [Order]    INT NOT NULL CONSTRAINT DF_ExamQuestions_Order DEFAULT 0,

    CONSTRAINT PK_ExamQuestions PRIMARY KEY (Id),
    CONSTRAINT FK_ExamQuestions_Exams FOREIGN KEY (ExamId)
        REFERENCES dbo.Exams (Id) ON DELETE CASCADE ON UPDATE NO ACTION,
    CONSTRAINT FK_ExamQuestions_Questions FOREIGN KEY (QuestionId)
        REFERENCES dbo.Questions (Id) ON DELETE NO ACTION ON UPDATE NO ACTION
);
GO

CREATE UNIQUE INDEX IX_ExamQuestions_ExamId_QuestionId ON dbo.ExamQuestions (ExamId, QuestionId);
GO

-- ============================================================
-- 7. ExamTokens  (enlace de un solo uso enviado al candidato)
-- ============================================================
CREATE TABLE dbo.ExamTokens (
    Id             INT           NOT NULL IDENTITY(1,1),
    Token          NVARCHAR(200) NOT NULL,   -- 64 chars Base64 URL-safe
    ExamId         INT           NOT NULL,
    CandidateName  NVARCHAR(200) NOT NULL,
    CandidateEmail NVARCHAR(200) NOT NULL,
    UserId         INT           NULL,       -- resuelto/creado la primera vez que se abre el enlace
    CreatedAt      DATETIME2     NOT NULL CONSTRAINT DF_ExamTokens_CreatedAt DEFAULT GETUTCDATE(),
    ExpiresAt      DATETIME2     NOT NULL,
    IsUsed         BIT           NOT NULL CONSTRAINT DF_ExamTokens_IsUsed DEFAULT 0,
    UsedAt         DATETIME2     NULL,

    CONSTRAINT PK_ExamTokens PRIMARY KEY (Id),
    CONSTRAINT FK_ExamTokens_Exams FOREIGN KEY (ExamId)
        REFERENCES dbo.Exams (Id) ON DELETE NO ACTION ON UPDATE NO ACTION,
    CONSTRAINT FK_ExamTokens_Users FOREIGN KEY (UserId)
        REFERENCES dbo.Users (Id) ON DELETE SET NULL ON UPDATE NO ACTION
);
GO

CREATE UNIQUE INDEX IX_ExamTokens_Token ON dbo.ExamTokens (Token);
CREATE INDEX IX_ExamTokens_UserId ON dbo.ExamTokens (UserId);
GO

-- ============================================================
-- 8. ExamSessions  (sesión activa de un candidato)
--    Status: 1=InProgress | 2=Completed | 3=Expired | 4=Abandoned
-- ============================================================
CREATE TABLE dbo.ExamSessions (
    Id          INT       NOT NULL IDENTITY(1,1),
    ExamTokenId INT       NOT NULL,
    StartedAt   DATETIME2 NOT NULL CONSTRAINT DF_ExamSessions_StartedAt DEFAULT GETUTCDATE(),
    CompletedAt DATETIME2 NULL,
    Status      INT       NOT NULL CONSTRAINT DF_ExamSessions_Status DEFAULT 1,

    CONSTRAINT PK_ExamSessions PRIMARY KEY (Id),
    CONSTRAINT FK_ExamSessions_ExamTokens FOREIGN KEY (ExamTokenId)
        REFERENCES dbo.ExamTokens (Id) ON DELETE CASCADE ON UPDATE NO ACTION
);
GO

-- 1 token → máx. 1 sesión
CREATE UNIQUE INDEX IX_ExamSessions_ExamTokenId ON dbo.ExamSessions (ExamTokenId);
GO

-- ============================================================
-- 9. UserAnswers  (respuestas del candidato, con auto-guardado)
-- ============================================================
CREATE TABLE dbo.UserAnswers (
    Id               INT            NOT NULL IDENTITY(1,1),
    ExamSessionId    INT            NOT NULL,
    QuestionId       INT            NOT NULL,
    SelectedAnswerId INT            NULL,       -- NULL para preguntas abiertas
    OpenAnswer       NVARCHAR(4000) NULL,       -- NULL para preguntas tipo test
    IsCorrect        BIT            NULL,       -- NULL hasta corrección (abiertas)
    AnsweredAt       DATETIME2      NOT NULL CONSTRAINT DF_UserAnswers_AnsweredAt DEFAULT GETUTCDATE(),

    CONSTRAINT PK_UserAnswers PRIMARY KEY (Id),
    CONSTRAINT FK_UserAnswers_ExamSessions FOREIGN KEY (ExamSessionId)
        REFERENCES dbo.ExamSessions (Id) ON DELETE CASCADE ON UPDATE NO ACTION,
    CONSTRAINT FK_UserAnswers_Questions FOREIGN KEY (QuestionId)
        REFERENCES dbo.Questions (Id) ON DELETE NO ACTION ON UPDATE NO ACTION,
    CONSTRAINT FK_UserAnswers_Answers FOREIGN KEY (SelectedAnswerId)
        REFERENCES dbo.Answers (Id) ON DELETE NO ACTION ON UPDATE NO ACTION
);
GO

-- ============================================================
-- 10. ExamResults  (resultado final, 1:1 con ExamSession)
-- ============================================================
CREATE TABLE dbo.ExamResults (
    Id              INT           NOT NULL IDENTITY(1,1),
    ExamSessionId   INT           NOT NULL,
    ExamId          INT           NOT NULL,
    CandidateName   NVARCHAR(200) NOT NULL,
    CandidateEmail  NVARCHAR(200) NOT NULL,
    UserId          INT           NULL,       -- alumno dueño de la sesión
    TotalPoints     INT           NOT NULL CONSTRAINT DF_ExamResults_TotalPoints    DEFAULT 0,
    ObtainedPoints  INT           NOT NULL CONSTRAINT DF_ExamResults_ObtainedPoints DEFAULT 0,
    ScorePercentage DECIMAL(5,2)  NOT NULL CONSTRAINT DF_ExamResults_Score         DEFAULT 0,
    Passed          BIT           NOT NULL CONSTRAINT DF_ExamResults_Passed        DEFAULT 0,
    CompletedAt     DATETIME2     NOT NULL CONSTRAINT DF_ExamResults_CompletedAt   DEFAULT GETUTCDATE(),

    CONSTRAINT PK_ExamResults PRIMARY KEY (Id),
    CONSTRAINT FK_ExamResults_ExamSessions FOREIGN KEY (ExamSessionId)
        REFERENCES dbo.ExamSessions (Id) ON DELETE CASCADE ON UPDATE NO ACTION,
    CONSTRAINT FK_ExamResults_Exams FOREIGN KEY (ExamId)
        REFERENCES dbo.Exams (Id) ON DELETE NO ACTION ON UPDATE NO ACTION,
    CONSTRAINT FK_ExamResults_Users FOREIGN KEY (UserId)
        REFERENCES dbo.Users (Id) ON DELETE SET NULL ON UPDATE NO ACTION
);
GO

-- 1 sesión → máx. 1 resultado
CREATE UNIQUE INDEX IX_ExamResults_ExamSessionId ON dbo.ExamResults (ExamSessionId);
CREATE INDEX IX_ExamResults_CandidateEmail       ON dbo.ExamResults (CandidateEmail);
CREATE INDEX IX_ExamResults_ExamId               ON dbo.ExamResults (ExamId);
CREATE INDEX IX_ExamResults_CompletedAt          ON dbo.ExamResults (CompletedAt);
CREATE INDEX IX_ExamResults_UserId               ON dbo.ExamResults (UserId);
GO

-- ============================================================
-- 11. QuestionGenerationJobs  (solicitud de generación de preguntas por IA)
--    Difficulty: 1=Basic | 2=Intermediate | 3=Advanced
--    Type:       1=MultipleChoice | 2=OpenEnded
--    Status:     1=Queued | 2=Running | 3=Completed | 4=Failed
-- ============================================================
CREATE TABLE dbo.QuestionGenerationJobs (
    Id              INT           NOT NULL IDENTITY(1,1),
    CategoryId      INT           NOT NULL,
    Difficulty      INT           NOT NULL,
    Type            INT           NOT NULL,
    Topic           NVARCHAR(500) NOT NULL,
    RequestedCount  INT           NOT NULL,
    Status          INT           NOT NULL CONSTRAINT DF_QGJobs_Status DEFAULT 1,
    CreatedByUserId INT           NOT NULL,
    CompletedAt     DATETIME2     NULL,
    CreatedAt       DATETIME2     NOT NULL CONSTRAINT DF_QGJobs_CreatedAt DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2     NULL,

    CONSTRAINT PK_QuestionGenerationJobs PRIMARY KEY (Id),
    CONSTRAINT FK_QGJobs_Categories FOREIGN KEY (CategoryId)
        REFERENCES dbo.Categories (Id) ON DELETE NO ACTION ON UPDATE NO ACTION,
    CONSTRAINT FK_QGJobs_Users FOREIGN KEY (CreatedByUserId)
        REFERENCES dbo.Users (Id) ON DELETE NO ACTION ON UPDATE NO ACTION
);
GO

CREATE INDEX IX_QGJobs_Status ON dbo.QuestionGenerationJobs (Status);
GO

-- ============================================================
-- 12. QuestionGenerationJobItems  (una fila por pregunta solicitada dentro de un job)
--    Status: 1=Pending | 2=Succeeded | 3=Failed
-- ============================================================
CREATE TABLE dbo.QuestionGenerationJobItems (
    Id           INT           NOT NULL IDENTITY(1,1),
    JobId        INT           NOT NULL,
    Status       INT           NOT NULL CONSTRAINT DF_QGJobItems_Status DEFAULT 1,
    QuestionId   INT           NULL,
    ErrorMessage NVARCHAR(2000) NULL,

    CONSTRAINT PK_QuestionGenerationJobItems PRIMARY KEY (Id),
    CONSTRAINT FK_QGJobItems_Jobs FOREIGN KEY (JobId)
        REFERENCES dbo.QuestionGenerationJobs (Id) ON DELETE CASCADE ON UPDATE NO ACTION,
    CONSTRAINT FK_QGJobItems_Questions FOREIGN KEY (QuestionId)
        REFERENCES dbo.Questions (Id) ON DELETE SET NULL ON UPDATE NO ACTION
);
GO

CREATE INDEX IX_QGJobItems_Status ON dbo.QuestionGenerationJobItems (Status);
CREATE INDEX IX_QGJobItems_JobId  ON dbo.QuestionGenerationJobItems (JobId);
GO

-- ============================================================
-- DATOS INICIALES (SEED)
-- ============================================================

-- ── Usuario administrador ──────────────────────────────────
-- Contraseña: Admin@123!
-- Hash SHA-256: 6cf0ea55e5fd5e692e007b16339a83f4319370cdb8b6193c1630820119cbba50
-- Para generar un hash diferente (PowerShell):
--   $p = "NuevaContraseña"; [BitConverter]::ToString(
--     [System.Security.Cryptography.SHA256]::Create().ComputeHash(
--       [Text.Encoding]::UTF8.GetBytes($p))).Replace("-","").ToLower()
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = 'admin@techeval.com')
BEGIN
    INSERT INTO dbo.Users (Email, PasswordHash, Name, IsAdmin, IsActive)
    VALUES (
        'admin@techeval.com',
        '6cf0ea55e5fd5e692e007b16339a83f4319370cdb8b6193c1630820119cbba50',
        'Administrador',
        1, 1
    );
    PRINT 'Usuario admin creado.';
END
GO

-- ── Categorías ────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM dbo.Categories)
BEGIN
    INSERT INTO dbo.Categories (Name, Description)
    VALUES
        ('SQL',          'Consultas, diseño de BD, optimización'),
        ('C#',           'Programación orientada a objetos, LINQ, async'),
        ('APIs REST',    'Diseño de APIs, HTTP, autenticación'),
        ('Arquitectura', 'Patrones de diseño, Clean Architecture, SOLID'),
        ('DevOps',       'Docker, CI/CD, despliegue');
    PRINT '5 categorías creadas.';
END
GO

-- ── Preguntas de ejemplo ───────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM dbo.Questions)
BEGIN
    DECLARE @sqlCat INT = (SELECT Id FROM dbo.Categories WHERE Name = 'SQL');
    DECLARE @csCat  INT = (SELECT Id FROM dbo.Categories WHERE Name = 'C#');

    -- Pregunta 1: SQL tipo test (básica)
    INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points)
    VALUES ('¿Qué cláusula SQL se usa para filtrar grupos de registros?', 1, 1, @sqlCat, 1);

    DECLARE @q1 INT = SCOPE_IDENTITY();
    INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
        (@q1, 'WHERE',    0, 1),
        (@q1, 'HAVING',   1, 2),
        (@q1, 'GROUP BY', 0, 3),
        (@q1, 'ORDER BY', 0, 4);

    -- Pregunta 2: SQL abierta (intermedia)
    INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, SampleAnswer)
    VALUES (
        '¿Cuál es la diferencia entre INNER JOIN y LEFT JOIN?',
        2, 2, @sqlCat, 3,
        'INNER JOIN devuelve solo las filas que tienen coincidencia en ambas tablas. ' +
        'LEFT JOIN devuelve todas las filas de la tabla izquierda y las coincidencias ' +
        'de la derecha (NULL si no hay coincidencia).'
    );

    -- Pregunta 3: C# tipo test (básica)
    INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points)
    VALUES ('¿Qué palabra clave de C# permite ejecutar código de forma asíncrona sin bloquear el hilo?',
            1, 1, @csCat, 1);

    DECLARE @q3 INT = SCOPE_IDENTITY();
    INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
        (@q3, 'parallel',   0, 1),
        (@q3, 'await',      1, 2),
        (@q3, 'async only', 0, 3),
        (@q3, 'thread',     0, 4);

    PRINT '3 preguntas de ejemplo creadas.';
END
GO

PRINT '✓ TechEvalDb lista para usar.';
GO
