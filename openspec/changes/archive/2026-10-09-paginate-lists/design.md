## Context

Ver `proposal.md` — Why. Los requisitos están en `specs/`.

Lo que condiciona el enfoque:

- Los cinco listados cargan todo y filtran y ordenan en el cliente. El mecanismo actual (`SortState`, `ListSort`, `ListState`, `SortHeader`) nació en `list-tools-and-categories` con la paginación como no objetivo declarado. `ListSort` y `SortState` siguen sirviendo a los listados que no se paginan (categorías, cola de corrección, historial del evaluador, portal del alumno).
- Las cifras de `ResultList` y `ResultsByExam` se calculan en el cliente **sobre la lista completa, sin filtros**: aprobados, reprobados, pendientes, nota media y, en una prueba, máxima, mínima y distribución. Con una página en el cliente no se pueden calcular.
- Los filtros de fecha comparan `CompletedAt.ToLocalTime().Date` con el día elegido. El servidor no conoce la zona del navegador.
- El filtro por prueba de `ResultList` usa el **título**, sacado de la propia lista de resultados. Dos pruebas con el mismo título se confunden, y sin la lista completa no hay de dónde sacar los títulos.
- `GetWithStatsAsync` carga cada prueba con `ExamQuestions → Question`, `ExamTokens → ExamSession → ExamResult` para hacer cuatro `Count`. Es R4.
- `QuestionService.GetAllAsync` usa `GetFilteredAsync(…, onlyActive: true)`: la API **solo devuelve preguntas activas**, y la fila de filtros de la pantalla tiene un «Estado: Inactiva» que nunca puede coincidir con nada. Además el método hace `Include(q => q.Answers)` y devuelve las cuatro opciones de cada pregunta, que la tabla no muestra. Hoy es el único llamador de ese método, que el listado paginado sustituye, así que se borra.
- Las pruebas de repositorio usan EF en memoria (`AdminReservationTests`, `BusinessRulesTests`). En memoria, `Contains` de cadenas distingue mayúsculas; en SQL Server, la intercalación de la base no. Hay que escribir la búsqueda para que se comporte igual en las dos.
- `UserManagementService` usa `IRepository<User>`, y sus pruebas simulan `FindAsync`. No hay `IUserRepository`.
- `ExamNew` (composición manual) pide todas las preguntas para que el administrador marque las que quiere. `ExamDetail` pide todos los evaluadores activos para el desplegable de asignación.

## Goals / Non-Goals

**Goals:**

- Que el coste de abrir un listado dependa del tamaño de la página y no del de la tabla.
- Que lo que el administrador ve no cambie, salvo el control de paginación y los tres filtros de preguntas que sobraban.
- Un único contrato, para no tener cuatro formas de paginar.

**Non-Goals:**

- Paginación por cursor, índices nuevos o búsqueda de texto completo.
- Paginar la cola de corrección, el historial del evaluador, el portal del alumno y las categorías.
- Rehacer la composición manual de pruebas con búsqueda en servidor.

## Decisions

### D1. `PageQuery` y `PagedResult<T>` en el dominio

`Domain/Common/Paging.cs`:

```csharp
public sealed class PageQuery
{
    public const int DefaultPageSize = 25, MaxPageSize = 100;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = DefaultPageSize;
    public string? Q { get; set; }
    public string? Sort { get; set; }
    public bool Desc { get; set; }

    public int EffectivePage => Math.Max(1, Page);
    public int EffectivePageSize => PageSize < 1 ? DefaultPageSize : Math.Min(PageSize, MaxPageSize);
    public int Skip => (EffectivePage - 1) * EffectivePageSize;
    public string? Text => string.IsNullOrWhiteSpace(Q) ? null : Q.Trim();
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
    public PagedResult<TOut> Map<TOut>(Func<T, TOut> f) => new(Items.Select(f).ToList(), Total, Page, PageSize);
}
```

En el dominio porque las interfaces de repositorio viven ahí y la Aplicación, la API y la Web ya lo referencian. Es una clase con `set` y no un `record` posicional: el enlazador de `[FromQuery]` de MVC rellena propiedades, y un valor que no es un número (`page=abc`) lo rechaza el enlazador con `400`, y uno fuera de rango (`page=0`) se corrige con las propiedades `Effective*`. Las propiedades `Effective*` son las que se usan; los valores crudos no se leen en ningún otro sitio.

