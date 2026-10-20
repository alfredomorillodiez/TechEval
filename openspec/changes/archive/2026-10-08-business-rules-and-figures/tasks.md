## 1. Servidor

- [x] 1.1 Crear `ExamLimits` y las validaciones de `ExamService` (alta, generación y edición) según design.md D1. Verificar con pruebas: 0 minutos, 601 minutos, nota 150, título vacío o de 201 caracteres, lista de preguntas vacía y repetida, generación de 0 y de 201 preguntas, y edición con 0 minutos; en todos, `ValidationException` sin escribir ni leer el repositorio.
- [x] 1.2 Validar las invitaciones en `ExamTokenService` (D1) y rechazar las de una prueba inactiva (D2). Verificar con pruebas: correo inválido, nombre vacío, 0 horas, 721 horas, masivo vacío, masivo de 501, masivo con una línea inválida (no crea ningún token), prueba inactiva individual y masiva (`ConflictException` sin tokens ni correo) y el caso feliz.
- [x] 1.3 Exigir categoría activa en `GetRandomAsync`, `CountByDifficultyAsync` y `ExamService.CreateAsync` (D3). Verificar con una prueba de repositorio contra la base en memoria o con la consulta traducida: las preguntas de una categoría inactiva no salen ni se cuentan.
- [x] 1.4 `EmailSettings.TimeZoneId` y la fecha local en `GraphEmailService` (D4). Verificar con pruebas: horario de verano, de invierno, zona desconocida (UTC con etiqueta y sin fallar) y valor con `Kind` indefinido.
- [x] 1.5 `CandidateCsvParser` (D5). Verificar con pruebas: `;`, tabulador y `,`, encabezado, líneas vacías, BOM, nombre con coma entre comillas, correo inválido, línea sin separador, duplicados y texto sin ninguna línea válida.

## 2. Web

- [x] 2.1 `ResultsByExam`: reprobados, porcentaje y pendientes (D6); `ExamList`: enlace de resultados (D6). Verificar en el navegador con una prueba de 5 resultados (2 aprobados, 1 reprobado, 2 pendientes) y con una de solo pendientes.
- [x] 2.2 `ExamList`: usar `CandidateCsvParser`, mostrar el informe y el motivo de un envío rechazado (D5, D7); `ApiService.SendExamAsync` y `SendExamBulkAsync` con mensaje. Verificar en el navegador: CSV con `;`, con encabezado, con una línea mala, vacío; envío a una prueba desactivada.
- [x] 2.3 `ExamNew`, `GenerateExam` y `ExamDetail`: límites de `ExamLimits` en los `min`/`max` y comprobación previa con mensaje (D1). Verificar en el navegador: 0 minutos, sin preguntas y nota 150 muestran el aviso sin llamar a la API.

## 3. Documentación y cierre

- [x] 3.1 Documentar `Email:TimeZoneId` y los límites en `documentacion.md` y `README.md`. Verificar con una búsqueda.
- [x] 3.2 Ejecutar la batería completa y `openspec validate business-rules-and-figures --strict`. Verificar que pasan.
- [x] 3.3 Recorrido en local contra una base de prueba (nunca `TechEvalDb`) y sin credenciales de correo: probar con la API los `400` y `409` y comprobar en la base de datos que no se crea nada.
