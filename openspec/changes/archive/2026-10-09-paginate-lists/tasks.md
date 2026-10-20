## 1. Contrato y repositorios

- [x] 1.1 `PageQuery` y `PagedResult<T>` en `Domain/Common/Paging.cs` (design.md D1). Verificar con pruebas: página 0 y negativa → 1; tamaño 0, negativo → 25; 5000 → 100; `Skip`; texto en blanco → nulo; `TotalPages` con total 0, exacto y con resto; `Map`.
- [x] 1.2 `IRepository<T>.GetPageAsync` y su implementación en `BaseRepository` (design.md D2). Verificar con una prueba en EF en memoria: filtro, orden, `Skip`/`Take` y total.
- [x] 1.3 `ResultFilter` y `IExamResultRepository.GetPagedAsync` con el filtro, la búsqueda, el orden por lista blanca y el desempate por `Id` (design.md D2, D3, D4). Verificar con pruebas en memoria: búsqueda por nombre y por correo sin distinguir mayúsculas; cada valor de `outcome`; rango de nota que excluye pendientes; `from` inclusivo y `to` exclusivo; cada columna de orden en las dos direcciones; pendientes al final al ordenar por nota; dos páginas seguidas con la misma nota sin repetir ni omitir; página más allá del final.
- [x] 1.4 `IExamResultRepository.GetStatsAsync(int? examId)` (design.md D4). Verificar con pruebas: totales, aprobados, nota media sin pendientes, máxima y mínima, sin corregidos (todo 0), con `examId`, y los tramos de la distribución en los límites 49/50, 59/60, 69/70, 79/80, 89/90 y 100.
- [x] 1.5 `QuestionFilter` y `IQuestionRepository.GetPagedAsync` sin cargar las opciones; borrar `GetFilteredAsync` (design.md D5). Verificar con pruebas: cada filtro y su combinación, búsqueda en el enunciado, `active` en sus tres estados, cada columna de orden y el desempate. Verificar que `grep GetFilteredAsync src` no devuelve nada.
- [x] 1.6 `ExamFilter`, `ExamSummaryRow` e `IExamRepository.GetPagedSummariesAsync` con los recuentos calculados en la consulta, y `GetOptionsAsync` (design.md D6). Verificar con pruebas: los recuentos de preguntas, puntos, invitaciones, resultados y abandonadas coinciden con los que da `GetWithStatsAsync` para los mismos datos; búsqueda por título; `active`; cada columna de orden; las opciones salen ordenadas por título.
- [x] 1.7 `DayRange.ToUtc` en Aplicación (design.md D4, D12). Verificar con pruebas en zonas distintas (`Europe/Madrid`, UTC, `America/Bogota`): un día en horario de verano, otro en el cambio de hora y un rango de un solo día.

## 2. Servicios y API

- [x] 2.1 `ResultService` y `ResultsController`: `GetAllAsync` y `GetByExamAsync` paginados con los filtros, y `GET /api/results/stats` con `404` para una prueba inexistente (design.md D4, D8). Verificar con pruebas de servicio y con `curl` contra la API: las tres rutas, los límites de `PageQuery` y el `404`.
- [x] 2.2 `QuestionService` y `QuestionsController` paginados (design.md D5, D8). Verificar con pruebas de servicio y `curl`.
- [x] 2.3 `ExamService` y `ExamsController` paginados, más `GET /api/exams/options` (design.md D6, D8). Comprobar si `GetWithStatsAsync` sigue teniendo llamadores y, si no, borrarlo. Verificar con pruebas de servicio y `curl`.
- [x] 2.4 `UserManagementService.ListAsync` y `UsersController` paginados (design.md D7). Adaptar las pruebas existentes de `ListAsync`. Verificar que toda la batería sigue verde.

## 3. Web: infraestructura

