## Context

Ver `proposal.md` — Why para la motivación.

Lo que condiciona el enfoque:

- El modelo ya tiene todo lo necesario. `ExamSession.Status` (`InProgress` / `Completed` / `Expired` / `Abandoned`) y `ExamSession.StartedAt` existen desde el principio. **Este cambio no toca el esquema de base de datos.**
- `ExamTokenRepository.GetWithExamAndSessionAsync` ya carga la sesión con sus `UserAnswers`. La información para decidir y para reconstruir el estado ya llega al servicio en una sola consulta.
- `ExamTokenValidationDto` ya lleva un campo `SessionId` opcional que hoy nunca se rellena con valor útil, porque el flujo se corta antes.
- `ExamToken.IsValid` es una propiedad calculada de la entidad, ignorada por EF (`QuestionConfiguration.cs:113`). No puede consultar el estado de la sesión sin depender de que la navegación esté cargada.
- Los endpoints de examen son públicos y operan por `sessionId` sin comprobar propiedad. Ese hueco es un hallazgo aparte; este diseño no lo abre más ni lo cierra.

## Goals / Non-Goals

**Goals:**

- Que una prueba en curso sobreviva a una recarga, a un cierre de pestaña y a una pérdida de red.
- Que el tiempo de la prueba lo mida el servidor, para que reanudar no regale tiempo.
- Que reanudar recupere lo ya respondido y que el envío posterior no lo destruya.
- Que el candidato tenga dos caminos de vuelta: el enlace del correo y el portal.

**Non-Goals:**

- No se valida en el servidor que el envío llegue dentro de plazo. El servidor calcula el tiempo restante, pero sigue aceptando un envío tardío.
- No se comprueba la propiedad de la sesión en `answer` ni en `submit`.
- No se cierran automáticamente las sesiones abandonadas. `SessionStatus.Expired` y `Abandoned` siguen sin usarse.
- No se resuelve el caso de dos pestañas abiertas sobre la misma sesión. Hoy ya ocurre y este cambio no lo empeora.

## Decisions

### La regla de reanudación vive en el servicio, no en la entidad

`ExamToken.IsValid` pasa a llamarse `CanStart`, con el mismo cuerpo (`!IsUsed && !IsExpired`). El nombre deja de mentir: contesta a "¿se puede **empezar** esta prueba?", que es lo único que esa expresión sabe.

La pregunta nueva —"¿se puede **volver** a esta prueba?"— la responde `ExamTokenService`, que ya tiene la sesión cargada:

```
puedeEntrar(token) =
    token.CanStart                                  // nunca se empezó
 || token.ExamSession?.Status == InProgress         // se empezó y sigue abierta
```

**Alternativa descartada**: añadir a `ExamToken` una propiedad `IsResumable` que mire `ExamSession.Status`. Una propiedad calculada de una entidad que depende de una navegación devuelve un resultado silenciosamente falso cuando esa navegación no está cargada. `ExamTokenRepository` tiene dos métodos de lectura y solo uno carga la sesión; la trampa estaba servida.

**Alternativa descartada**: dejar de marcar `IsUsed = true` al iniciar. Rompe la unicidad del token, que es el mecanismo que impide dos sesiones sobre la misma invitación, y afecta a `exam-delivery`.

### `ExpiresAt` cierra la puerta de entrada, no la de salida

Una invitación que expira a las 18:00 y una prueba empezada a las 17:55 con 60 minutos de límite: el candidato llega hasta las 18:55. `ExpiresAt` gobierna hasta cuándo se puede empezar; a partir de ahí manda el tiempo límite del examen.

La alternativa —cortar la prueba al llegar `ExpiresAt`— convierte la expiración en una trampa que nadie ve venir, y el candidato no tiene forma de saber cuánto tiempo real le queda.

### El tiempo restante viaja calculado desde el servidor

`ExamSessionInfoDto` gana `RemainingSeconds`, calculado como:

```
restante = max(0, (StartedAt + TimeLimitMinutes) - UtcNow)
```

**Alternativa descartada**: devolver `StartedAt` y que el cliente reste. Es lo que hay hoy en la práctica, y falla por dos vías: el reloj del navegador puede ir desfasado, y el candidato puede cambiarlo. El servidor ya es la única fuente fiable.

Se conserva `TimeLimitMinutes` en el DTO: la pantalla de bienvenida lo muestra y no es lo mismo que el tiempo restante.

