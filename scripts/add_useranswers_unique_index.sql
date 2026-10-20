-- Cambio aditivo (submission-hardening, D12): una sola respuesta por pregunta y sesión.
-- Crea el índice único sobre dbo.UserAnswers (ExamSessionId, QuestionId) y un índice sobre
-- QuestionId, en una base TechEvalDb ya existente, sin borrar ninguna fila. Idempotente:
-- puede reejecutarse sin error.
--
-- Sin la unicidad, dos autoguardados de la misma pregunta que coinciden en el tiempo creaban
-- dos filas, y la corrección sumaba los puntos de las dos: la pregunta se puntuaba dos veces.
--
-- SI LA BASE YA TIENE DUPLICADOS este guion NO los borra ni crea el índice único: lista las
-- parejas repetidas con sus Id y puntos, y termina con un error. Quien despliega decide cuál
-- fila conserva (las dos pueden tener puntos otorgados por un corrector), borra la sobrante
-- y vuelve a ejecutar el guion. Para ver los duplicados antes de ejecutarlo:
--
--   SELECT ExamSessionId, QuestionId, COUNT(*) AS Filas
--   FROM dbo.UserAnswers GROUP BY ExamSessionId, QuestionId HAVING COUNT(*) > 1;
--
-- La API anterior sigue funcionando contra la base actualizada: solo notaría el índice si
-- intentara insertar un duplicado, que es justo lo que se quiere impedir. Hacer una copia
-- de la base antes de ejecutarlo.
SET QUOTED_IDENTIFIER ON;
GO

USE TechEvalDb;
GO

-- 1. Índice de apoyo a la clave foránea. No depende de los datos, así que no puede fallar.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_UserAnswers_QuestionId' AND object_id = OBJECT_ID('dbo.UserAnswers'))
    CREATE INDEX IX_UserAnswers_QuestionId ON dbo.UserAnswers (QuestionId);
GO

-- 2. Índice único. Todo en un solo lote: así, si hay duplicados, no se crea nada y el
--    error de abajo es lo último que ocurre.
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = 'IX_UserAnswers_ExamSessionId_QuestionId'
             AND object_id = OBJECT_ID('dbo.UserAnswers'))
    PRINT 'IX_UserAnswers_ExamSessionId_QuestionId ya existe: nada que hacer.';
ELSE IF EXISTS (SELECT 1 FROM dbo.UserAnswers
                GROUP BY ExamSessionId, QuestionId HAVING COUNT(*) > 1)
BEGIN
    SELECT d.ExamSessionId, d.QuestionId, d.Filas,
           (SELECT CAST(STRING_AGG(CONCAT('Id=', a.Id, ' puntos=', ISNULL(CAST(a.AwardedPoints AS varchar(10)), 'NULL')), '; ') AS varchar(400))
              FROM dbo.UserAnswers a
             WHERE a.ExamSessionId = d.ExamSessionId AND a.QuestionId = d.QuestionId) AS Detalle
      FROM (SELECT ExamSessionId, QuestionId, COUNT(*) AS Filas
              FROM dbo.UserAnswers
             GROUP BY ExamSessionId, QuestionId
            HAVING COUNT(*) > 1) d
     ORDER BY d.ExamSessionId, d.QuestionId;

    RAISERROR('UserAnswers tiene respuestas duplicadas para la misma sesion y pregunta (ver la lista de arriba). No se ha creado el indice unico ni se ha borrado nada. Borra a mano la fila sobrante de cada pareja y vuelve a ejecutar este guion.', 16, 1);
END
ELSE
BEGIN
    CREATE UNIQUE INDEX IX_UserAnswers_ExamSessionId_QuestionId
        ON dbo.UserAnswers (ExamSessionId, QuestionId);
    PRINT 'UserAnswers admite ya una sola respuesta por sesion y pregunta (sin perdida de datos).';
END
GO

-- Vuelta atrás, si se quiere. La API anterior ignora estos índices.
--
-- DROP INDEX IX_UserAnswers_ExamSessionId_QuestionId ON dbo.UserAnswers;
-- DROP INDEX IX_UserAnswers_QuestionId ON dbo.UserAnswers;
