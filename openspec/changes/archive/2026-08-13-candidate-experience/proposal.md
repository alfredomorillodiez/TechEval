# Proposal: candidate-experience

## Why

Con `exam-taking` la API ya expone los endpoints públicos para validar un token, iniciar una sesión de examen, guardar respuestas incrementalmente y enviar el examen completo. Pero ese contrato por sí solo no es una experiencia usable: el candidato necesita una interfaz que consuma esos endpoints en el orden correcto, le muestre con claridad cuánto tiempo le queda, le permita responder a su ritmo sin miedo a perder el trabajo hecho, y le confirme visualmente que su examen fue recibido.

Esta capacidad cierra el flujo de cara al candidato con una única pantalla Blazor WebAssembly que guía todo el recorrido — desde que abre el enlace del email hasta que ve su resultado — sin exigir instalación, cuenta de usuario ni conexión estable de principio a fin. Al ejecutarse enteramente en el navegador del candidato, el examen puede sobrevivir a caídas puntuales de red porque las respuestas quedan guardadas en el servidor pregunta por pregunta, no solo al final.

## What Changes

- Pantalla de bienvenida que, tras validar el token del enlace recibido por email, muestra el título del examen, el nombre del candidato, el número de preguntas y el tiempo límite antes de que el candidato decida iniciar.
- Inicio de examen con temporizador visible en cuenta atrás (mm:ss), con aviso visual progresivo (normal → advertencia bajo 5 minutos → crítico bajo 1 minuto) y barra de progreso de preguntas.
- Renderizado de preguntas tipo test (opciones de selección única) y de preguntas abiertas (área de texto libre), con navegación entre preguntas y mini-mapa de navegación rápida que indica preguntas respondidas.
- Auto-guardado de cada respuesta contra el backend en el momento en que el candidato la marca o pierde el foco del campo de texto, para no depender del envío final.
- Auto-envío del examen en el instante en que el temporizador llega a cero, sin intervención del candidato.
- Envío manual del examen antes de agotar el tiempo, con un diálogo de confirmación que resume cuántas preguntas quedaron respondidas.
- Pantalla final de confirmación con el resultado (aprobado/no aprobado, puntuación y puntos obtenidos) y aviso de que el detalle llegará también por email.
- UI responsive basada en Bootstrap, usable tanto en escritorio como en dispositivos móviles, sin necesidad de autenticación ni instalación de software adicional por parte del candidato.

## Capabilities

### New Capabilities

- `candidate-experience`: Interfaz Blazor WebAssembly de cara al candidato que valida su enlace de examen, le guía a través de la pantalla de bienvenida, la resolución de preguntas con temporizador y auto-guardado, el envío (manual o automático por tiempo agotado) y la confirmación final del resultado.

### Modified Capabilities

(ninguna)

## Impact

- Nueva página Blazor `TechEval.Web/Pages/Exam/TakeExam.razor`, enrutada en `/exam/{Token}`, sin autenticación previa.
- Consume exclusivamente los endpoints públicos de `exam-taking` (`validate`, `start`, `answer`) y `exam-results` (`submit`), a través de `ApiService`.
- Depende de `project-architecture` para el consumo de DTOs compartidos (`ExamSessionInfoDto`, `SubmitAnswerDto`, `SubmitExamDto`, `ExamResultDto`) y de `exam-delivery` para la existencia previa del enlace enviado por email.
- No introduce nuevos endpoints ni entidades de dominio: es puramente una capa de presentación sobre contratos ya existentes.
