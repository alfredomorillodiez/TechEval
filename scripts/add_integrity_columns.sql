-- Cambio aditivo, no destructivo: añade el orden propio de cada sesión y el registro de
-- señales de integridad (exam-integrity-signals) en una base TechEvalDb ya existente,
-- sin borrar ningún dato. Idempotente: puede reejecutarse sin error.
--
-- Las sesiones que ya existen quedan con ShuffleSeed a NULL. Eso es deliberado: una
-- sesión abierta en el momento del despliegue conserva el orden del examen y no cambia
-- de orden a mitad de la prueba, y el corrector la ve como anterior al registro.
SET QUOTED_IDENTIFIER ON;
GO

USE TechEvalDb;
GO

IF COL_LENGTH('dbo.ExamSessions','ShuffleSeed') IS NULL
    ALTER TABLE dbo.ExamSessions ADD ShuffleSeed INT NULL;
GO

IF COL_LENGTH('dbo.ExamSessions','IntegrityLimitReached') IS NULL
    ALTER TABLE dbo.ExamSessions ADD IntegrityLimitReached BIT NOT NULL
        CONSTRAINT DF_ExamSessions_IntegrityLimitReached DEFAULT 0;
GO

IF OBJECT_ID('dbo.ExamIntegrityEvents', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ExamIntegrityEvents (
        Id            INT       NOT NULL IDENTITY(1,1),
        ExamSessionId INT       NOT NULL,
        Type          INT       NOT NULL,   -- 1=PageLeft | 2=PageReturned | 3=Paste
        QuestionId    INT       NULL,       -- pregunta en pantalla; sin FK, es solo un dato
        OccurredAt    DATETIME2 NOT NULL CONSTRAINT DF_ExamIntegrityEvents_OccurredAt DEFAULT GETUTCDATE(),
        AwaySeconds   INT       NULL,       -- solo PageReturned, medido por el navegador
        PastedChars   INT       NULL,       -- solo Paste; el texto no se guarda

        CONSTRAINT PK_ExamIntegrityEvents PRIMARY KEY (Id),
        CONSTRAINT FK_ExamIntegrityEvents_ExamSessions FOREIGN KEY (ExamSessionId)
            REFERENCES dbo.ExamSessions (Id) ON DELETE CASCADE ON UPDATE NO ACTION
    );

    CREATE INDEX IX_ExamIntegrityEvents_ExamSessionId ON dbo.ExamIntegrityEvents (ExamSessionId);
END
GO

PRINT 'ExamSessions tiene ya orden propio y registro de senales (sin perdida de datos).';
GO

-- Vuelta atrás, si se quiere. La versión anterior de la aplicación ignora estas columnas
-- y esta tabla, así que no hace falta para volver a ella.
--
-- DROP TABLE IF EXISTS dbo.ExamIntegrityEvents;
-- ALTER TABLE dbo.ExamSessions DROP CONSTRAINT DF_ExamSessions_IntegrityLimitReached;
-- ALTER TABLE dbo.ExamSessions DROP COLUMN IntegrityLimitReached, ShuffleSeed;
