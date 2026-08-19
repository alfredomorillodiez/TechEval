-- ============================================================
-- TechEval Platform — Limpieza del histórico de intentos de examen
-- previo al cambio "student-user-accounts"
--
-- Elimina ExamTokens (y por cascada: ExamSessions, UserAnswers,
-- ExamResults) que fueron creados bajo el modelo anterior, donde
-- el candidato era solo CandidateName/CandidateEmail sin User
-- asociado. NO toca Users, Categories, Questions, Answers ni Exams.
--
-- Uso: sqlcmd -S <servidor> -d TechEvalDb -i scripts/reset_exam_history.sql
-- ============================================================

USE TechEvalDb;
GO

DELETE FROM dbo.ExamTokens;
GO

PRINT '✓ Histórico de ExamTokens/ExamSessions/UserAnswers/ExamResults eliminado.';
GO
