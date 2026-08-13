# Design: candidate-experience

## Context

El candidato accede al examen a través de un enlace de un solo uso enviado por email (`exam-delivery`), sin registrarse ni autenticarse. La API pública de `exam-taking` ya expone `validate`, `start` y `answer`, y `exam-results` expone `submit` con la corrección automática. Esta capacidad decide cómo se construye la interfaz que hilvana esas llamadas en una experiencia continua de principio a fin, ejecutada por completo en el navegador del candidato.

El entorno real en el que se completa un examen técnico no es de laboratorio: candidatos en redes domésticas, portátiles corporativos con proxy, o conexiones móviles inestables. La interfaz debe tolerar eso sin que el candidato pierda su progreso ni la sesión quede en un estado inconsistente.

## Goals / Non-Goals

**Goals**
- Una sola página Blazor (`TakeExam.razor`) que gestione todo el ciclo: validación → bienvenida → resolución → envío → resultado, como una máquina de estados explícita.
- Cero pérdida de respuestas ya guardadas ante un cierre de pestaña o corte de conexión, mediante auto-guardado incremental en el backend.
- Cumplimiento estricto del tiempo límite mediante auto-envío, sin depender de que el candidato esté atento al reloj.
- UI utilizable en pantallas pequeñas sin JavaScript adicional más allá del runtime de Blazor y Bootstrap.

**Non-Goals**
- No se implementa persistencia offline en el navegador (localStorage/IndexedDB): el auto-guardado depende de que cada petición llegue al backend en el momento en que ocurre, no de una cola local que reintente sin conexión.
- No se implementa recuperación de sesión tras cierre de pestaña en mitad del examen más allá de lo que ya resuelve `exam-taking` (reutilización de sesión en progreso); esta capacidad solo consume ese comportamiento, no lo diseña.
- No se corrige ni puntúa nada en el cliente: toda la lógica de corrección vive en `exam-results`, la UI solo muestra el resultado que el backend devuelve.

## Decisions

### Blazor WebAssembly en vez de Blazor Server
Se eligió Blazor WebAssembly para la experiencia del candidato porque se ejecuta enteramente en el cliente: no hay estado de sesión de UI en el servidor, lo que permite escalar trivialmente el número de candidatos concurrentes sin gestionar circuitos por usuario. Igual de importante: el examen sigue siendo usable aunque la conexión del candidato sea inestable, porque cada respuesta se guarda en el backend en el momento en que se produce (ver auto-guardado más abajo) y el estado de navegación entre preguntas vive en memoria del propio navegador, sin depender de un socket abierto. Blazor Server habría exigido SignalR con una conexión persistente durante toda la duración del examen — un riesgo real para candidatos con redes poco fiables, donde una desconexión momentánea podría interrumpir la interacción o incluso perder el circuito de servidor a mitad de examen.

### Máquina de estados explícita en el componente
El componente modela el flujo como un enum (`Validating → Invalid | Welcome → InProgress → Completed`) en lugar de banderas booleanas sueltas, para que cada pantalla sea mutuamente excluyente y el estado del examen sea siempre inequívoco, evitando que, por ejemplo, el temporizador siga corriendo mientras se muestra la pantalla de resultado.

### Auto-guardado por evento, no por intervalo
Las respuestas tipo test se guardan en el instante de la selección (`onchange`) y las respuestas abiertas al perder el foco del campo (`onblur`), en lugar de un guardado periódico por temporizador. Esto minimiza las llamadas de red innecesarias (no se guarda mientras el candidato sigue escribiendo) y garantiza que la última interacción del candidato con cada pregunta quede persistida sin que dependa de un intervalo arbitrario que pudiera no dispararse antes de un cierre de pestaña.

### Temporizador de cliente con auto-envío
El temporizador en cuenta atrás vive en el cliente (temporizador local de la página) e inicializa su valor a partir del tiempo límite devuelto por el backend al iniciar la sesión. Al llegar a cero, dispara el mismo flujo de envío que el botón manual, garantizando que ningún candidato pueda continuar respondiendo tras agotar su tiempo, y que el examen siempre termine enviado, incluso si el candidato abandona la pestaña.

## Risks / Trade-offs

- **Deriva de reloj cliente-servidor**: el temporizador se basa en el reloj del navegador del candidato, no en el del servidor. Un reloj de sistema muy desajustado no afecta a la duración percibida (se cuenta hacia atrás desde el tiempo límite, no contra una hora absoluta), pero sí implica que el servidor no impone de forma independiente un corte de tiempo; se asume que `exam-taking`/`exam-results` no necesitan validar server-side el tiempo transcurrido para esta primera versión.
- **Pérdida de respuesta en curso al cerrar la pestaña**: si el candidato cierra el navegador mientras escribe una respuesta abierta sin que el campo haya perdido el foco todavía, esa última edición no llega a guardarse. Se acepta este riesgo porque el disparador `onblur` cubre el caso normal de "termino de escribir y avanzo a la siguiente pregunta", y añadir guardado por cada tecla incrementaría notablemente el tráfico de red sin necesidad real.
- **Ausencia de reintentos ante fallo de red en el auto-guardado**: si una llamada de auto-guardado falla (por ejemplo, un corte de red momentáneo), la interfaz no reintenta automáticamente esa petición concreta; la siguiente interacción del candidato con la misma pregunta sí generará una nueva llamada. Se acepta como compromiso razonable para esta versión, dado que el objetivo principal (no perder el conjunto del examen ante una conexión inestable) queda cubierto por el guardado incremental en sí.