Una página que pasa del final devuelve cero elementos con el `Total` real: el cliente sabe que se pasó y pide la última. Se descarta el `404` porque obliga a cada pantalla a distinguirlo de un fallo.

### D2. El repositorio recibe el filtro y devuelve entidades

Cada repositorio expone un método `GetPagedAsync(filtro, PageQuery, ct)` que:

1. Construye el `IQueryable` con los filtros.
2. Cuenta (`CountAsync`) **antes** de ordenar y paginar.
3. Ordena por una **lista blanca** de columnas: un `switch` de clave a expresión. Una clave desconocida cae en el orden por defecto, y nunca llega a SQL.
4. Desempata por `Id` (siempre en el mismo sentido que el orden pedido para los textos; descendente en los resultados, que se ordenan por fecha descendente).
5. `Skip(q.Skip).Take(q.EffectivePageSize)`.

Los filtros son objetos de dominio (`ResultFilter`, `QuestionFilter`), no parámetros sueltos: la firma no crece cada vez que un listado gana un filtro.

**Usuarios** no tiene repositorio propio. Se añade a `IRepository<T>` un único método genérico:

```csharp
Task<(IReadOnlyList<T> Items, int Total)> GetPageAsync(
    Expression<Func<T, bool>>? filter,
    Func<IQueryable<T>, IOrderedQueryable<T>> order,
    int skip, int take, CancellationToken ct = default);
```

`UserManagementService` le pasa el filtro y el orden. Se descartó un `IUserRepository` aparte: obligaba a cambiar el constructor de `UserManagementService` y las pruebas de todos los servicios que lo construyen, a cambio de nada.

### D3. La búsqueda se escribe igual para SQL Server y para EF en memoria

`q` se compara con `campo.ToLower().Contains(texto.ToLower())`. EF lo traduce a `LOWER(campo) LIKE '%…%'`, con los comodines del texto escapados, y en memoria se comporta igual. No se usa `EF.Functions.Like` ni `StringComparison`: la primera no existe en todos los proveedores y la segunda no se traduce a SQL. El coste es que no usa índices; con búsqueda parcial a ambos lados ningún índice habría servido.

### D4. Resultados

```
GET /api/results?page&pageSize&q&examId&outcome&scoreMin&scoreMax&from&to&sort&desc
GET /api/results/exam/{examId}?…            (igual, con examId fijo)
GET /api/results/stats[?examId=]
```

- `ResultFilter(int? ExamId, string? Outcome, int? ScoreMin, int? ScoreMax, DateTime? From, DateTime? To)`.
- `outcome`: `pending` → `Status == PendingReview`; `passed` → `Status == Reviewed && Passed == true`; `failed` → `Status == Reviewed && Passed == false`; otro valor o vacío → sin filtro. Es lo que hace hoy `MatchesOutcome`.
- `scoreMin`/`scoreMax` exigen `Status == Reviewed`. Es lo que hace hoy el filtro.
- `from` inclusivo y `to` exclusivo, en UTC. El cliente convierte el **día local** elegido a instantes: `from = día 00:00 local → UTC`, `to = (día de «hasta» + 1) 00:00 local → UTC`. Así el servidor no necesita conocer la zona, y el resultado coincide con la comparación de hoy (`ToLocalTime().Date`). En Blazor WebAssembly `TimeZoneInfo.Local` es la zona del navegador.
- Orden: `candidate` (nombre), `exam` (título de la prueba), `score` (los pendientes al final: clave doble `Status == Reviewed ? 0 : 1`, luego nota), `outcome` (`Pending → 2`, `Passed → 0`, `Failed → 1`), `date`. Por defecto `date` descendente. En `results/exam/{id}`, el valor por defecto que usa la pantalla es `score` descendente (el ranking), y lo pide la pantalla; el servidor no lo impone.
- **`ResultStats`**: una sola consulta agregada sobre los corregidos (`GroupBy(_ => 1)` con `Count`, `Sum` de aprobados, `Average`, `Max`, `Min`) más dos `Count` para el total y los pendientes, y seis `Count` para la distribución. Si no hay corregidos, las notas valen 0. `ExamTitle` solo con `examId`; `404` si la prueba no existe. El redondeo es el de hoy: `passRate` entero con `Math.Round(…)` y `avgScore` con un decimal.
- `ExamResultSummaryDto` no cambia.

Alternativa descartada: **cifras que dependen del filtro**. Hoy no lo hacen, y las tarjetas que cambian al filtrar harían que «Candidatos» dejara de ser el total de la prueba.

### D5. Preguntas

