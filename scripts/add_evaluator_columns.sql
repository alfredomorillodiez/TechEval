-- Cambio aditivo (evaluator-review): añade la asignación de evaluadores a pruebas y la
-- reserva de un resultado mientras alguien lo corrige, en una base TechEvalDb ya existente,
-- sin perder ninguna fila. Idempotente: puede reejecutarse sin error.
--
-- Todos los resultados existentes quedan sin reserva. No retira ninguna columna: el
-- binario anterior sigue funcionando contra la base actualizada.
--
-- Vuelta atrás, si se quiere (el binario anterior no la necesita):
--
--   DROP TABLE IF EXISTS dbo.ExamEvaluators;
--   ALTER TABLE dbo.ExamResults DROP CONSTRAINT FK_ExamResults_ReservedByUser;
--   DROP INDEX IX_ExamResults_ReservedByUserId ON dbo.ExamResults;
--   ALTER TABLE dbo.ExamResults DROP COLUMN ReservedByUserId, ReservedUntil;
SET QUOTED_IDENTIFIER ON;
GO

USE TechEvalDb;
GO

IF COL_LENGTH('dbo.ExamResults','ReservedByUserId') IS NULL
    ALTER TABLE dbo.ExamResults ADD ReservedByUserId INT NULL;
GO

IF COL_LENGTH('dbo.ExamResults','ReservedUntil') IS NULL
    ALTER TABLE dbo.ExamResults ADD ReservedUntil DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ExamResults_ReservedByUser')
    ALTER TABLE dbo.ExamResults ADD CONSTRAINT FK_ExamResults_ReservedByUser
        FOREIGN KEY (ReservedByUserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION ON UPDATE NO ACTION;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ExamResults_ReservedByUserId')
    CREATE INDEX IX_ExamResults_ReservedByUserId ON dbo.ExamResults (ReservedByUserId);
GO

IF OBJECT_ID('dbo.ExamEvaluators', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ExamEvaluators (
        ExamId           INT       NOT NULL,
        UserId           INT       NOT NULL,   -- usuario con rol Evaluador
        AssignedAt       DATETIME2 NOT NULL CONSTRAINT DF_ExamEvaluators_AssignedAt DEFAULT GETUTCDATE(),
        AssignedByUserId INT       NOT NULL,   -- administrador que asignó

        CONSTRAINT PK_ExamEvaluators PRIMARY KEY (ExamId, UserId),
        CONSTRAINT FK_ExamEvaluators_Exams FOREIGN KEY (ExamId)
            REFERENCES dbo.Exams (Id) ON DELETE CASCADE ON UPDATE NO ACTION,
        CONSTRAINT FK_ExamEvaluators_Users FOREIGN KEY (UserId)
            REFERENCES dbo.Users (Id) ON DELETE NO ACTION ON UPDATE NO ACTION,
        CONSTRAINT FK_ExamEvaluators_AssignedBy FOREIGN KEY (AssignedByUserId)
            REFERENCES dbo.Users (Id) ON DELETE NO ACTION ON UPDATE NO ACTION
    );

    CREATE INDEX IX_ExamEvaluators_UserId ON dbo.ExamEvaluators (UserId);
END
GO

PRINT 'ExamEvaluators y la reserva de ExamResults listas (sin perdida de datos).';
GO
