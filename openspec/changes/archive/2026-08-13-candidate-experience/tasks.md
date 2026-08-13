## 1. Enrutamiento y validación de token

- [x] 1.1 Crear la página `TakeExam.razor` enrutada en `/exam/{Token}` como punto de entrada público, sin autenticación.
- [x] 1.2 Al inicializar el componente, invocar la validación del token contra el backend y modelar el resultado como estado `Validating → Invalid | Welcome` mediante un enum de estados del componente.

## 2. Pantalla de bienvenida e inicio de sesión

- [x] 2.1 Construir la pantalla de bienvenida con título del examen, nombre del candidato, número de preguntas y tiempo límite, obtenidos de la validación del token.
- [x] 2.2 Implementar el botón "Comenzar examen" que solicita el inicio de sesión al backend, inicializa los borradores de respuesta en memoria (uno por pregunta) y transiciona el estado a `InProgress`.

## 3. Temporizador

- [x] 3.1 Implementar un temporizador local en cuenta atrás inicializado con el tiempo límite en segundos, actualizado cada segundo mientras el estado es `InProgress`.
- [x] 3.2 Dar formato mm:ss al tiempo restante y aplicar estilos visuales progresivos (normal / advertencia bajo 5 minutos / crítico bajo 1 minuto).
- [x] 3.3 Liberar el temporizador (`Dispose`) al destruir el componente para evitar fugas de recursos.

## 4. Renderizado y navegación de preguntas

- [x] 4.1 Renderizar preguntas tipo test como opciones de selección única, resaltando la opción marcada por el candidato.
- [x] 4.2 Renderizar preguntas abiertas como área de texto libre, precargada con el borrador existente.
- [x] 4.3 Implementar navegación entre preguntas (anterior / siguiente) y un mini-mapa de navegación rápida que marque visualmente las preguntas ya respondidas.

## 5. Auto-guardado de respuestas

- [x] 5.1 Guardar automáticamente la respuesta seleccionada en preguntas tipo test en el momento de la selección.
- [x] 5.2 Guardar automáticamente la respuesta de preguntas abiertas al perder el foco del campo de texto.

## 6. Envío del examen

- [x] 6.1 Implementar el diálogo de confirmación de envío manual, mostrando cuántas preguntas quedaron respondidas antes de confirmar.
- [x] 6.2 Implementar el envío automático del examen al llegar el temporizador a cero, deteniendo el temporizador y omitiendo el diálogo de confirmación.
- [x] 6.3 Unificar el envío manual y el automático en una misma rutina que remita el conjunto completo de respuestas al backend y transicione al estado `Completed`.

## 7. Pantalla de resultado

- [x] 7.1 Mostrar en la pantalla final si el candidato aprobó o no, el porcentaje de puntuación y los puntos obtenidos sobre el total.
- [x] 7.2 Incluir el aviso de que el detalle del resultado también llegará por email.

## 8. UI responsive

- [x] 8.1 Maquetar todas las pantallas con Bootstrap (tarjetas, badges, barra de progreso) asegurando un uso correcto en dispositivos móviles y de escritorio.
