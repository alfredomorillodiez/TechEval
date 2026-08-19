## Context

La aplicación TechEval usa "examen"/"exam" de forma consistente en cinco capas: texto visible en español, rutas de navegación Blazor, identificadores de código C# (`Exam`, `ExamService`, `IExamRepository`...), rutas de la API REST (`/api/exams`, `/api/exam/validate/{token}`...) y el esquema de base de datos (tablas `Exams`, `ExamResults`, etc., creadas vía `EnsureCreatedAsync`, sin migraciones).

El disparador del cambio es puramente de percepción de producto: "examen" suena mal como palabra de cara al usuario final. No hay ningún requisito funcional nuevo. Se confirmó en exploración previa que:
- No hay candidatos con exámenes pendientes actualmente → romper la ruta pública antigua no tiene impacto.
- La base de datos no tiene historial de migraciones → no hay riesgo de romper un esquema desplegado si en el futuro se decide renombrar esa capa (fuera de alcance de este cambio).

## Goals / Non-Goals

**Goals:**
- Sustituir "examen"/"exámenes" por "prueba"/"pruebas" en todo el texto visible en español de la interfaz (menú lateral, `PageTitle`, encabezados, botones, mensajes, etiquetas de filtro/formulario, modales).
- Renombrar las rutas de navegación de Blazor que contienen el segmento `exam`/`exams` a `prueba`/`pruebas`, y actualizar cualquier `href`, `NavLink` o `NavigationManager.NavigateTo` que apunte a esas rutas.

**Non-Goals:**
- No se renombran clases, interfaces, servicios, controllers ni DTOs en C# (`Exam`, `ExamService`, `IExamRepository`, `ExamsController`, `ExamDto`, `ExamResult`, `ExamSession`, `ExamToken`, `ExamQuestion`, etc.). Son infraestructura interna en inglés, equivalente a que `Question` nunca se traduzca en código aunque la UI diga "Pregunta".
- No se renombran las rutas de la API REST (`/api/exams`, `/api/exams/generate`, `/api/exams/send`, `/api/exam/validate/{token}`, `/api/exam/start/{token}`, `/api/results/exam/{examId}`, etc.). Son un contrato entre frontend y backend, no texto expuesto directamente al usuario.
- No se renombran tablas ni columnas de base de datos.
- No se añaden rutas de redirección/compatibilidad para las URLs antiguas (`/admin/exams*`, `/exam/{Token}`): se confirmó que no hay uso activo que proteger.
- No se traducen otros segmentos de ruta ya en inglés (`new`, `generate`, `results`, `questions`, `dashboard`) para mantener consistencia con el resto de la navegación.

## Decisions

**1. Cambiar solo texto e identificadores de rutas de Blazor, no código C# ni la API.**
Alternativa considerada: renombrar también las entidades de dominio y la API (`Exam` → `Test`/`Prueba`) para que el código "hable el mismo idioma" que la UI.
Se descarta porque: (a) es un refactor de ~40 archivos across Domain/Application/Infrastructure/API sin ningún beneficio funcional; (b) el usuario confirmó explícitamente que el problema es solo la palabra que ve el usuario final, no la nomenclatura interna; (c) mezclar inglés/español en nombres de dominio (`Prueba`, `PruebaService`) sería más inconsistente que mantener el inglés como convención interna uniforme (igual que `Question`/`User`/`Result` ya son inglés puro).

**2. Mantener el mismo `Requirement` header en los deltas de spec, solo actualizar el cuerpo.**
Los requisitos afectados en `admin-console` y `candidate-experience` describen comportamiento (rutas, textos citados) que cambia, pero el *nombre* del requisito es documentación interna, no texto de usuario. Se usa `## MODIFIED Requirements` conservando el título original y actualizando únicamente la descripción/escenarios, siguiendo el flujo estándar de OpenSpec (evita mezclar una operación RENAMED innecesaria con el cambio de contenido).

**3. Sin redirects de compatibilidad para rutas antiguas.**
Alternativa: añadir rutas antiguas como alias (`@page "/admin/exams"` adicional a `@page "/admin/pruebas"`) para no romper bookmarks.
Se descarta porque no hay candidatos con exámenes pendientes ni evidencia de bookmarks de administradores a rutas específicas; añadir alias sería complejidad permanente sin beneficio medible ahora. Si en el futuro se detecta necesidad, se puede añadir como cambio separado.

## Risks / Trade-offs

- [Enlaces externos ya compartidos (p. ej. un token de examen ya enviado a un candidato antes de este cambio) dejan de funcionar] → Mitigación: confirmado que no hay candidatos con exámenes pendientes; no aplica en este momento.
- [Inconsistencia terminológica temporal si se olvida algún texto o ruta] → Mitigación: `tasks.md` incluye una verificación final con búsqueda de "examen"/"exámenes"/"exam" restante en archivos `.razor` de la capa Web, limitada a texto/rutas (excluyendo nombres de clase C# y rutas de API, que son intencionalmente inmutables).
- [Confusión futura entre el dominio interno `Exam` y el término de producto "prueba"] → Mitigación: es el mismo patrón ya usado por `Question`/"Pregunta"; no introduce un caso nuevo, solo lo extiende de forma coherente.

## Migration Plan

No aplica migración de datos (no hay migraciones de EF Core; el esquema se recrea en desarrollo vía `EnsureCreatedAsync`). El "despliegue" de este cambio es simplemente el build/deploy normal de `TechEval.Web` con los archivos `.razor` y `app.css` actualizados. No requiere pasos de rollback especiales más allá de revertir el commit.