### Las respuestas restauradas reutilizan `SubmitAnswerDto`

`ExamSessionInfoDto` gana `List<SubmitAnswerDto> SavedAnswers`. Ese record ya existe, ya es el formato con el que el cliente maneja sus borradores y lleva exactamente tres campos: `QuestionId`, `SelectedAnswerId` y `OpenAnswer`.

Esa forma cumple sola el requisito de no revelar el solucionario: no hay sitio donde meter `IsCorrect`, ni `AwardedPoints`, ni qué opción era la buena. Un DTO nuevo habría que vigilarlo; este no.

### El cliente decide con el `SessionId` que ya recibe

`ValidateTokenAsync` rellena `SessionId` cuando hay sesión en curso. `TakeExam.razor` lo lee:

| `SessionId` | Estado inicial |
|---|---|
| nulo | `Welcome` — pantalla de bienvenida, como hoy |
| con valor | `InProgress` — llama a `start`, restaura y arranca el temporizador |

**Alternativa descartada**: un endpoint nuevo `GET /api/exam/session/{id}`. Tras este cambio, `POST /api/exam/start/{token}` ya es idempotente y devuelve exactamente lo que hace falta. Un endpoint más sería otra superficie pública que vigilar.

### Tiempo agotado: se devuelve cero y el cliente auto-envía

Si el candidato vuelve cuando ya no queda tiempo, el servidor devuelve la sesión con `RemainingSeconds = 0`. El auto-envío del temporizador —que ya existe— dispara de inmediato con lo que hubiera guardado.

**Alternativa descartada**: que el servidor cierre la sesión y devuelva un error. Obliga a inventar un camino de cierre sin candidato delante, que es justo el trabajo de un proceso de sesiones abandonadas. Queda anotado como *non-goal*.

## Risks / Trade-offs

**Las sesiones abandonadas que ya existen en producción se vuelven reanudables** → Tras el despliegue, un candidato que abandonó su prueba hace semanas puede volver a abrir el enlace. Entrará con tiempo cero y la prueba se auto-enviará con lo poco que tuviera, generando un `ExamResult` inesperado. Mitigación: antes de desplegar, consultar `ExamSession` con `Status = InProgress` y `StartedAt` antiguo, y cerrarlas a mano o dejarlas en `Abandoned`. La consulta va en las notas de despliegue.

**Un candidato puede recargar a propósito para ver de nuevo el enunciado** → No es un riesgo nuevo: hoy también ve todas las preguntas a la vez y navega libremente. El temporizador, ahora sí, sigue corriendo mientras tanto.

**El envío tardío se sigue aceptando** → Este cambio hace visible el tiempo real, pero no lo hace obligatorio. Alguien que congele el temporizador del navegador puede enviar fuera de plazo igual que hoy. Mitigación: es un hallazgo aparte, y este diseño deja la pieza que necesita —el cálculo en el servidor— ya construida y probada.

**Dos pestañas sobre la misma sesión se pisan el auto-guardado** → Ambas reanudan la misma sesión y la última en guardar gana. Ya ocurre hoy con dos pestañas abiertas desde el principio. Mitigación: ninguna en este cambio; se acepta.

**El cambio de nombre `IsValid` → `CanStart` toca la configuración de EF** → `builder.Ignore(t => t.IsValid)` dejaría de compilar, que es exactamente lo que se quiere: el compilador señala el único sitio a actualizar. Sin riesgo silencioso.

## Migration Plan

1. Sin migración de esquema. No hay script SQL nuevo.
2. Antes del despliegue, revisar las sesiones abiertas:
   ```sql
   SELECT s.Id, s.StartedAt, t.CandidateEmail, e.Title, e.TimeLimitMinutes
   FROM ExamSessions s
   JOIN ExamTokens t ON t.Id = s.ExamTokenId
   JOIN Exams e ON e.Id = t.ExamId
   WHERE s.Status = 1 AND s.CompletedAt IS NULL
   ORDER BY s.StartedAt;
   ```
   Las que superen ampliamente su tiempo límite se cierran a mano antes de desplegar.
3. Despliegue normal de API y Web. Ambas deben ir juntas: la Web nueva espera `RemainingSeconds` en la respuesta, y la API vieja no lo devuelve.
4. Vuelta atrás: revertir ambas. El esquema no cambia, así que no hay nada que deshacer en base de datos. Las sesiones abiertas vuelven a quedar bloqueadas, que es el comportamiento anterior.
