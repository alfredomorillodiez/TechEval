## Context

Ver `proposal.md` — Why para la motivación. Los requisitos están en `specs/`.

Lo que condiciona el enfoque:

- `ExamService.GenerateAsync` hace hoy una sola llamada a `QuestionRepository.GetRandomAsync(count, categoryIds, difficulty)`, que filtra por categorías y por un nivel opcional y ordena por `NEWID()`. Si devuelve menos de las pedidas, lanza `ValidationException` con «Se encontraron X de Y».
- `GenerateExamDto` es un record posicional (`Title, Description, TimeLimitMinutes, PassingScorePercentage, QuestionCount, CategoryIds, Difficulty`). La Web lo construye en `GenerateExam.razor`.
- `POST /api/exams/generate` devuelve `201` con el `ExamDto` creado. `ExamDto` lleva `Questions` con el `Difficulty` de cada una, así que el reparto real de cualquier prueba se puede calcular en la Web sin cambiar el contrato.
- La Web referencia `TechEval.Application`: puede usar el mismo código de cálculo que el servidor.
- `ApiService.GenerateExamAsync` devuelve null ante cualquier error, y la pantalla muestra siempre el mismo mensaje fijo.

## Goals / Non-Goals

**Goals:**

- Que la vista previa y el servidor calculen el reparto con el mismo código.
- Que el cálculo sea una función pura, probada sin base de datos.
- Que las peticiones actuales (sin reparto) se comporten igual que hoy.

**Non-Goals:**

- Completar un nivel con otro. Si un nivel no alcanza, se rechaza.
- Guardar el reparto pedido en la prueba. Como nunca se ajusta, el reparto real de sus preguntas es el pedido.

## Decisions

### D1. Un planificador puro en la capa de aplicación

```csharp
public static class DifficultyPlanner
{
    public static DifficultyPlan Plan(
        int total,
        IReadOnlyDictionary<DifficultyLevel, int> percentages,
        IReadOnlyDictionary<DifficultyLevel, int> available);
}

public record DifficultyPlan(
    IReadOnlyDictionary<DifficultyLevel, int> Requested,   // mayor resto
    IReadOnlyList<LevelShortage> Shortages);               // vacío: se puede generar

public record LevelShortage(DifficultyLevel Level, int Needed, int Available);
```

Pasos:

1. **Validar:** tres niveles, cada uno de 0 a 100, suma 100. Si no, `ValidationException`, porque es un error de la petición.
2. **Mayor resto:** `floor(total × p / 100)` por nivel; los restantes, uno a uno, al mayor decimal; empate → mayor porcentaje; empate → nivel más fácil.
3. **Faltantes:** un `LevelShortage` por cada nivel con `Requested > available`.

El planificador no lanza por falta de preguntas: devuelve los faltantes. Así la vista previa puede marcar el nivel, y el servicio construye el mensaje del rechazo.

Alternativa descartada: **un endpoint de vista previa en el servidor.** Una llamada por cada tecla, y dos sitios que pueden no coincidir. Con el planificador compartido, la Web solo pide la disponibilidad, que cambia poco, y calcula lo demás.

### D2. Selección por nivel en el servicio

`GenerateAsync` con reparto:

1. `400` si llegan a la vez `Difficulty` y `DifficultyPercentages`.
2. Pide la disponibilidad por nivel en las categorías (`QuestionRepository.CountByDifficultyAsync`).
3. Llama al planificador. Si hay faltantes, lanza `ValidationException` con un mensaje por nivel: «No hay suficientes preguntas de nivel avanzado: se necesitan 5 y hay 2.»
4. Llama a `GetRandomAsync(Requested[n], categoryIds, n)` para cada nivel con `Requested[n] > 0`.
5. Mezcla las preguntas elegidas con `Random.Shared.Shuffle` y numera `Order` de 1 a n.

Sin reparto, el camino actual no cambia: una sola llamada, con o sin nivel único, y rechazo si no alcanza.

Los pasos 2 y 4 son consultas separadas. Si alguien da de baja una pregunta entre las dos, un nivel puede devolver una menos. El servicio lo comprueba y lanza la misma `ValidationException`; es una carrera rara y el reintento la resuelve.

### D3. Contrato de la API

- `GenerateExamDto` gana un último parámetro opcional: `Dictionary<DifficultyLevel, int>? DifficultyPercentages = null`. Los llamadores actuales no cambian.
- La respuesta de `generate` no cambia: el reparto real se ve en `Questions`.
- Nuevo `LevelCountDto(DifficultyLevel Level, int Count)` para la disponibilidad.

### D4. Endpoint de disponibilidad

`GET /api/questions/availability?categoryIds=1&categoryIds=2` en `QuestionsController` (política `Gestion`), respuesta `List<LevelCountDto>` con los tres niveles siempre. Una consulta agregada: `GROUP BY Difficulty` sobre las activas de esas categorías, completada con ceros en memoria.

### D5. Web

`GenerateExam.razor`:

- El selector de dificultad gana la opción «Reparto por nivel». Al elegirla aparecen tres campos de porcentaje, con 34 / 33 / 33 como valor inicial.
- Al cargar y al cambiar las categorías, pide la disponibilidad. Al cambiar cualquier dato, recalcula con `DifficultyPlanner` en el navegador.
- Muestra por nivel: porcentaje, preguntas que saldrán y disponibles. Un nivel con faltante se marca con «necesita N, hay M».
- «Generar prueba» queda bloqueado si la suma no es 100 o si hay faltantes.
- Si el servidor rechaza, la pantalla muestra su mensaje. Para eso `GenerateExamAsync` pasa a devolver el código y el error, con el `SendAsync` que ya existe en `ApiService`. En los casos de hoy, el mensaje fijo actual se mantiene.

`ExamDetail.razor` añade una línea con el reparto real (número y porcentaje por nivel), calculado de `Questions`.

## Risks / Trade-offs

- [El planificador corre en el navegador y en el servidor: una diferencia de versión entre la Web y la API daría vistas previas distintas del resultado] → Se despliegan juntas. Aun así, el servidor manda: si la vista previa se equivoca, el servidor rechaza con su mensaje.
- [La disponibilidad de la vista previa puede quedarse vieja] → El servidor recalcula con la disponibilidad del momento y rechaza si ya no alcanza.
- [Con el banco actual, los repartos con muchas avanzadas se rechazarán a menudo en categorías pequeñas] → Es lo que decidió el usuario: rechazar antes que mezclar niveles. La vista previa lo dice antes de pulsar, con los números de cada nivel.
- [Con pocas preguntas, los porcentajes no se pueden cumplir: 5 preguntas al 33 / 33 / 34 dan 2 / 1 / 2] → Es inevitable. La vista previa muestra los números, no solo los porcentajes.

## Migration Plan

Sin cambios de esquema. Se despliegan la API y la Web juntas. Las peticiones sin reparto se comportan igual que antes.
