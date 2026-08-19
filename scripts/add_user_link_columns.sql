-- Cambio aditivo, no destructivo: añade las columnas nuevas de student-user-accounts
-- a una base de datos TechEvalDb ya existente, sin borrar ningún dato.
USE TechEvalDb;
GO

IF COL_LENGTH('dbo.Users','Username') IS NULL
    ALTER TABLE dbo.Users ADD Username NVARCHAR(200) NULL;
GO

SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_Username')
    CREATE UNIQUE INDEX IX_Users_Username ON dbo.Users (Username) WHERE Username IS NOT NULL;
GO

IF COL_LENGTH('dbo.ExamTokens','UserId') IS NULL
    ALTER TABLE dbo.ExamTokens ADD UserId INT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ExamTokens_Users')
    ALTER TABLE dbo.ExamTokens ADD CONSTRAINT FK_ExamTokens_Users
        FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE SET NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ExamTokens_UserId')
    CREATE INDEX IX_ExamTokens_UserId ON dbo.ExamTokens (UserId);
GO

IF COL_LENGTH('dbo.ExamResults','UserId') IS NULL
    ALTER TABLE dbo.ExamResults ADD UserId INT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ExamResults_Users')
    ALTER TABLE dbo.ExamResults ADD CONSTRAINT FK_ExamResults_Users
        FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE SET NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ExamResults_UserId')
    CREATE INDEX IX_ExamResults_UserId ON dbo.ExamResults (UserId);
GO

PRINT 'Esquema actualizado (sin pérdida de datos).';
GO