`GET /api/questions?categoryId&difficulty&type&active&q&sort&desc&page&pageSize`.

- Nuevo método `IQuestionRepository.GetPagedAsync(QuestionFilter, PageQuery)`, con `Include(q => q.Category)` y **sin** `Include(q => q.Answers)`. `GetFilteredAsync` se borra: ya no lo usa nadie.
- `QuestionFilter(int? CategoryId, DifficultyLevel? Difficulty, QuestionType? Type, bool? Active)`. `q` busca en `Text`.
- Orden: `text`, `category` (nombre), `difficulty`, `type`, `points`, `status`. Por defecto, `category` y luego `difficulty` (el de hoy). Los desempates entre páginas siempre acaban en `Id`.
- `active` es `bool?`: sin él, la API devuelve activas e inactivas; con él, filtra. La pantalla guarda el filtro «Estado» con **«Activas» por defecto**, que es lo que el administrador ve hoy, y por primera vez permite elegir «Inactivas» o «Todas». `GET /api/questions` sin parámetros cambia de «solo activas» a «todas»: el único llamador sin filtro es `ExamNew`, que pasa a pedir `active=true`.

### D6. Pruebas

`GET /api/exams?active&q&sort&desc&page&pageSize` y `GET /api/exams/options`.

- Nuevo método `IExamRepository.GetPagedSummariesAsync(ExamFilter, PageQuery)` que **proyecta**: la consulta devuelve una fila `ExamSummaryRow(Id, Title, TimeLimitMinutes, PassingScorePercentage, IsActive, CreatedAt, QuestionCount, TotalPoints, InvitationCount, ResultCount, AbandonedCount)`, con los recuentos como subconsultas (`e.ExamQuestions.Count()`, `e.ExamQuestions.Sum(eq => eq.Question.Points)`, `e.ExamTokens.Count()`, `e.ExamTokens.Count(t => t.ExamSession.ExamResult != null)`, `…Status == Abandoned`). Resuelve R4 para este listado.
- Los puntos usan `Sum` sobre `Question.Points` con `?? 0`, como hoy.
- `ExamSummaryDto` no cambia; sale del `ExamSummaryRow`.
- `GetWithStatsAsync` se queda: otros llamadores (el aviso de pruebas sin evaluador, el dashboard) lo usan. Se comprueba en la tarea 3.1 y, si ya no lo usa nadie, se borra.
- Orden: `title`, `created`, `questions`, `time`, `invitations`, `results`. Por defecto, `created` descendente. El selector de la pantalla ofrece título, fecha, preguntas y resultados.
- `GET /api/exams/options` → `ExamOptionDto(int Id, string Title)` de todas las pruebas, ordenadas por título sin distinguir mayúsculas. Es acotado por naturaleza (una fila por prueba, dos campos); si algún día no basta, se pagina como el resto.

### D7. Usuarios

`GET /api/users?role&active&q&sort&desc&page&pageSize`. `UserManagementService.ListAsync` construye el filtro y el orden y llama a `GetPageAsync` (D2). Orden: `name`, `email`, `role`, `status`, `created`. Por defecto, rol y luego nombre (el de hoy). `q` busca en nombre y correo (D3). `UserDto` no cambia.

### D8. Controladores y servicios

Los controladores enlazan `[FromQuery] PageQuery` más los filtros propios de cada listado, y los servicios devuelven `PagedResult<TDto>`. No hay lógica nueva en los controladores. Las interfaces `IResultService.GetAllAsync` y `GetByExamAsync` cambian de firma; `IQuestionService.GetAllAsync`, `IExamService.GetAllAsync` y `IUserManagementService.ListAsync` también.

`ResultService.GetDashboardStatsAsync` sigue igual: ya usa agregados (R2).

### D9. El cliente: `ApiService`, `Pager` y un ayudante de carga

