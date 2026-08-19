## Why

Crear preguntas del banco a mano, una por una, es lento para los administradores. Se quiere poder generar varias preguntas de golpe a partir de un tema en texto libre, usando un modelo de IA que corre localmente (sin depender de un proveedor externo de pago), dejando siempre una revisión humana antes de que esas preguntas puedan usarse en una prueba real.

## What Changes

- Nueva pantalla de administración "Generar preguntas con IA": el administrador elige categoría, dificultad y tipo de pregunta (igual que en la creación manual) y escribe un tema en texto libre junto con la cantidad de preguntas a generar.
- La generación se procesa **en segundo plano**: se encola un job y el administrador puede seguir usando la aplicación mientras corre; no es una llamada síncrona que bloquee la pantalla.
- El motor de generación llama a un modelo de IA local (**Ollama**, modelo `deepseek-r1:7b`) expuesto como un nuevo servicio dentro del mismo `docker-compose.yml`, en la misma red interna que ya usan `api` y `sqlserver` — no es un servicio en la nube ni depende de la máquina de un administrador en particular.
- Las preguntas generadas **nunca se activan automáticamente**: entran con un estado de revisión pendiente y no son elegibles para creación manual/automática de pruebas hasta ser aprobadas.
- Nueva pantalla "Preguntas pendientes de revisión": lista simple con **aprobar**, **rechazar** y **editar** por cada pregunta generada. Si una pregunta puntual del lote falló al generarse o no se pudo interpretar el resultado del modelo, esa fila muestra el error correspondiente sin afectar al resto del lote.
- Nuevo campo de estado de revisión en `Question` (`PendingReview` / `Approved` / `Rejected`), independiente del campo existente `IsActive` (que sigue siendo soft-delete).
- Nueva entidad para agrupar cada solicitud de generación (tema, categoría, dificultad, tipo, cantidad solicitada, estado del job, preguntas resultantes), de forma que la bandeja de revisión pueda mostrar de qué lote salió cada pregunta.
- **BREAKING**: ninguno — es una funcionalidad puramente aditiva; no cambia comportamiento existente de creación manual de preguntas, generación de pruebas ni flujo de examen.
- Explícitamente fuera de alcance: no se pausa ni limita la generación cuando hay candidatos con una prueba en curso (se descartó como escenario real); el motor de IA no elige categoría/dificultad/tipo por sí mismo (los fija el administrador de antemano).

## Capabilities

### New Capabilities
- `ai-question-generation`: motor de generación de preguntas por prompt — el job en segundo plano, la integración con Ollama, el contrato de entrada/salida del modelo, y las acciones de aprobar/rechazar/editar sobre preguntas generadas.

### Modified Capabilities
- `question-bank`: el requisito de filtrado de preguntas activas debe excluir las preguntas en estado `PendingReview` o `Rejected` de cualquier listado usado para construir pruebas (manual o automática), de forma que una pregunta generada por IA nunca llegue a un candidato sin haber sido aprobada.
- `admin-console`: el requisito de gestión del banco de preguntas desde la interfaz se actualiza para incluir, junto a los accesos existentes, un botón "Generar con IA" y un acceso a la bandeja "Pendientes de revisión" desde la pantalla de preguntas (mismo patrón que los botones "Manual"/"Generar automático" que ya existen para pruebas).

## Impact

- **Nueva dependencia de infraestructura**: servicio `ollama` (imagen `ollama/ollama`) añadido a `docker-compose.yml`, corriendo en el mismo servidor que `sqlserver`/`api`/`web` — comparte CPU/RAM con ellos (hardware modesto, sin GPU útil para inferencia; la generación es CPU-bound y puede tardar minutos por lote).
- **Backend**: nuevo `BackgroundService` con cola en memoria para procesar jobs de generación (no se introduce Hangfire, SignalR ni un broker externo — la app corre en una sola instancia); nueva entidad de dominio para el lote/job; migración de base de datos para el nuevo estado de revisión y la nueva tabla.
- **Frontend**: dos páginas nuevas en `TechEval.Web` (formulario de generación, bandeja de revisión) y un nuevo enlace en `MainLayout.razor`.
- **Sin impacto** en: flujo de creación manual de preguntas existente, generación/envío de pruebas, experiencia del candidato, autenticación.
