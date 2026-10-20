## Context

Ver `proposal.md` — Why. Los requisitos están en `specs/`.

Lo que condiciona el enfoque:

- Todos los listados cargan la lista entera y filtran en el cliente (`ResultList`, `QuestionList` salvo sus tres filtros de servidor, `UserList` con su búsqueda de servidor, y los demás). No hay paginación, y no se añade aquí.
- Los filtros y la búsqueda son campos privados de cada página (`_fSearch`, `_catFilter`…). Una página se destruye al navegar, y con ella su estado. Un servicio de ámbito `Scoped` en Blazor WebAssembly vive lo que dura la aplicación en el navegador (una recarga lo reinicia), así que sirve como almacén de sesión sin tocar el disco.
- `AuthStateService` ya conoce el cierre de sesión (`LogoutAsync`) y es `Scoped`, igual que `ApiService`.
- `CategoryService.GetAllAsync` devuelve todas las categorías sin `IsActive`; `CreateAsync` y `UpdateAsync` no validan nada; el nombre es único por índice (`IX_Categories_Name`), así que un duplicado llega a EF como `DbUpdateException` y sale `500`. `DeleteAsync` desactiva.
- Los desplegables de `QuestionForm`, `QuestionList`, `ExamNew` y `GenerateExam` usan `GetCategoriesAsync()`, que hoy trae todas.
- El orden de textos en el navegador depende de la cultura. Blazor WebAssembly lleva los datos ICU (`icudt_*.dat` aparece en la publicación), así que `es-ES` está disponible.
- La Web no tiene proyecto de pruebas. La lógica pura (orden) se pone en Aplicación para poder probarla.

## Goals / Non-Goals

**Goals:**

- Un único mecanismo de orden y de estado, usado por todas las pantallas, y no uno por pantalla.
- Que el estado de un listado no viaje fuera del navegador ni sobreviva al cierre de sesión.
- Que ninguna categoría desactivada se ofrezca para un alta.

**Non-Goals:**

- Paginación, o filtros en servidor.
- Guardar el estado tras recargar o entre sesiones.

## Decisions

### D1. `ListSort` en Aplicación, y `SortState` como su estado

`Application/ListSort.cs`:

```csharp
public sealed class SortState { public string? Key; public bool Descending; ... Toggle(key) ... }
public static class ListSort {
    public static IEnumerable<T> Apply<T>(IEnumerable<T> source, SortState state,
        IReadOnlyDictionary<string, Func<T, object?>> columns);
}
```

- `Toggle(key)`: si es la misma columna, invierte; si es otra, la pone ascendente.
- `Apply`: sin clave o con una clave desconocida, devuelve la fuente tal cual. Con clave, ordena por el valor de la columna. Los `null` (y las cadenas vacías) van **siempre al final**, en cualquier dirección. Textos con `es-ES`, `CompareOptions.IgnoreCase`; resto de tipos por `IComparable`. El desempate es estable: `OrderBy`/`OrderByDescending` de LINQ son estables, así que la posición original decide los empates.
- Los valores vacíos al final en ambas direcciones se consigue ordenando en dos claves: primero «¿es vacío?», después el valor.
- Si la cultura `es-ES` no existe, usa la invariante.

La Web usa el componente `SortHeader` para pintar la cabecera.

### D2. `SortHeader` y la accesibilidad

`Shared/SortHeader.razor` pinta un `<th scope="col" aria-sort="…">` con un `<button>` dentro (el botón es lo que recibe el teclado) y una flecha (`bi-caret-up-fill` o `bi-caret-down-fill`) solo en la columna activa. `aria-sort` es `ascending`, `descending` o `none`. Recibe `Key`, `State`, `OnSort` y un `Class` para conservar las clases actuales de cada `<th>` (`ps-3`, `text-end`…).

### D3. `ListState`: el estado de cada listado

`Services/ListState.cs`, registrado `Scoped`: un diccionario `string → object`. Cada pantalla define un `record` privado con su estado (búsqueda, filtros, orden), lo restaura al empezar y lo guarda al destruirse (`IDisposable`).

- `Get<T>(key)` devuelve el estado guardado o `null`; `Set(key, value)` lo guarda.
- La clave es el nombre de la pantalla, y para los resultados de una prueba lleva el identificador: `results-exam-7`.
- `AuthStateService.LogoutAsync` (y el inicio de sesión) llama a `Clear()`. Otra persona que entre en el mismo navegador no hereda nada.
- Un estado restaurado se aplica **antes** de la primera carga, para que los filtros de servidor de `QuestionList` (categoría, nivel y tipo) y la búsqueda de `UserList` salgan en la primera petición.

