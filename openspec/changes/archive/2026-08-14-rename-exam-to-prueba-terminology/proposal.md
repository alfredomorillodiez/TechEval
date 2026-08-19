## Why

El término "examen" se ve mal como palabra de cara al usuario final (candidatos y administradores). El nombre debe cambiar a "prueba" en todo lo que el usuario ve y navega, sin tocar la infraestructura interna (código, base de datos, contratos de API) que no está expuesta directamente al usuario.

## What Changes

- Todos los textos visibles en español que usan "examen"/"exámenes" pasan a "prueba"/"pruebas": menú lateral, títulos de página (`PageTitle`), encabezados, botones, mensajes de confirmación/error, etiquetas de formulario y modales.
- **BREAKING**: las rutas de navegación (Blazor Router) que exponen la palabra "exam(s)" cambian a "prueba(s)", manteniendo en inglés el resto de segmentos (`new`, `generate`) para no romper la convención existente en rutas hermanas como `/admin/questions/new`:
  - `/admin/exams` → `/admin/pruebas`
  - `/admin/exams/new` → `/admin/pruebas/new`
  - `/admin/exams/generate` → `/admin/pruebas/generate`
  - `/admin/exams/{Id:int}` → `/admin/pruebas/{Id:int}`
  - `/exam/{Token}` → `/prueba/{Token}`
  - `/admin/results/exam/{ExamId:int}` → `/admin/results/prueba/{ExamId:int}`
- No hay redirects de compatibilidad para las rutas antiguas: no hay candidatos con exámenes pendientes actualmente, así que romper enlaces antiguos no tiene impacto.
- Sin cambios en: nombres de clases C#, interfaces, servicios, controllers, DTOs (`Exam`, `ExamService`, `IExamRepository`, `ExamsController`, `ExamDto`, etc.), rutas de la API REST (`/api/exams`, `/api/exam/validate/{token}`, etc.) ni el esquema de base de datos (tablas `Exams`, `ExamResults`, `ExamSessions`, `ExamTokens`, `ExamQuestions`). Esta capa es infraestructura interna en inglés, análoga a que `Question` nunca se traduzca a nivel de código aunque en pantalla diga "Pregunta".

## Capabilities

### New Capabilities

(ninguna — este cambio es un renombrado de terminología visible, no introduce comportamiento nuevo)

### Modified Capabilities

- `admin-console`: los requisitos que documentan las rutas del panel de administración (`/admin/exams`, `/admin/exams/generate`, `/admin/exams/{id}`) y las etiquetas de UI ("Generar examen", "Crear examen", "Enviar examen", menú "Exámenes") se actualizan para reflejar las nuevas rutas `/admin/pruebas*` y el texto "prueba"/"pruebas".
- `candidate-experience`: los requisitos que documentan la ruta pública (`/exam/{Token}`) y los textos de la interfaz de resolución ("Comenzar examen", "Finalizar examen", pantallas de bienvenida/resultado) se actualizan a la ruta `/prueba/{Token}` y al texto "prueba".

Las capacidades `exam-management`, `exam-delivery`, `exam-taking` y `exam-results` documentan comportamiento y contratos de la API backend (`/api/exams`, `/api/exam/validate/{token}`, entidades `Exam*`), que no cambian en este alcance — no requieren delta spec.

## Impact

- **Código afectado**: archivos `.razor` bajo `src/TechEval.Web/Pages/Admin/Exams/`, `src/TechEval.Web/Pages/Admin/Results/`, `src/TechEval.Web/Pages/Exam/` y `src/TechEval.Web/Layout/MainLayout.razor` — directivas `@page` (rutas) y texto literal en español.
- **Enlaces de navegación**: cualquier `href`/`NavLink`/`NavigateTo` que apunte a las rutas antiguas debe actualizarse junto con el renombrado de rutas.
- **Sin impacto** en: API REST, base de datos, entidades de dominio, DTOs, tests de backend (si existen) que dependan de nombres de clase o rutas de API.
- **Riesgo**: bajo. No hay migraciones de base de datos que romper (el esquema se crea con `EnsureCreatedAsync`, sin historial de migraciones) y no hay candidatos con exámenes pendientes que dependan de la ruta pública actual.
