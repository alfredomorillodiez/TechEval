# resume-exam-in-progress

Una prueba en curso sobrevive a la recarga: el candidato vuelve a sus preguntas con el tiempo restante que calcula el servidor y con las respuestas que ya tenía guardadas

## Notas de despliegue

**Sin cambios de esquema.** No hay script SQL nuevo. `SessionStatus.InProgress` y `ExamSession.StartedAt` ya existían.

**API y Web se despliegan juntas.** La Web nueva espera `RemainingSeconds` y `SavedAnswers` en la respuesta de `POST /api/exam/start/{token}`. Una API anterior no los devuelve, y el temporizador arrancaría en cero.

**Antes de desplegar, revisar las sesiones abandonadas.** Tras el cambio se vuelven reanudables. Un candidato que dejó su prueba hace semanas entraría con tiempo cero y la prueba se auto-enviaría, creando un `ExamResult` inesperado.

```sql
SELECT s.Id, s.StartedAt, t.CandidateEmail, e.Title, e.TimeLimitMinutes
FROM ExamSessions s
JOIN ExamTokens t ON t.Id = s.ExamTokenId
JOIN Exams e ON e.Id = t.ExamId
WHERE s.Status = 1 AND s.CompletedAt IS NULL
ORDER BY s.StartedAt;
```

Las sesiones que superen ampliamente su tiempo límite se cierran a mano antes de desplegar.

**Vuelta atrás.** Revertir ambos despliegues. La base de datos no cambia, así que no hay nada que deshacer. Las sesiones abiertas vuelven a quedar bloqueadas, que es el comportamiento anterior.
