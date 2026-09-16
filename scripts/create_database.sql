-- ============================================================
-- TechEval Platform — Script de creación de base de datos
-- SQL Server 2019 / 2022
--
-- Uso:
--   1. Ejecutar en SQL Server Management Studio (SSMS) o
--      sqlcmd -S <servidor> -i scripts/create_database.sql
--   2. Es la UNICA forma de crear el esquema. La aplicacion ya no lo crea al arrancar,
--      y el proyecto no usa migraciones de EF Core: crearlo desde el modelo dejaba a
--      las migraciones sin su tabla de historial y por tanto sin poder aplicarse nunca.
--
-- Este guion NO crea el usuario administrador. Lo siembra la API en su primer arranque
-- a partir de `AdminPassword`, que no tiene valor por defecto. El hash es
-- PBKDF2-HMAC-SHA256 desde el 16-09-2026, asi que un hash SHA-256 escrito a mano aqui
-- quedaria en el formato antiguo hasta el primer inicio de sesion correcto.
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
    Id                INT           NOT NULL IDENTITY(1,1),
    Name              NVARCHAR(100) NOT NULL,
    Description       NVARCHAR(500) NOT NULL CONSTRAINT DF_Categories_Desc DEFAULT '',
    IsActive          BIT           NOT NULL CONSTRAINT DF_Categories_IsActive  DEFAULT 1,
    CreatedAt         DATETIME2     NOT NULL CONSTRAINT DF_Categories_CreatedAt DEFAULT GETUTCDATE(),
    UpdatedAt         DATETIME2     NULL,

    CONSTRAINT PK_Categories PRIMARY KEY (Id)
);
GO

CREATE UNIQUE INDEX IX_Categories_Name ON dbo.Categories (Name);
GO

-- ============================================================
-- 3. Questions
--    Type:               1 = MultipleChoice | 2 = OpenEnded
--    Difficulty:         1 = Basic | 2 = Intermediate | 3 = Advanced
-- ============================================================
CREATE TABLE dbo.Questions (
    Id                   INT            NOT NULL IDENTITY(1,1),
    Text                 NVARCHAR(2000) NOT NULL,
    Type                 INT            NOT NULL,
    Difficulty           INT            NOT NULL,
    CategoryId           INT            NOT NULL,
    Points               INT            NOT NULL CONSTRAINT DF_Questions_Points    DEFAULT 1,
    IsActive             BIT            NOT NULL CONSTRAINT DF_Questions_IsActive  DEFAULT 1,
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
    AwardedPoints    INT            NULL,       -- puntos congelados; NULL en abiertas sin corregir
    ReviewerComment  NVARCHAR(2000) NULL,       -- comentario del corrector (abiertas)

    -- Copia de lo que se le preguntó al candidato, congelada en el envío. Sin ella, editar
    -- una pregunta cambiaba hacia atrás lo que constaba en los exámenes ya cerrados.
    QuestionTextSnapshot       NVARCHAR(2000) NULL,
    SelectedAnswerTextSnapshot NVARCHAR(1000) NULL,
    CorrectAnswerTextSnapshot  NVARCHAR(1000) NULL,
    QuestionPointsSnapshot     INT            NULL,

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
    Passed          BIT           NULL,       -- NULL mientras esté pendiente de corrección
    Status          INT           NOT NULL CONSTRAINT DF_ExamResults_Status        DEFAULT 2, -- 1=PendingReview, 2=Reviewed
    ReviewedAt      DATETIME2     NULL,
    ReviewedByUserId INT          NULL,       -- administrador que corrigió las abiertas
    CompletedAt     DATETIME2     NOT NULL CONSTRAINT DF_ExamResults_CompletedAt   DEFAULT GETUTCDATE(),

    CONSTRAINT PK_ExamResults PRIMARY KEY (Id),
    CONSTRAINT FK_ExamResults_ExamSessions FOREIGN KEY (ExamSessionId)
        REFERENCES dbo.ExamSessions (Id) ON DELETE CASCADE ON UPDATE NO ACTION,
    CONSTRAINT FK_ExamResults_Exams FOREIGN KEY (ExamId)
        REFERENCES dbo.Exams (Id) ON DELETE NO ACTION ON UPDATE NO ACTION,
    CONSTRAINT FK_ExamResults_Users FOREIGN KEY (UserId)
        REFERENCES dbo.Users (Id) ON DELETE SET NULL ON UPDATE NO ACTION,
    CONSTRAINT FK_ExamResults_ReviewedByUser FOREIGN KEY (ReviewedByUserId)
        REFERENCES dbo.Users (Id) ON DELETE NO ACTION ON UPDATE NO ACTION
);
GO

-- 1 sesión → máx. 1 resultado
CREATE UNIQUE INDEX IX_ExamResults_ExamSessionId ON dbo.ExamResults (ExamSessionId);
CREATE INDEX IX_ExamResults_CandidateEmail       ON dbo.ExamResults (CandidateEmail);
CREATE INDEX IX_ExamResults_ExamId               ON dbo.ExamResults (ExamId);
CREATE INDEX IX_ExamResults_CompletedAt          ON dbo.ExamResults (CompletedAt);
CREATE INDEX IX_ExamResults_UserId               ON dbo.ExamResults (UserId);
CREATE INDEX IX_ExamResults_ReviewedByUserId     ON dbo.ExamResults (ReviewedByUserId);
CREATE INDEX IX_ExamResults_Status               ON dbo.ExamResults (Status);
GO

-- ============================================================
-- Este guion crea el ESQUEMA y nada mas.
--
-- Antes sembraba aqui un administrador con un hash SHA-256 de la contrasena publica de
-- desarrollo. Eso esquivaba la proteccion de secretos: como ya existia un usuario, la API
-- no llegaba a aplicar AdminPassword, y el despliegue quedaba con una cuenta cuya
-- contrasena conoce cualquiera que haya leido el repositorio.
--
-- Quien crea el administrador es la API, en su primer arranque, a partir de AdminPassword.
-- Fuera de desarrollo se niega a arrancar si esa clave falta o si conserva el valor
-- publicado para desarrollo.
--
-- Tambien sembraba cinco categorias y tres preguntas de ejemplo. En el banco de un cliente
-- eso es basura. En desarrollo las siembra la propia API.
--
-- Para cargar el banco de preguntas de verdad:
--   sqlcmd -S <servidor> -i scripts/seed_questions_examen.sql
-- ============================================================

PRINT 'Esquema de TechEvalDb creado. El administrador lo siembra la API al arrancar.';
GO
