-- Cambio aditivo (session-terms-and-abandonment): congela en cada sesion el tiempo limite y la
-- nota de corte con que empezo el candidato. Anade dos columnas NULL a dbo.ExamSessions en una
-- base TechEvalDb ya existente, sin tocar ninguna fila. Idempotente: puede reejecutarse.
--
-- Las sesiones que ya existen quedan con los dos valores a NULL. Es deliberado: NULL significa
-- «anterior al cambio» y esas sesiones siguen usando el tiempo y la nota actuales de la prueba,
-- que es lo que hacian. Copiar el valor actual de la prueba a sesiones ya empezadas seria
-- fingir que siempre tuvieron ese valor.
--
-- La API anterior sigue funcionando contra la base actualizada: ignora las columnas.
--
-- AL DESPLEGAR LA API NUEVA: su trabajo de sesiones abandonadas marca como Abandoned (Status = 4)
-- toda sesion en curso (Status = 1) sin resultado cuyo plazo vencio hace mas de 24 horas (valor
-- de Sessions:AbandonAfterHours). Para ver cuantas hay antes de desplegar:
--
--   SELECT COUNT(*) FROM dbo.ExamSessions s
--   WHERE s.Status = 1 AND NOT EXISTS (SELECT 1 FROM dbo.ExamResults r WHERE r.ExamSessionId = s.Id);
--
-- Hacer una copia de la base antes de ejecutarlo.
SET QUOTED_IDENTIFIER ON;
GO

USE TechEvalDb;
GO

IF COL_LENGTH('dbo.ExamSessions', 'TimeLimitMinutesSnapshot') IS NULL
    ALTER TABLE dbo.ExamSessions ADD TimeLimitMinutesSnapshot INT NULL;
GO

IF COL_LENGTH('dbo.ExamSessions', 'PassingScoreSnapshot') IS NULL
    ALTER TABLE dbo.ExamSessions ADD PassingScoreSnapshot INT NULL;
GO

PRINT 'ExamSessions guarda ya las condiciones con que empezo cada sesion (sin perdida de datos).';
GO

-- Vuelta atras, si se quiere. La API anterior ignora estas columnas, asi que no hace falta
-- para volver a ella.
--
-- ALTER TABLE dbo.ExamSessions DROP COLUMN TimeLimitMinutesSnapshot, PassingScoreSnapshot;
