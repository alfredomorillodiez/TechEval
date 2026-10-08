## Why

El análisis del 08·10·2026 encontró cifras, enlaces y reglas que dicen una cosa y hacen otra.

- **Cifras y enlaces.**
  - En los resultados de una prueba, «Reprobados» es `_all.Count - _passedCount`: cuenta también los resultados pendientes de corrección, que no están reprobados. En el listado global ya está bien (`_reviewedCount - _passedCount`), y los dos números discrepan para la misma prueba.
  - El botón de resultados de cada prueba apunta a `/admin/results?examId=…`. Esa pantalla ignora el parámetro y abre el listado global.
- **Validaciones que faltan.** Nada comprueba en el servidor el tiempo límite, la nota de corte, la lista de preguntas ni el título. Se puede crear una prueba de 0 minutos (el candidato la abre y se envía sola en blanco) o sin preguntas. Un CSV de candidatos separado por `;`, que es lo que exporta Excel en castellano, importa 0 candidatos y no dice por qué.
- **Reglas del negocio incumplidas.**
  - Una prueba desactivada sigue aceptando invitaciones. Desactivar una prueba no la retira de la circulación.
  - Las preguntas de una categoría desactivada siguen entrando en la generación automática y en la disponibilidad que la pantalla anuncia.
- **Fechas.** Los correos dicen «expira el 09/10/2026 12:30 UTC». El candidato lee la hora en su reloj local y pierde el plazo sin saberlo.

## What Changes

- **Resultados.** «Reprobados» cuenta solo los corregidos que no aprobaron, en el listado de resultados de una prueba, con su porcentaje sobre los corregidos. Si ninguno está corregido, no se muestra un porcentaje falso. El botón de resultados de cada prueba abre `/admin/results/prueba/{id}`.
- **Validaciones del servidor** (`400` con un mensaje que dice qué falla):
  - Prueba (alta, generación y edición): título no vacío y de hasta 200 caracteres; tiempo límite entre 1 y 600 minutos; nota de corte entre 0 y 100.
  - Alta manual: al menos una pregunta, sin repetidas. Generación: entre 1 y 200 preguntas.
  - Invitación: nombre no vacío, correo con formato válido y expiración entre 1 y 720 horas. Envío masivo: entre 1 y 500 candidatos.
  - La Web usa los mismos límites en los formularios y los comprueba antes de enviar.
- **CSV de candidatos.** Acepta `;`, tabulador o `,` como separador (el último de la línea separa el correo del nombre, así que un nombre con coma entre comillas sirve), ignora una primera línea de encabezado y las líneas vacías, y valida cada correo. El resultado dice cuántos candidatos se importaron y qué líneas se ignoraron, con su número y el motivo. Con 0 importados, dice por qué.
- **Una prueba desactivada no admite invitaciones.** El envío individual y el masivo responden `409` con un mensaje claro. El envío masivo se rechaza entero, antes de crear ningún token.
- **Categorías desactivadas.** Las preguntas de una categoría desactivada no entran en la generación automática, ni en la disponibilidad que muestra la pantalla de generación, ni en el alta manual de una prueba.
- **Fechas de los correos en hora local.** La invitación y el enlace para fijar la contraseña muestran la fecha de expiración en una zona horaria configurable (`Email:TimeZoneId`, por defecto `Europe/Madrid`), con el nombre de la zona y su desfase respecto a UTC: «09/10/2026 14:30 (hora de Madrid, UTC+02:00)». Si la zona no existe en el equipo, vuelve a UTC y lo dice.
- **La Web muestra el motivo de un envío rechazado** en lugar de «Verifica la configuración de email».

Fuera de alcance:

- Congelar la nota de corte y el tiempo de la prueba, y las sesiones abandonadas: cambio posterior.
- Cambios en la base de datos.
- Reglas sobre qué pasa con los enlaces ya enviados si se desactiva la prueba después.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `exam-management`: validación de los datos de una prueba; categorías desactivadas fuera de la generación y el alta.
- `exam-delivery`: validación de la invitación; una prueba desactivada no admite invitaciones; fechas de los correos en hora local.
- `admin-console`: «Reprobados» correcto en los resultados de una prueba; el botón de resultados abre su pantalla; importación de CSV con informe.

## Impact

- **Aplicación**: nuevo `ExamLimits` con los límites compartidos; validación en `ExamService` y `ExamTokenService`; `ExamTokenService.SendExamAsync` y `SendExamBulkAsync` rechazan una prueba inactiva; `CandidateCsvParser` (puro, con pruebas).
- **Infraestructura**: `QuestionRepository` (generación, disponibilidad) exige categoría activa; `GraphEmailService` y `EmailSettings.TimeZoneId`.
- **API**: `400` y `409` nuevos con mensaje. Sin endpoints nuevos.
- **Web**: `ResultsByExam`, `ExamList`, `ExamNew`, `GenerateExam`, `ExamDetail`, `ApiService` (envío con mensaje de error).
- **Pruebas**: validaciones, prueba y categoría inactivas, analizador de CSV, formato de fecha local.
- **Configuración**: `Email:TimeZoneId` opcional.
