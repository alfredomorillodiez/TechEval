## 1. Servicios base
- [x] 1.1 Implementar `ApiService` con métodos tipados para autenticación, categorías, preguntas, exámenes, envío (individual y masivo) y resultados, con manejo de errores HTTP/JSON
- [x] 1.2 Implementar `AuthStateService` con persistencia de token y nombre de usuario en `localStorage`, restauración al iniciar la app y notificación de cambios de estado

## 2. Autenticación y sesión
- [x] 2.1 Implementar la página de login (`Login.razor`) con validación de campos, envío con Enter, mensaje de credenciales incorrectas y redirección a `/admin` si ya hay sesión válida
- [x] 2.2 Implementar `MainLayout` con sidebar de navegación condicionada a sesión activa, nombre del usuario conectado y cierre de sesión
- [x] 2.3 Proteger cada página de `/admin/*` verificando la sesión al inicializar y redirigiendo a `/login` en caso contrario

## 3. Dashboard
- [x] 3.1 Implementar `Dashboard.razor` con tarjetas de resumen enlazadas a sus páginas de detalle y tabla de últimas evaluaciones

## 4. Banco de preguntas
- [x] 4.1 Implementar `QuestionList.razor` con filtros por categoría, dificultad, tipo y estado, búsqueda local y baja de preguntas
- [x] 4.2 Implementar `QuestionForm.razor` como formulario único de creación/edición que se adapta según el tipo de pregunta (opciones de test con selección de la correcta, o respuesta modelo para preguntas abiertas)

## 5. Gestión y generación de exámenes
- [x] 5.1 Implementar `ExamList.razor` con tarjetas por examen y acciones de editar, ver resultados y eliminar
- [x] 5.2 Implementar `GenerateExam.razor` para la generación automática de exámenes a partir de número de preguntas, categorías y dificultad, con manejo del error de preguntas insuficientes

## 6. Envío de exámenes
- [x] 6.1 Implementar el modal de envío embebido en `ExamList.razor` con selector de modo individual/masivo
- [x] 6.2 Implementar el flujo de envío individual con confirmación en el propio modal
- [x] 6.3 Implementar el flujo de envío masivo con alta manual de candidatos e importación desde CSV pegado, y el resumen de envíos correctos/fallidos

## 7. Resultados
- [x] 7.1 Implementar `ResultList.razor` con KPIs agregados (total, aprobados, reprobados, nota media)
- [x] 7.2 Implementar filtros combinables por candidato/email, examen, resultado, rango de nota y rango de fechas, y navegación al detalle de cada resultado
