## Why

El hallazgo R1 de la auditoría: ningún listado pagina. Resultados, preguntas, pruebas y usuarios devuelven la tabla entera en cada petición, y el cliente Blazor la recibe completa para filtrarla, ordenarla y calcular las cifras en memoria. El coste crece con el uso, no con la pantalla: con diez mil resultados, abrir `/admin/results` descarga diez mil filas aunque se vean veinticinco.

Dos hechos agravan el problema:

- El listado de pruebas carga cada prueba con todas sus preguntas, invitaciones, sesiones y resultados solo para contarlos (R4).
- El listado de preguntas carga las cuatro opciones de cada pregunta aunque la tabla no las muestra.

## What Changes

- **Contrato común de paginación.** `PageQuery` (página, tamaño, texto, orden, sentido) y `PagedResult<T>` (elementos, total, página, tamaño). Tamaño por defecto 25 y máximo 100. Un valor fuera de rango se corrige, no se rechaza.
- **Cuatro listados pasan a ser paginados en servidor**, con búsqueda, filtros y orden también en servidor:
  - `GET /api/results` y `GET /api/results/exam/{examId}`.
  - `GET /api/questions`.
  - `GET /api/exams`.
  - `GET /api/users`.
- **Las cifras de resultados salen de su propio endpoint.** `GET /api/results/stats` (global, o de una prueba con `?examId=`) devuelve totales, aprobados, nota media, máxima, mínima y la distribución por tramos. Con la lista paginada, el cliente ya no puede calcularlas.
- **`GET /api/exams/options`**: identificador y título de todas las pruebas, para los desplegables que antes los sacaban de la lista completa.
- **Los listados de la Web** muestran una página, un control de paginación accesible, el total y el tamaño de página. El estado guardado (`ListState`) incluye la página. Cambiar un filtro, la búsqueda o el orden vuelve a la página 1.
- **El listado de preguntas pierde los filtros duplicados.** Tenía tres desplegables arriba (categoría, nivel, tipo, de servidor) y otros tres en la fila de filtros de la tabla (de cliente). Quedan los de arriba, más la búsqueda y el estado.
- **Los consumidores que necesitan todo** (la composición manual de una prueba y la lista de evaluadores activos) recorren las páginas con un ayudante del cliente, en lugar de depender de un listado sin límite.

**BREAKING:** las cuatro rutas de listado devuelven `{ items, total, page, pageSize }` y no un array. No hay otros consumidores que la propia Web.

Fuera de alcance:

- La cola de corrección, el historial del evaluador y el portal del alumno: su tamaño está acotado por usuario.
- Las categorías: son pocas y la pantalla las edita todas.
- Búsqueda de texto completo. La búsqueda es una coincidencia parcial sin distinguir mayúsculas, como hoy.
- Paginación por cursor. Con `Skip`/`Take` basta mientras la tabla no pase de cientos de miles de filas.
- Rehacer la composición manual de pruebas con búsqueda en servidor.

## Capabilities

### New Capabilities

- `list-pagination`: contrato de paginación, búsqueda, filtro y orden en servidor de los listados de resultados, preguntas, pruebas y usuarios, y el endpoint de cifras de resultados.

### Modified Capabilities

- `admin-console`: los listados muestran una página con control de paginación; el estado guardado incluye la página; los filtros de resultados y preguntas se aplican en servidor.

## Impact

- **Dominio**: `PageQuery` y `PagedResult<T>`; métodos paginados en `IExamResultRepository`, `IQuestionRepository`, `IExamRepository` y un repositorio o método para usuarios.
- **Infraestructura**: consultas con `Skip`/`Take`, lista blanca de columnas de orden, desempate por identificador; cifras de resultados con una sola consulta agregada; listado de pruebas con los recuentos calculados en SQL.
- **Aplicación**: los servicios devuelven `PagedResult<T>`; `ResultStatsDto` y `ExamOptionDto` nuevos.
- **API**: parámetros de consulta nuevos y forma de respuesta nueva en cuatro rutas; dos rutas nuevas (`results/stats`, `exams/options`).
- **Web**: `ApiService` (consultas con parámetros, `GetAllPagesAsync`), componente `Pager`, y cambios en `ResultList`, `ResultsByExam`, `QuestionList`, `ExamList`, `UserList`, `ExamNew` y `ExamDetail`.
- **Pruebas**: repositorios paginados (filtro, orden, desempate, límites), cifras y distribución, y la lógica pura de normalización.
- **Base de datos**: sin cambios de esquema. Se pueden añadir índices más adelante si las medidas lo piden.
