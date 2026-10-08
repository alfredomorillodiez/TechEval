-- Cambio aditivo, no destructivo: añade las columnas de corrección manual de preguntas
-- abiertas (manual-review-open-questions) a una base de datos TechEvalDb ya existente,
-- sin borrar ningún dato. Idempotente: puede reejecutarse sin error.
USE TechEvalDb;
GO

-- ExamResults.Status: 1 = PendingReview, 2 = Reviewed.
-- Los resultados históricos son evaluaciones ya cerradas de hecho, así que reciben
-- Reviewed: marcarlos como pendientes los volcaría de golpe en la cola de corrección
-- y distorsionaría las métricas del dashboard.
IF COL_LENGTH('dbo.ExamResults','Status') IS NULL
BEGIN
    ALTER TABLE dbo.ExamResults ADD Status INT NOT NULL CONSTRAINT DF_ExamResults_Status DEFAULT (2);
END
GO

IF COL_LENGTH('dbo.ExamResults','ReviewedAt') IS NULL
    ALTER TABLE dbo.ExamResults ADD ReviewedAt DATETIME2 NULL;
GO

IF COL_LENGTH('dbo.ExamResults','ReviewedByUserId') IS NULL
    ALTER TABLE dbo.ExamResults ADD ReviewedByUserId INT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ExamResults_ReviewedByUser')
    ALTER TABLE dbo.ExamResults ADD CONSTRAINT FK_ExamResults_ReviewedByUser
        FOREIGN KEY (ReviewedByUserId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ExamResults_ReviewedByUserId')
    CREATE INDEX IX_ExamResults_ReviewedByUserId ON dbo.ExamResults (ReviewedByUserId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ExamResults_Status')
    CREATE INDEX IX_ExamResults_Status ON dbo.ExamResults (Status);
GO

-- Passed pasa a admitir NULL: un resultado pendiente de corrección no tiene veredicto.
-- Los resultados históricos conservan su true/false, que sigue siendo válido.
IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID('dbo.ExamResults') AND name = 'Passed' AND is_nullable = 0)
    ALTER TABLE dbo.ExamResults ALTER COLUMN Passed BIT NULL;
GO

-- UserAnswers: puntuación otorgada y comentario del corrector.
IF COL_LENGTH('dbo.UserAnswers','AwardedPoints') IS NULL
    ALTER TABLE dbo.UserAnswers ADD AwardedPoints INT NULL;
GO

IF COL_LENGTH('dbo.UserAnswers','ReviewerComment') IS NULL
    ALTER TABLE dbo.UserAnswers ADD ReviewerComment NVARCHAR(2000) NULL;
GO

-- Retrocompatibilidad de las respuestas históricas: AwardedPoints es la fuente de verdad
-- de ObtainedPoints a partir de ahora, así que las respuestas ya existentes se rellenan
-- con lo que valieron en su momento. Las abiertas antiguas nunca puntuaron: quedan a 0.
UPDATE ua
SET ua.AwardedPoints = CASE WHEN ua.IsCorrect = 1 THEN q.Points ELSE 0 END
FROM dbo.UserAnswers ua
INNER JOIN dbo.Questions q ON q.Id = ua.QuestionId
WHERE ua.AwardedPoints IS NULL;
GO

PRINT 'Esquema actualizado con las columnas de corrección manual (sin pérdida de datos).';
GO