- [x] 3.1 `ApiService`: métodos con consulta para los cuatro listados, `GetResultStatsAsync`, `GetExamOptionsAsync` y `GetAllPagesAsync` (design.md D9). Verificar que compila y que no queda ningún llamador del contrato anterior (la compilación falla si queda alguno).
- [x] 3.2 `Shared/Pager.razor` (design.md D9). Verificar en el navegador: rango mostrado, primera, anterior, siguiente y última, ventana de páginas, tamaño de página, `aria-current` en la actual, botones sin efecto deshabilitados, una sola página, y con el teclado.
- [x] 3.3 `ListLoader` (cargas que descartan respuestas viejas y búsqueda con espera de 300 ms) (design.md D9). Verificar con una prueba que una respuesta lenta anterior no sustituye a una rápida posterior, y que cinco pulsaciones seguidas hacen una sola petición.

## 4. Web: pantallas

- [x] 4.1 `ResultList`: filtros, orden y página en servidor; cifras desde `results/stats`; filtro por prueba con `examId` y las opciones de `exams/options` (design.md D4, D11). Verificar en el navegador: buscar, cada filtro, rango de fechas en hora local, ordenar, pasar de página, abrir una ficha y volver a la misma página, y que las cifras de arriba no cambian al filtrar.
- [x] 4.2 `ResultsByExam`: lo mismo, con las cifras y la distribución desde `results/stats?examId=` y el título de la prueba desde la misma respuesta (design.md D4). Verificar en el navegador, incluido el cambio de una prueba a otra sin heredar la página ni los filtros.
- [x] 4.3 `QuestionList`: filtros en servidor, quitar los tres filtros duplicados de la fila de la tabla y el filtro «Estado» con «Activas» por defecto (design.md D5, D10). Verificar en el navegador: cada filtro, ver las inactivas, borrar una pregunta en una página con varias filas y en una con solo una (debe volver a la anterior si la página queda vacía).
- [x] 4.4 `ExamList` (tarjetas): buscador y selector de orden en servidor y paginación (design.md D6). Verificar en el navegador, y que el modal de envío sigue abriendo la prueba correcta tras cambiar de página.
- [x] 4.5 `UserList`: búsqueda, filtros, orden y página en servidor (design.md D7). Verificar en el navegador, incluidas las acciones sobre un usuario (desactivar, cambiar rol, reenviar enlace) que recargan la página actual y no la primera.
- [x] 4.6 `ExamNew` y `ExamDetail`: recorrer las páginas con `GetAllPagesAsync` (design.md D9). Verificar en el navegador con más de 100 preguntas en el banco, y que un fallo en la segunda página muestra el error de carga y no una lista de 100.
- [x] 4.7 Estado conservado: `Saved` de las cinco pantallas incluye `Page` y `PageSize`, y una página que ya no existe cae en la última. Verificar en el navegador, y que cerrar sesión y entrar con otra cuenta deja todo en la página 1.

## 5. Cierre

- [x] 5.1 Recorrido completo contra `TechEvalDb` o una base de prueba con volumen: al menos 300 resultados, 150 preguntas y 40 usuarios sembrados. Medir el tamaño de la respuesta de `GET /api/results` antes y después, y comprobar que el orden por texto con tildes en servidor coincide con el del cliente en los casos «Álvaro», «Andrés», «Zoe». Medido el 09·10·2026 con 360 resultados, 153 preguntas, 40 usuarios y 4 pruebas: la página por defecto de `GET /api/results` pesa 5,5 KB frente a 79 KB el listado entero (las cuatro páginas de 100 juntas). El orden del servidor es Álvaro < Andrés < Beatriz < Carlos < Ñandú < Zoe, el mismo que el de es-ES.
- [x] 5.2 `documentacion.md` y `README.md`: contrato de paginación, rutas nuevas y cambios de las rutas existentes. `openspec validate paginate-lists --strict` pasa.
- [x] 5.3 Suite completa en verde y archivo del cambio.
