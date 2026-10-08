-- Cambio aditivo, no destructivo: añade a UserAnswers la copia de lo que se le preguntó
-- al candidato (faithful-answer-record, defecto D7) en una base TechEvalDb ya existente,
-- sin borrar ningún dato. Idempotente: puede reejecutarse sin error.
--
-- Motivo: las fichas de resultados leían el enunciado, las opciones y los puntos de la
-- pregunta actual del banco. Editar una pregunta cambiaba, hacia atrás, lo que constaba
-- que se preguntó en cada examen ya cerrado.
SET QUOTED_IDENTIFIER ON;
GO

USE TechEvalDb;
GO

IF COL_LENGTH('dbo.UserAnswers','QuestionTextSnapshot') IS NULL
    ALTER TABLE dbo.UserAnswers ADD QuestionTextSnapshot NVARCHAR(2000) NULL;
GO

IF COL_LENGTH('dbo.UserAnswers','SelectedAnswerTextSnapshot') IS NULL
    ALTER TABLE dbo.UserAnswers ADD SelectedAnswerTextSnapshot NVARCHAR(1000) NULL;
GO

IF COL_LENGTH('dbo.UserAnswers','CorrectAnswerTextSnapshot') IS NULL
    ALTER TABLE dbo.UserAnswers ADD CorrectAnswerTextSnapshot NVARCHAR(1000) NULL;
GO

IF COL_LENGTH('dbo.UserAnswers','QuestionPointsSnapshot') IS NULL
    ALTER TABLE dbo.UserAnswers ADD QuestionPointsSnapshot INT NULL;
GO

-- Relleno de las respuestas anteriores al cambio.
--
-- AVISO: esto NO recupera lo que se preguntó entonces. Ese texto no está guardado en
-- ninguna parte. Rellena con el enunciado de hoy, que es exactamente lo que esas fichas
-- muestran ya. No mejora esas filas; solo impide que sigan cambiando a partir de ahora.
UPDATE ua
SET ua.QuestionTextSnapshot       = q.Text,
    ua.QuestionPointsSnapshot     = q.Points,
    ua.SelectedAnswerTextSnapshot = sel.Text,
    ua.CorrectAnswerTextSnapshot  = (SELECT TOP 1 c.Text
                                     FROM dbo.Answers c
                                     WHERE c.QuestionId = q.Id AND c.IsCorrect = 1
                                     ORDER BY c.[Order], c.Id)
FROM dbo.UserAnswers ua
INNER JOIN dbo.Questions q ON q.Id = ua.QuestionId
LEFT  JOIN dbo.Answers  sel ON sel.Id = ua.SelectedAnswerId
WHERE ua.QuestionTextSnapshot IS NULL;
GO

PRINT 'UserAnswers guarda ya la copia de lo preguntado (sin pérdida de datos).';
GO