Alternativas descartadas:

- **`sessionStorage` del navegador.** Sobreviviría a una recarga, pero los textos de búsqueda son nombres y correos de candidatos, y dejarlos en el almacén del navegador es guardar datos personales sin necesidad. La memoria de la aplicación cubre el caso real: abrir el detalle y volver.
- **Parámetros en la URL.** Se pueden compartir y sobreviven a una recarga, pero obligan a reescribir la navegación de cada pantalla y a codificar cada filtro. Es una buena idea para otro cambio.

### D4. Categorías: contrato

- `CategoryDto(int Id, string Name, string Description, int QuestionCount, bool IsActive = true)`.
- `UpdateCategoryDto(string Name, string Description, bool? IsActive = null)`.
- `ICategoryService.GetAllAsync(bool includeInactive = false, CancellationToken ct = default)`. `GET /api/categories?includeInactive=true`.
- `CreateAsync` y `UpdateAsync` validan (`ValidationException`) y comprueban el duplicado (`ConflictException`) antes de escribir. El nombre se compara recortado y sin distinguir mayúsculas, con `FindAsync(c => c.Name.ToLower() == normalizado && c.Id != id)`.
- El nombre se guarda recortado.
- `UpdateAsync` aplica `IsActive` solo si viene.

La carrera entre dos altas simultáneas con el mismo nombre sigue pudiendo llegar al índice único. No se trata: lo hace un administrador a mano, y el índice sigue garantizando la unicidad.

### D5. Pantalla de categorías

`Pages/Admin/Categories/CategoryList.razor` en `/admin/categorias`, con el patrón de las demás: `LoadError`, `SortHeader` y `ListState`. Un diálogo (el mismo estilo modal que usa `UserList`) sirve para alta y edición; el error del servidor se muestra dentro del diálogo y no lo cierra. «Dar de baja» pide confirmación en línea (el patrón de «Liberar» de la cola). El enlace del menú va entre «Preguntas» y «Pruebas».

### D6. Formularios con categorías activas

`GetCategoriesAsync(bool includeInactive = false)`. `ExamNew` y `GenerateExam` usan el valor por defecto. `QuestionList` pide todas (el filtro debe poder consultar las desactivadas). `QuestionForm` pide todas y pinta cada `<option>` desactivada con «(desactivada)» y `disabled`, salvo la de la pregunta que se edita. Con la API del servidor ya filtrando por defecto, los formularios nuevos no cambian de código salvo `QuestionForm` y `QuestionList`.

### D7. Dónde se aplica

| Pantalla | Búsqueda | Columnas ordenables |
|---|---|---|
| Preguntas | enunciado (ya existe) | enunciado, categoría, nivel, tipo, puntos, estado |
| Resultados | candidato o correo (ya existe) | candidato, prueba, nota, estado, fecha |
| Resultados de una prueba | candidato o correo (ya existe) | candidato, nota, estado, fecha |
| Usuarios | nombre o correo (de servidor, ya existe) | nombre, correo, rol, estado |
| Categorías | nombre o descripción | nombre, preguntas, estado |
| Correcciones pendientes | candidato, correo o prueba | candidato, prueba, enviada, esperando, por corregir |
| Cola del evaluador | prueba o alias | alias, prueba, enviada, esperando, por corregir |
| Historial del evaluador | prueba o alias | alias, prueba, corregida, nota |
| Pruebas (tarjetas) | título | selector: título, fecha, preguntas, resultados |

Cada una con su `record` de estado en `ListState`.

## Risks / Trade-offs

- [Los listados siguen cargando todo] → Con cientos de filas va bien; miles pedirían paginación en servidor. Se deja anotado como fuera de alcance.
- [El estado restaurado oculta datos y la persona no sabe por qué faltan filas] → Todos los listados con filtros ya muestran «Mostrando N de M» o un botón «Limpiar filtros»; los que no lo tengan lo ganan.
- [`GET /api/categories` cambia su valor por defecto y algún cliente externo esperaba todas] → El único cliente es la Web de este repositorio. Quien quiera todas, pasa `includeInactive=true`.
- [Orden de textos con tildes depende de ICU] → Si falta `es-ES`, cae a la cultura invariante, que ordena bien salvo matices de la ñ.
- [Reordenar al pulsar cabecera con muchos datos] → Es LINQ sobre una lista en memoria; no es perceptible con los volúmenes actuales.

## Migration Plan

Sin cambios de esquema. Desplegar la API y la Web juntas: una Web anterior con la API nueva ya no vería las categorías desactivadas en sus desplegables, que es lo que se quiere. Para volver atrás, revertir ambas.
