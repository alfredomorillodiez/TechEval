## Why

La generación automática de una prueba solo admite un nivel de dificultad o todos. Con «todos», el reparto sale al azar: una prueba de 10 preguntas puede salir con 8 básicas y ninguna avanzada, aunque se quiera evaluar a un perfil sénior. Con un solo nivel, la prueba es entera de ese nivel. No hay forma de pedir, por ejemplo, 30 % básicas, 50 % intermedias y 20 % avanzadas.

Además, el banco no está equilibrado. En `TechEvalDb` hay 88 preguntas básicas activas, 105 intermedias y solo 36 avanzadas, y algunas categorías no tienen ninguna avanzada. Un reparto pedido no siempre se puede cumplir, y el administrador tiene que saberlo antes de generar, no después de un rechazo.

## What Changes

- **Reparto por nivel.** La generación automática acepta, en lugar de un nivel único, un porcentaje entero de 0 a 100 para cada nivel (básico, intermedio, avanzado). Los tres suman 100. Un nivel al 0 % queda fuera. El porcentaje es de preguntas, no de puntos.
- **Redondeo por mayor resto.** El número de preguntas de cada nivel sale del porcentaje, redondeado con el método del mayor resto, así que la suma es siempre el total pedido.
- **Rechazo si un nivel no alcanza.** Si algún nivel del reparto no tiene bastantes preguntas activas en las categorías elegidas, la generación se rechaza. El mensaje dice qué niveles fallan, cuántas preguntas se necesitan y cuántas hay. El sistema no completa nunca un nivel con preguntas de otro.
- **El nivel único sigue como hoy.** Si se elige un solo nivel, la prueba es entera de ese nivel, y la generación se rechaza si no alcanza.
- **Disponibilidad por nivel.** Un endpoint nuevo devuelve cuántas preguntas activas hay de cada nivel en unas categorías. La pantalla lo usa para la vista previa.
- **Vista previa.** La pantalla de generación muestra, antes de generar, cuántas preguntas saldrán de cada nivel y cuántas hay disponibles. Si algún nivel no alcanza, lo dice y no deja generar. El detalle de una prueba muestra su reparto real por nivel.

Fuera de alcance:

- Plantillas de reparto (junior, sénior…).
- El reparto por puntos, por tipo de pregunta (test o abierta) o por categoría.
- La creación manual de pruebas.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `exam-management`: la generación automática gana el reparto por nivel, con su redondeo y el rechazo cuando un nivel no alcanza; el nivel único sigue como hoy.
- `question-bank`: nueva consulta de disponibilidad de preguntas activas por nivel en unas categorías.
- `admin-console`: la pantalla de generación ofrece el reparto por nivel con vista previa y bloqueo si un nivel no alcanza; el detalle de la prueba muestra su reparto real.

## Impact

- **Aplicación**: un planificador puro (`DifficultyPlanner`) calcula, a partir del total, los porcentajes y la disponibilidad, cuántas preguntas salen de cada nivel y cuántas faltan en cada uno. Lo usan el servicio y la vista previa de la Web, así que los dos dicen lo mismo. `ExamService.GenerateAsync` elige al azar por nivel según ese plan y mezcla el resultado.
- **DTO**: `GenerateExamDto` gana los porcentajes por nivel, como parámetro opcional. Nuevo DTO de disponibilidad por nivel.
- **API**: `GET /api/questions/availability`. `POST /api/exams/generate` acepta el reparto; el nivel único sigue como hoy.
- **Web**: `GenerateExam.razor` gana el modo «Reparto por nivel», la vista previa y el aviso. `ExamDetail.razor` muestra el reparto real.
- **Base de datos**: sin cambios. El reparto real se deduce de las preguntas de la prueba.
- **Pruebas**: planificador (redondeo, desempate, faltantes por nivel) y generación por nivel.
- **Documentación**: `README.md` y `documentacion.md` describen el reparto y el rechazo.
