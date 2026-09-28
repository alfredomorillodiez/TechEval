## Why

Hoy nada dificulta ni registra la trampa durante una prueba. Todos los candidatos de un examen reciben las mismas preguntas, con las mismas opciones, en el mismo orden (`ExamQuestion.Order` es fijo por examen). Una prueba filtrada sirve tal cual al siguiente candidato. Además, el corrector no tiene ningún dato sobre cómo se hizo la prueba: no sabe si el candidato salió de la página para buscar la pregunta, ni si pegó una respuesta abierta escrita en otro sitio.

Una página web no puede impedir la captura de pantalla ni la consulta a un buscador o a una IA. Este cambio no lo intenta. Su objetivo es que la filtración valga menos, que compartir una captura identifique a su autor, y que el corrector tenga señales para decidir. Las señales no cambian la nota de forma automática.

## What Changes

- **Orden propio de cada sesión.** Cada `ExamSession` presenta las preguntas, y las opciones de cada pregunta de test, en una permutación propia. La permutación es estable: el candidato que recarga la página ve el mismo orden. El orden del examen (`ExamQuestion.Order`) no cambia y sigue siendo la referencia en los resultados.
- **Marca de agua visible.** Durante la resolución, la pantalla muestra el nombre y el correo del candidato en diagonal y semitransparentes sobre el contenido. La marca no impide leer ni responder. La marca no se puede seleccionar y los lectores de pantalla la ignoran.
- **Registro de salidas de la página.** La interfaz registra cada vez que la pestaña deja de estar visible o la ventana pierde el foco, y cada vez que el candidato vuelve. En la vuelta, la interfaz envía la duración de la ausencia.
- **Registro de pegados en las respuestas abiertas.** La interfaz registra cada pegado en un área de texto de respuesta abierta, con la pregunta y el número de caracteres pegados. El sistema no guarda el texto pegado.
- **Nuevo endpoint de señales.** `POST /api/exam/integrity/{sessionId}` recibe las señales. Exige el JWT de alumno y la propiedad de la sesión, igual que el auto-guardado. Solo acepta señales mientras la sesión está en curso, y limita el número de señales por sesión.
- **Aviso en la bienvenida.** La pantalla de bienvenida informa, antes de empezar, de qué actividad se registra y de que el corrector la ve.
- **Señales visibles para el corrector.** El detalle de un resultado y el detalle de corrección muestran un resumen de las señales de la sesión y su cronología. Ningún proceso automático usa las señales para puntuar, suspender ni marcar al candidato.

Fuera de alcance, y anotado como trabajo posterior:

- Un subconjunto de preguntas distinto para cada candidato a partir de un banco mayor.
- La entrega pregunta a pregunta. Hoy el examen completo llega al navegador en un solo JSON y es visible en las herramientas de desarrollo.
- El registro de copias del texto de la pregunta, la pantalla completa obligatoria, el tiempo por pregunta y la detección de dos navegadores sobre la misma sesión.
- Una marca de ordenación fija por pregunta para opciones que dependen de su posición ("Todas las anteriores").

## Capabilities

### New Capabilities

- `exam-integrity`: registro de las señales de integridad de una sesión (salidas de la página, vueltas y pegados), sus límites, su protección de acceso y su presentación al corrector, sin efecto automático sobre la nota.

### Modified Capabilities

- `exam-taking`: se añade el requisito del orden propio de cada sesión, para las preguntas y para las opciones de test, estable entre recargas.
- `candidate-experience`: se añaden los requisitos de la marca de agua durante la resolución y del aviso de registro en la pantalla de bienvenida.

## Impact

- **Dominio**: `ExamSession` gana `ShuffleSeed`. Nueva entidad `ExamIntegrityEvent` con su enumerado de tipos, relacionada con `ExamSession`.
- **Base de datos**: nueva tabla `ExamIntegrityEvents` y nueva columna `ExamSessions.ShuffleSeed`. Hay que añadirlas a `scripts/create_database.sql` y a un guion aditivo nuevo. La prueba de alineación entre modelo y guion lo exige.
- **Aplicación**: `ExamTokenService.BuildSessionInfo` aplica la permutación y añade el correo del candidato. `StartSessionAsync` genera la semilla al crear la sesión. Nuevo servicio para registrar y leer señales. `ResultService` y `OpenQuestionReviewService` exponen las señales, o las expone un endpoint propio.
- **API**: `ExamSessionController` gana el endpoint de señales, con limitación de ritmo. Nuevo endpoint de administración para leer las señales de un resultado.
- **DTO**: `ExamSessionInfoDto` gana `CandidateEmail`. Nuevos DTO de señal, de entrada y de salida.
- **Web**: `TakeExam.razor` muestra la marca de agua y el aviso. `exam-timer.js` ya escucha `visibilitychange`, `focus` y `pageshow`; hay que extenderlo o añadir un guion hermano para las salidas y los pegados. `ResultDetail.razor` y `ReviewResult.razor` muestran el panel de señales.
- **Pruebas**: pruebas de servicio para la permutación estable, la propiedad de la sesión, el rechazo fuera de la sesión en curso y el límite de señales.
- **Documentación**: `README.md` y `documentacion.md` describen las medidas y dejan claro lo que no cubren.
- **Protección de datos**: el registro de actividad es un tratamiento de datos del candidato. El aviso de la bienvenida lo informa antes de empezar.
