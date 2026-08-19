## 1. Rutas Blazor (`@page`)

- [x] 1.1 `src/TechEval.Web/Pages/Admin/Exams/ExamList.razor`: cambiar `@page "/admin/exams"` a `@page "/admin/pruebas"`
- [x] 1.2 `src/TechEval.Web/Pages/Admin/Exams/ExamNew.razor`: cambiar `@page "/admin/exams/new"` a `@page "/admin/pruebas/new"`
- [x] 1.3 `src/TechEval.Web/Pages/Admin/Exams/ExamDetail.razor`: cambiar `@page "/admin/exams/{Id:int}"` a `@page "/admin/pruebas/{Id:int}"`
- [x] 1.4 `src/TechEval.Web/Pages/Admin/Exams/GenerateExam.razor`: cambiar `@page "/admin/exams/generate"` a `@page "/admin/pruebas/generate"`
- [x] 1.5 `src/TechEval.Web/Pages/Admin/Results/ResultsByExam.razor`: cambiar `@page "/admin/results/exam/{ExamId:int}"` a `@page "/admin/results/prueba/{ExamId:int}"`
- [x] 1.6 `src/TechEval.Web/Pages/Exam/TakeExam.razor`: cambiar `@page "/exam/{Token}"` a `@page "/prueba/{Token}"`

## 2. Enlaces y navegación internos

- [x] 2.1 `src/TechEval.Web/Layout/MainLayout.razor`: actualizar el `href` del `NavLink` de `/admin/exams` a `/admin/pruebas`
- [x] 2.2 `src/TechEval.Web/Pages/Admin/Dashboard.razor`: actualizar `<a href="/admin/exams">` a `/admin/pruebas`
- [x] 2.3 `src/TechEval.Web/Pages/Admin/Exams/ExamList.razor`: actualizar los `href` a `/admin/exams/new`, `/admin/exams/generate` y `/admin/exams/@exam.Id` a `/admin/pruebas/new`, `/admin/pruebas/generate` y `/admin/pruebas/@exam.Id`
- [x] 2.4 `src/TechEval.Web/Pages/Admin/Exams/ExamNew.razor`: actualizar los `href="/admin/exams"` (volver, cancelar) y `Nav.NavigateTo($"/admin/exams/{result.Id}")` a `/admin/pruebas` y `/admin/pruebas/{result.Id}`
- [x] 2.5 `src/TechEval.Web/Pages/Admin/Exams/ExamDetail.razor`: actualizar `href="/admin/exams"` (volver) a `/admin/pruebas`
- [x] 2.6 `src/TechEval.Web/Pages/Admin/Exams/GenerateExam.razor`: actualizar los `href="/admin/exams"` (volver, cancelar) y `Nav.NavigateTo("/admin/exams")` a `/admin/pruebas`
- [x] 2.7 `src/TechEval.Web/Pages/Admin/Results/ResultList.razor`: actualizar `<a href="/admin/results/exam/@r.ExamId">` a `/admin/results/prueba/@r.ExamId`

## 3. Texto visible — navegación y dashboard

- [x] 3.1 `src/TechEval.Web/Layout/MainLayout.razor`: cambiar la etiqueta del menú lateral "Exámenes" por "Pruebas"
- [x] 3.2 `src/TechEval.Web/Pages/Admin/Dashboard.razor`: cambiar "Exámenes activos" por "Pruebas activas" y el encabezado de columna "Examen" por "Prueba"

## 4. Texto visible — gestión de pruebas (`Pages/Admin/Exams/*.razor`)

