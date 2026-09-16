## Context

Ver `proposal.md` — Why para la motivación.

Lo que condiciona el enfoque:

- `UserAnswer.AwardedPoints` ya congela los puntos otorgados, y su comentario explica el motivo: recalcular contra `Question.Points` daría notas distintas si la pregunta se edita. El patrón existe; falta aplicarlo al resto.
- El proyecto no usa migraciones de EF. El esquema se mantiene con guiones en `scripts/`, aditivos e idempotentes, más `EnsureCreatedAsync` en el arranque. Ver el hallazgo `A4`.
- Las lecturas que hay que cambiar son cuatro, en dos servicios: `ResultService.GetDetailAsync` y `OpenQuestionReviewService.GetDetailAsync`.
- El autoguardado fuera de plazo se rechaza con `409` desde el arreglo de `S4`.

## Goals / Non-Goals

**Goals:**

- Que editar una pregunta no cambie lo que consta en un examen ya cerrado.
- Que no se pierda lo que el candidato escribe en una pregunta abierta.
- Que la hora de una respuesta sea comparable con la de su sesión.

**Non-Goals:**

- No se versiona la pregunta. Este cambio guarda lo que el candidato vio, no la historia completa del banco.
- No se conservan las opciones que el candidato descartó, solo la que eligió y la correcta.
- No se toca `SampleAnswer`, que es guía del corrector y no algo que el candidato viera.

## Decisions

### Congelar en `UserAnswer`, no versionar la pregunta

Cuatro columnas nuevas: enunciado, texto de la opción elegida, texto de la correcta y puntos máximos.

**Alternativa descartada**: versionar la pregunta entera, con tabla de versiones y `UserAnswer` apuntando a la versión formulada. Conserva más —las cuatro opciones, no solo dos— y es la solución de fondo. Pero es un cambio mayor: tabla nueva, migración de datos y tres pantallas afectadas. La copia resuelve el defecto tal como está descrito, con un guion aditivo y sin tocar el banco de preguntas.

**Alternativa descartada**: guardar la pregunta entera serializada en una columna. Evita decidir qué campos importan, pero deja un dato opaco que ninguna consulta puede filtrar, y que hay que versionar aparte en cuanto cambie la forma del objeto.

### El momento de congelar es el envío

La copia se escribe en `SubmitExamAsync`, dentro de la misma transacción que fija `AwardedPoints`. Es cuando la respuesta pasa a formar parte del resultado.

**Alternativa descartada**: congelar en cada autoguardado. Sería más fiel —captura lo que el candidato tenía delante en ese instante— pero multiplica las escrituras y complica el autoguardado, que hoy es una operación pequeña. Queda una ventana: si un administrador edita la pregunta entre la respuesta y el envío, la copia recoge el texto editado. Esa ventana dura lo que dura un examen, y el caso exige editar justo una pregunta que alguien está respondiendo.

### El relleno de las filas antiguas usa el texto de hoy

No hay forma de recuperar lo que se preguntó entonces: no está guardado en ninguna parte. El guion rellena con el enunciado actual, que es exactamente lo que esas fichas muestran hoy. No mejora esas filas, pero tampoco las empeora, y a partir de la ejecución dejan de moverse.

### El autoguardado por escritura usa un temporizador, no cada pulsación

`@oninput` ya existe y actualiza el borrador en memoria; lo que falta es enviarlo. Enviar en cada pulsación haría una llamada por carácter. Un `Timer` que se reinicia con cada pulsación y dispara a los dos segundos agrupa la escritura continua en un solo envío.

El `@onblur` se conserva: cubre el caso de quien escribe y salta de campo antes de que venza el retardo.

**Sobre el rechazo fuera de plazo**: el autoguardado por escritura puede dispararse justo después de vencer el plazo y recibir un `409`. La pantalla no debe tratarlo como pérdida de la respuesta, porque el envío final conserva lo último guardado en plazo.

## Risks / Trade-offs

**Cuatro columnas más en la tabla que más crece** → `UserAnswers` tiene una fila por pregunta y candidato. El enunciado se repite en cada una. A cambio, la ficha deja de depender de una tabla que cambia. Mitigación: ninguna; es el precio de la copia, y es el mismo que ya se paga con `AwardedPoints`.

**Más llamadas al backend durante el examen** → El retardo las agrupa, pero un candidato que escriba mucho generará varias por minuto. Con los volúmenes de hoy no es un problema; con los de `R1` habría que revisarlo junto con el resto.

**El guion de relleno recorre `UserAnswers` entera** → En una base grande es un `UPDATE` largo. Se ejecuta una sola vez, en una ventana de mantenimiento.

**Una API nueva contra un esquema antiguo falla** → Las columnas no existen. El guion va antes que el despliegue. Es el orden habitual en este proyecto.

## Migration Plan

1. Ejecutar `scripts/add_answer_snapshot_columns.sql` contra la base de datos de destino. Es aditivo e idempotente.
2. Desplegar la API.
3. Desplegar la Web.

**Vuelta atrás**: revertir la API. Las columnas se quedan, sin uso y sin estorbar. No hay que deshacer el guion.
