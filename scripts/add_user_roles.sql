-- Cambio aditivo (user-roles): añade el rol de usuario, el sello de seguridad y la tabla
-- de enlaces para fijar la contraseña en una base TechEvalDb ya existente, sin perder
-- ninguna fila. Idempotente: puede reejecutarse sin error.
--
-- El rol se rellena desde IsAdmin (1 -> Admin, 0 -> Alumno) y después IsAdmin se quita,
-- para que el rol tenga una sola fuente. Por eso este guion y el binario nuevo se
-- despliegan juntos: el binario anterior ya no arranca contra la base actualizada.
--
-- ANTES DE EJECUTARLO: los tokens emitidos por la versión anterior dejan de valer. Un
-- candidato con una prueba abierta pierde el guardado hasta volver a abrir su enlace.
-- Despliega cuando esta consulta devuelva 0:
--
--   SELECT COUNT(*) FROM dbo.ExamSessions WHERE Status = 1;   -- 1 = InProgress
--
-- Vuelta atrás, con la API parada y antes de desplegar el binario anterior. Recrea
-- IsAdmin y da a Role un valor por defecto, porque el binario anterior crea alumnos sin
-- indicar el rol. El resto de columnas y la tabla nueva no le molestan.
--
--   ALTER TABLE dbo.Users ADD IsAdmin BIT NOT NULL CONSTRAINT DF_Users_IsAdmin DEFAULT 0;
--   GO
--   UPDATE dbo.Users SET IsAdmin = CASE WHEN Role = 1 THEN 1 ELSE 0 END;
--   ALTER TABLE dbo.Users ADD CONSTRAINT DF_Users_Role DEFAULT 3 FOR Role;
--   GO
SET QUOTED_IDENTIFIER ON;
GO

USE TechEvalDb;
GO

-- 1. Rol, primero admitiendo NULL para poder rellenarlo.
IF COL_LENGTH('dbo.Users','Role') IS NULL
    ALTER TABLE dbo.Users ADD Role INT NULL;
GO

-- 2. Relleno desde IsAdmin. SQL dinámico porque SQL Server compila el lote entero, y en
-- la segunda ejecución IsAdmin ya no existe.
IF COL_LENGTH('dbo.Users','IsAdmin') IS NOT NULL
    EXEC sp_executesql N'
        UPDATE dbo.Users
        SET Role = CASE WHEN IsAdmin = 1 THEN 1 ELSE 3 END
        WHERE Role IS NULL;';
GO

-- 3. Obligatorio y acotado a los tres valores del enumerado.
ALTER TABLE dbo.Users ALTER COLUMN Role INT NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Users_Role')
    ALTER TABLE dbo.Users ADD CONSTRAINT CK_Users_Role CHECK (Role IN (1, 2, 3));
GO

-- 4. Sello de seguridad. NEWID() se evalúa por fila: cada usuario existente recibe el suyo.
IF COL_LENGTH('dbo.Users','SecurityStamp') IS NULL
    ALTER TABLE dbo.Users ADD SecurityStamp UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_Users_SecurityStamp DEFAULT NEWID();
GO

-- 5. Enlaces para fijar la contraseña. Solo el SHA-256 del token, nunca el token.
IF OBJECT_ID('dbo.PasswordSetupTokens', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PasswordSetupTokens (
        Id        INT       NOT NULL IDENTITY(1,1),
        UserId    INT       NOT NULL,
        TokenHash NCHAR(64) NOT NULL,    -- SHA-256 hexadecimal del token del enlace
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_PasswordSetupTokens_CreatedAt DEFAULT GETUTCDATE(),
        ExpiresAt DATETIME2 NOT NULL,
        UsedAt    DATETIME2 NULL,

        CONSTRAINT PK_PasswordSetupTokens PRIMARY KEY (Id),
        CONSTRAINT FK_PasswordSetupTokens_Users FOREIGN KEY (UserId)
            REFERENCES dbo.Users (Id) ON DELETE CASCADE ON UPDATE NO ACTION
    );

    CREATE UNIQUE INDEX IX_PasswordSetupTokens_TokenHash ON dbo.PasswordSetupTokens (TokenHash);
    CREATE INDEX IX_PasswordSetupTokens_UserId ON dbo.PasswordSetupTokens (UserId);
END
GO

-- 6. Fuera IsAdmin. Su restricción por defecto se busca por columna y no por nombre: una
-- base creada por EF la tiene con un nombre generado.
IF COL_LENGTH('dbo.Users','IsAdmin') IS NOT NULL
BEGIN
    DECLARE @df SYSNAME = (
        SELECT dc.name
        FROM sys.default_constraints dc
        JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.Users') AND c.name = 'IsAdmin');

    IF @df IS NOT NULL
    BEGIN
        DECLARE @dropDf NVARCHAR(400) = N'ALTER TABLE dbo.Users DROP CONSTRAINT ' + QUOTENAME(@df);
        EXEC sp_executesql @dropDf;
    END

    EXEC sp_executesql N'ALTER TABLE dbo.Users DROP COLUMN IsAdmin;';
END
GO

PRINT 'Users tiene ya rol, sello de seguridad y enlaces para fijar la contrasena (sin perdida de datos).';
GO