- [x] 4.1 `ExamList.razor`: `PageTitle` "Exámenes — TechEval" → "Pruebas — TechEval"; encabezado "Exámenes" → "Pruebas"; "No hay exámenes creados aún." → "No hay pruebas creadas aún."; título del modal "Enviar examen" → "Enviar prueba"; mensaje "Examen enviado correctamente a {email}" → "Prueba enviada correctamente a {email}"; error "Error al enviar el examen. Verifica la configuración de email." → "Error al enviar la prueba. Verifica la configuración de email."
- [x] 4.2 `ExamNew.razor`: `PageTitle` "Nuevo examen — TechEval" → "Nueva prueba — TechEval"; encabezado "Nuevo examen manual" → "Nueva prueba manual"; botón "Crear examen" → "Crear prueba"; error "Error al crear el examen. Inténtalo de nuevo." → "Error al crear la prueba. Inténtalo de nuevo."
- [x] 4.3 `ExamDetail.razor`: `PageTitle` "Editar examen — TechEval" → "Editar prueba — TechEval"; alerta "No se encontró el examen." → "No se encontró la prueba."; etiqueta "Examen activo" → "Prueba activa"; título del modal "Enviar examen al candidato" → "Enviar prueba al candidato"; mensaje "Examen enviado correctamente a {email}" → "Prueba enviada correctamente a {email}"; error "Error al enviar el examen. Verifica la configuración de email." → "Error al enviar la prueba. Verifica la configuración de email."
- [x] 4.4 `GenerateExam.razor`: `PageTitle` "Generar examen — TechEval" → "Generar prueba — TechEval"; encabezado "Generar examen automático" → "Generar prueba automática"; etiqueta "Título del examen *" → "Título de la prueba *"; botón "Generar examen" → "Generar prueba"

## 5. Texto visible — resultados (`Pages/Admin/Results/*.razor`)

- [x] 5.1 `ResultList.razor`: etiqueta de filtro "Examen" → "Prueba"; opción "Todos los exámenes" → "Todas las pruebas"; encabezado de columna "Examen" → "Prueba"; `title` "Ver estadísticas de este examen" → "Ver estadísticas de esta prueba"
- [x] 5.2 `ResultDetail.razor`: etiqueta "Examen" → "Prueba"
- [x] 5.3 `ResultsByExam.razor`: `PageTitle` "Estadísticas del examen — TechEval" → "Estadísticas de la prueba — TechEval"; texto "Estadísticas del examen" → "Estadísticas de la prueba"; mensaje "Este examen aún no tiene resultados registrados." → "Esta prueba aún no tiene resultados registrados."; texto de fallback `$"Examen #{ExamId}"` → `$"Prueba #{ExamId}"`

## 6. Texto visible — experiencia del candidato (`Pages/Exam/TakeExam.razor`)

- [x] 6.1 `PageTitle` "Examen técnico — TechEval" → "Prueba técnica — TechEval"
- [x] 6.2 "Validando enlace de examen..." → "Validando enlace de prueba..."
- [x] 6.3 "Una vez iniciado no puedes pausar el examen." → "Una vez iniciada no puedes pausar la prueba."
- [x] 6.4 Botón "Comenzar examen" → "Comenzar prueba"
- [x] 6.5 Botón "Finalizar examen" → "Finalizar prueba"
- [x] 6.6 Diálogo "¿Finalizar el examen?" → "¿Finalizar la prueba?"
- [x] 6.7 Botón "Continuar examen" → "Continuar prueba"

## 7. Verificación final

- [x] 7.1 Buscar en `src/TechEval.Web` cualquier resto de "examen"/"exámenes"/"exam" en texto visible o rutas `@page`/`href`/`NavigateTo` que no haya sido cubierto arriba, y corregirlo
- [x] 7.2 Confirmar que no se modificó ningún nombre de clase, interfaz, servicio, controller, DTO ni ruta de API (`/api/exams*`, `/api/exam/*`, `/api/results/exam/{examId}`) — deben seguir intactos
- [ ] 7.3 Levantar la aplicación y navegar manualmente: menú lateral, listado de pruebas, crear/generar/editar prueba, enviar prueba a un candidato, listado y detalle de resultados, y el flujo completo de `/prueba/{token}` como candidato, verificando que no queda ningún enlace roto ni texto sin actualizar
