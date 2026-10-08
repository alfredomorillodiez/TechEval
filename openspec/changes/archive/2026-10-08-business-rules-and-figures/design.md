## Context

Ver `proposal.md` — Why. Los requisitos están en `specs/`.

Lo que condiciona el enfoque:

- No hay validación de datos de negocio en la capa de aplicación: `ValidationException` existe y el middleware la traduce a `400`, pero `ExamService` solo la usa para preguntas inexistentes o escasas, y `ExamTokenService` para el rol del invitado. Los DTO son `record` sin atributos.
- Las pantallas de la Web ponen `min` y `max` en los `<input type="number">`, pero esos atributos no impiden enviar el formulario con otro valor.
- `QuestionRepository` tiene tres consultas que alimentan la generación y la disponibilidad (`GetRandomAsync`, `CountByDifficultyAsync`) y una que usa el alta manual (`ExamService.CreateAsync` con `FindAsync`). Ninguna mira `Category.IsActive`. `Category.IsActive` existe y la baja lógica de una categoría ya la pone a `false`.
- `SendExamAsync` y `SendExamBulkAsync` cargan la prueba con `GetByIdAsync` y no miran `IsActive`. El envío masivo captura cada excepción por candidato y la convierte en un resultado: una excepción de la prueba desactivada saldría repetida en cada fila.
- `GraphEmailService` compone el HTML con `expiresAt:dd/MM/yyyy HH:mm` seguido de la literal «UTC». Hay dos plantillas con esta fecha: la invitación y el enlace de contraseña. `EmailSettings` no tiene zona.
- `ResultList` ya cuenta bien los reprobados (`_reviewedCount - _passedCount`); `ResultsByExam` usa `_all.Count - _passedCount`.
- La ruta de resultados de una prueba es `/admin/results/prueba/{ExamId:int}`; `ExamList` enlaza a `/admin/results?examId=…`, que `ResultList` no lee.
- El analizador de CSV vive en el `@code` de `ExamList.razor`, sin forma de probarlo.

## Goals / Non-Goals

**Goals:**

- Un solo sitio que diga cuáles son los límites, usado por el servidor y por la Web.
- Que la validación del servidor sea la fuente de verdad, y la de la Web una cortesía.
- Que ninguna prueba desactivada ni categoría desactivada alimente nada nuevo.

**Non-Goals:**

- Un framework de validación (FluentValidation o atributos). Con diez reglas, métodos pequeños bastan.
- Cambiar el esquema.

## Decisions

### D1. `ExamLimits` y métodos de validación en el servicio

`Application/ExamLimits.cs` con constantes públicas: `TitleMaxLength = 200`, `MinTimeLimitMinutes = 1`, `MaxTimeLimitMinutes = 600`, `MinPassingScore = 0`, `MaxPassingScore = 100`, `MaxGeneratedQuestions = 200`, `MinExpirationHours = 1`, `MaxExpirationHours = 720`, `MaxBulkCandidates = 500`. La Web las usa en sus `min`/`max` y en su comprobación previa.

`ExamService` gana `ValidateExamData(title, minutes, passing)` (privado y estático), usado por `CreateAsync`, `GenerateAsync` y `UpdateAsync`, y las comprobaciones de la lista y de la cantidad. Se valida **antes** de cualquier lectura del repositorio, para no escribir ni leer nada con datos inválidos. Cada falla lanza `ValidationException` con una frase concreta («El tiempo límite debe estar entre 1 y 600 minutos.»).

`ExamTokenService` gana `ValidateInvitation(name, email, hours)`, usado por el envío individual y por cada candidato del masivo. El formato del correo se comprueba con `System.Net.Mail.MailAddress.TryCreate` más la exigencia de que el dominio tenga un punto, y el texto no cambia tras recortarlo: así «Juan <x@y.com>» no pasa por correo. En el masivo se valida **toda** la lista antes de crear ningún token y se rechaza entera si alguna línea es inválida, con el número de la primera: un alta masiva a medias es peor que ninguna.

La tolerancia a fallos por candidato del masivo (que el correo falle para uno) no cambia: eso es otro tipo de fallo.

### D2. Invitar a una prueba inactiva

Un método privado `EnsureInvitable(Exam)` lanza `ConflictException("La prueba «…» está desactivada y no admite invitaciones.")`. En el masivo se llama una vez, antes del bucle. Usar `ConflictException` (409) y no `ValidationException` (400): el dato es correcto, es el estado el que lo impide, igual que `AlreadyReviewedException`.

### D3. Categoría activa en las consultas de preguntas