- `ApiService` gana métodos con una clase de consulta por listado, y `GetAllPagesAsync<T>(Func<int, Task<PagedResult<T>?>> page)` que pide páginas de 100 hasta agotar `Total` y devuelve `null` si alguna falla. `ExamNew` y `ExamDetail` lo usan.
- `Shared/Pager.razor`: recibe `Page`, `PageSize`, `Total`, `OnPage` y `OnPageSize`. Pinta «Mostrando a–b de N», un `<nav aria-label="Paginación">` con primera, anterior, ventana de tres páginas a cada lado, siguiente y última, y un `<select>` de tamaño (10, 25, 50, 100). La página actual lleva `aria-current="page"`. Los botones sin efecto, `disabled`. Con `Total <= PageSize`, solo «N resultados».
- `Shared/ListLoader.cs` (clase auxiliar, no un componente): serializa las cargas de una pantalla. Cada llamada incrementa un contador; al volver la respuesta, si el contador cambió, se **descarta**. Así una respuesta lenta no sustituye a una reciente. La búsqueda de texto usa un temporizador de 300 ms que se reinicia con cada pulsación (`System.Threading.Timer`, o `Task.Delay` con `CancellationTokenSource`).
- `ListState` guarda además `Page` y `PageSize` en el `record Saved` de cada pantalla. Al restaurar una página que ya no existe (`Page > TotalPages`), la pantalla repite la petición con la última (`TotalPages`).
- Un cambio de búsqueda, de filtro, de orden o de tamaño de página pone `Page = 1`.
- `SortState` y `SortHeader` se reutilizan tal cual: ahora `SortBy` no ordena en memoria, llama a la carga. `ListSort.Apply` deja de usarse en estas cinco pantallas.

### D10. `QuestionList` pierde tres filtros

La fila de filtros de la tabla tenía categoría, nivel y tipo, además de los tres desplegables de arriba, que son de servidor. Eran dos juegos para lo mismo. Con la paginación en servidor, un filtro de cliente solo vería la página actual, así que se **quitan los de la fila** y quedan los de arriba. La búsqueda de texto y el estado se quedan en la fila y pasan a servidor.

### D11. `ResultList` filtra por `examId`

El desplegable de pruebas usa `GET /api/exams/options` y filtra por identificador. Se evita así la confusión de dos pruebas con el mismo título, y no hace falta la lista completa de resultados para llenar el desplegable. `Saved` guarda el `examId` y no el título; un estado guardado por la versión anterior no existe (`ListState` vive en memoria y se reinicia al recargar), así que no hay compatibilidad que mantener.

### D12. Pruebas

- Repositorios con EF en memoria: filtros, búsqueda sin distinguir mayúsculas, orden por cada columna, desempate entre páginas, página más allá del final, límites de `PageQuery`, pendientes al final al ordenar por nota.
- Cifras: aprobados, nota media sin pendientes, sin corregidos, con `examId`, con una prueba inexistente, tramos de distribución en los límites (49/50, 59/60, 89/90, 100).
- Pruebas: los recuentos coinciden con los de `GetWithStatsAsync` para un mismo conjunto de datos.
- `PageQuery`: normalización.
- Las pruebas existentes de `UserManagementService.ListAsync` se adaptan a `GetPageAsync`.
- La Web no tiene proyecto de pruebas: `Pager` y la conversión de día local a UTC se verifican en el navegador. La conversión se pone en una función estática de la Aplicación (`DayRange.ToUtc(DateTime? from, DateTime? to, TimeZoneInfo zone)`), que sí se prueba.

## Risks / Trade-offs

- **Cambio de contrato en cuatro rutas.** Se acepta: no hay más consumidor que la Web, y ambas piezas salen juntas. La mitigación es que la compilación de la Web falla si queda un llamador del contrato viejo (`List<T>` ≠ `PagedResult<T>`).
- **`ExamNew` sigue cargando todo el banco**, ahora en trozos de 100. Es el mismo coste que hoy repartido en más peticiones; lo único que mejora es que ya no depende de un listado sin límite en el servidor. Queda como seguimiento: elegir las preguntas con búsqueda en servidor.
- **Orden por texto en servidor.** Hoy se ordena en el cliente con `es-ES`; en servidor manda la intercalación de la base. Si es sensible a mayúsculas y a tildes distintas de la del navegador, el orden puede diferir en casos raros («Álvaro» frente a «Alvaro»). Se verifica contra `TechEvalDb` en la tarea de cierre.
- **Cifras con las cifras de otro momento.** Las tarjetas y la tabla vienen de dos peticiones; si llega un resultado entre ellas, no cuadran hasta la siguiente recarga. Es aceptable y es el comportamiento de cualquier panel.
- **Desplazamiento (`Skip`) con tablas muy grandes.** `OFFSET` degrada en páginas profundas. A cientos de miles de filas se cambiaría a cursor; hoy, no hay datos que lo justifiquen.
- **Búsqueda con `LOWER(...) LIKE '%…%'`** recorre la tabla. Es igual de costosa que la del cliente pero sin transferir las filas, y el recorrido es en el servidor de base de datos.
