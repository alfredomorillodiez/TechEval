-- Elimina el pipeline de generación de preguntas por IA local (Ollama) de una base de
-- datos TechEvalDb ya existente: las tablas QuestionGenerationJob(s), y las columnas
-- Category.AllowsAiGeneration y Question.QuestionReviewStatus, que ya no tienen ningún
-- consumidor en el código. Idempotente: puede reejecutarse sin error.
USE TechEvalDb;
GO

-- Preguntas nunca aprobadas (PendingReview o Rejected): antes de perder el campo que las
-- excluía de la selección, se dan de baja lógica (IsActive = 0) para que sigan sin
-- aparecer en el banco de preguntas ni en la generación aleatoria de exámenes.
IF COL_LENGTH('dbo.Questions', 'QuestionReviewStatus') IS NOT NULL
    UPDATE dbo.Questions
    SET IsActive = 0, UpdatedAt = GETUTCDATE()
    WHERE QuestionReviewStatus <> 1 AND IsActive = 1;
GO

-- Tablas de jobs de generación (orden inverso de dependencias: items referencia a jobs)
IF OBJECT_ID('dbo.QuestionGenerationJobItems', 'U') IS NOT NULL
    DROP TABLE dbo.QuestionGenerationJobItems;
GO

IF OBJECT_ID('dbo.QuestionGenerationJobs', 'U') IS NOT NULL
    DROP TABLE dbo.QuestionGenerationJobs;
GO

-- Category.AllowsAiGeneration
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_Categories_AllowsAi')
    ALTER TABLE dbo.Categories DROP CONSTRAINT DF_Categories_AllowsAi;
GO

IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_Categories_AllowsAiGeneration')
    ALTER TABLE dbo.Categories DROP CONSTRAINT DF_Categories_AllowsAiGeneration;
GO

IF COL_LENGTH('dbo.Categories', 'AllowsAiGeneration') IS NOT NULL
    ALTER TABLE dbo.Categories DROP COLUMN AllowsAiGeneration;
GO

-- Question.QuestionReviewStatus
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Questions_ReviewStatus')
    DROP INDEX IX_Questions_ReviewStatus ON dbo.Questions;
GO

IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_Questions_ReviewStatus')
    ALTER TABLE dbo.Questions DROP CONSTRAINT DF_Questions_ReviewStatus;
GO

IF COL_LENGTH('dbo.Questions', 'QuestionReviewStatus') IS NOT NULL
    ALTER TABLE dbo.Questions DROP COLUMN QuestionReviewStatus;
GO

PRINT 'Esquema actualizado: generación de preguntas por IA eliminada (tablas y columnas retiradas).';
GO