`GetRandomAsync` y `CountByDifficultyAsync` añaden `.Where(q => q.Category!.IsActive)`. `ExamService.CreateAsync` cambia su `FindAsync` a `q.IsActive && q.Category!.IsActive`. EF traduce la navegación a un `JOIN`. El filtro del listado de preguntas del banco (`GetFilteredAsync`) **no** cambia: el administrador sigue viendo el banco entero y su estado.

### D4. Fecha local en los correos

`EmailSettings.TimeZoneId` (por defecto `Europe/Madrid`). `GraphEmailService` resuelve la zona una vez en el constructor: `TimeZoneInfo.FindSystemTimeZoneById(id)`; ante `TimeZoneNotFoundException` o `InvalidTimeZoneException`, UTC y un aviso en el log. .NET en Windows acepta ids IANA cuando hay ICU, y en Linux los usa de forma nativa.

Un método interno `FormatLocal(DateTime utc)` produce `dd/MM/yyyy HH:mm (hora de {ciudad}, UTC{+hh:mm})`. La ciudad sale del id (`Europe/Madrid` → «Madrid»; guiones bajos a espacios). Con la zona UTC, o la de reserva, produce `dd/MM/yyyy HH:mm (UTC)`. El valor de entrada se trata como UTC aunque llegue con `Kind` indefinido (SQL Server devuelve `DateTime` sin zona).

Alternativa descartada: hora del navegador del destinatario. El correo es HTML estático y no puede calcularla.

### D5. Analizador de CSV en Aplicación

`CandidateCsvParser.Parse(string text)` devuelve `CsvParseResult(List<BulkCandidateDto> Candidates, List<CsvLineIssue> Issues)`. `CsvLineIssue(int Line, string Reason)`.

- Divide por líneas (`\r\n` o `\n`), quita un BOM inicial y salta las vacías (cuentan en la numeración).
- El separador es el **último** de `;`, tabulador o `,` fuera de comillas. Así `"García, Juan",juan@x.com` da nombre y correo, y un correo (que no puede contener `;` ni tabulador, y rara vez coma) queda a su lado.
- Quita las comillas del nombre y del correo y los recorta.
- La primera línea se ignora **sin avisar** si no tiene un correo válido y su segunda columna es «email», «correo» o «e-mail» (sin distinguir mayúsculas). Cualquier otra línea sin correo válido es un `CsvLineIssue` con el motivo «el correo no es válido» o «falta el nombre» o «falta el separador».
- Detecta duplicados dentro del propio texto (mismo correo, sin distinguir mayúsculas) y los avisa («repetido en la línea N»).

`ExamList` usa el analizador y muestra el resumen: «N importados» y, si hay incidencias, una lista con la línea y el motivo; con 0 importados, el texto «No se importó ningún candidato» y el formato esperado.

### D6. Los resultados de una prueba

En `ResultsByExam`, «Reprobados» pasa a `_reviewedCount - _passedCount`, su porcentaje a `_reviewedCount > 0 ? 100 - _passRate : …` y no se pinta sin corregidos, y se añade una tarjeta «Pendientes» con `_pendingCount` (que la clase ya calcula y no se mostraba). `ExamList` apunta a la ruta correcta con un `title` y un `aria-label` («Resultados de esta prueba»).

### D7. Mensaje del servidor en los envíos

`ApiService.SendExamAsync` y `SendExamBulkAsync` pasan a `(Result, StatusCode, Error)` con `SendAsync`. `ExamList` muestra `error` si viene, con la frase actual de respaldo.

## Risks / Trade-offs

- [Los límites 1–600 y 1–720 se quedan cortos para algún uso] → Son constantes en un solo sitio. Subirlas es un cambio de una línea.
- [Una prueba ya existente con 0 minutos o sin preguntas] → La validación solo actúa al crear o editar. Una prueba existente con datos inválidos no se corrige sola: la edición la rechazará hasta que se corrija.
- [Rechazar entero un envío masivo por una línea inválida es incómodo] → La Web valida la lista antes de enviarla, así que en la práctica solo llega al servidor una lista ya limpia; el rechazo del servidor es la red de seguridad.
- [`TimeZoneInfo` y los ids IANA en Windows sin ICU] → Se cae a UTC y se registra. La etiqueta del correo dice «UTC», así que nunca es ambiguo.
- [Una categoría desactivada vacía de golpe la generación] → Es lo que la desactivación pide. La disponibilidad lo refleja antes de generar.

## Migration Plan

- Sin cambios de esquema.
- `Email:TimeZoneId` es opcional; sin él, `Europe/Madrid`.
- Para volver atrás, revertir la API y la Web.
