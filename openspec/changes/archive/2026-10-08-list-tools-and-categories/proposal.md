## Why

Dos carencias del análisis del 08·10·2026 que hacen la aplicación incómoda de usar a diario.

- **Los listados no se pueden buscar ni ordenar, y los filtros se pierden.** Las bandejas de correcciones, el historial del evaluador y el listado de pruebas no tienen buscador. Ningún listado ordena por columna. En los listados que sí filtran (preguntas, resultados, usuarios), abrir un elemento y volver deja los filtros en blanco: quien revisa 40 preguntas de una categoría tiene que volver a filtrar cada vez.
- **No hay pantalla de categorías.** Para dar de alta una tecnología (por ejemplo, «Kubernetes») hace falta SQL o llamar a la API a mano, aunque `POST /api/categories` existe. Además:
  - `CategoryDto` no dice si la categoría está activa, y `GET /api/categories` devuelve también las desactivadas. Los desplegables de los formularios ofrecen categorías dadas de baja.
  - Crear una categoría con un nombre repetido da `500`: el índice único rechaza la fila y nadie la traduce.
  - Nada valida el nombre ni la descripción: un nombre en blanco o de 300 caracteres llega a la base de datos.
  - No hay forma de reactivar una categoría desactivada.

## What Changes

- **Pantalla de categorías** (`/admin/categorias`, en el menú del administrador): lista con nombre, descripción, número de preguntas activas y estado; buscador; orden por columna; alta y edición en un diálogo; dar de baja con confirmación; reactivar.
- **API de categorías**:
  - `CategoryDto` gana `IsActive`. `UpdateCategoryDto` gana `IsActive` opcional (si falta, no cambia), que permite reactivar.
  - `GET /api/categories` devuelve solo las activas. Con `?includeInactive=true` devuelve todas, y es lo que usa la pantalla de categorías y el filtro del listado de preguntas.
  - Alta y edición validan: nombre no vacío y de hasta 100 caracteres; descripción de hasta 500. Un nombre ya usado (sin distinguir mayúsculas) responde `409` con un mensaje.
- **Los formularios solo ofrecen categorías activas.** El de preguntas, el de generación y el de alta manual de pruebas. En la edición de una pregunta cuya categoría está desactivada, esa categoría aparece marcada como desactivada y el resto de las desactivadas no se pueden elegir.
- **Buscador y orden por columna** en: preguntas, resultados, resultados de una prueba, usuarios, correcciones pendientes del administrador, cola e historial del evaluador, categorías y el listado de pruebas (que son tarjetas: buscador y selector de orden). El orden cambia al pulsar la cabecera de la columna, con una flecha que lo indica y `aria-sort` para quien usa lector de pantalla.
- **Los filtros se conservan al volver.** Los listados con filtros o búsqueda guardan su estado (búsqueda, filtros y orden) mientras dure la sesión del navegador, y lo restauran al volver a la pantalla. Cerrar la sesión lo borra. No viaja al servidor ni se guarda en disco.

Fuera de alcance:

- Paginación en servidor. Los listados siguen cargando todo y filtrando en el cliente.
- Guardar los filtros tras recargar la página o entre sesiones.
- Borrar categorías de verdad (siguen dándose de baja).
- Mover preguntas entre categorías en bloque.

## Capabilities

### New Capabilities

- `category-management`: gestión de categorías por la API y la interfaz.

### Modified Capabilities

- `admin-console`: buscador, orden por columna y filtros que se conservan en los listados.

## Impact

- **Aplicación**: `CategoryService` (validación, duplicados, `IsActive`, `includeInactive`); `CategoryDto` y `UpdateCategoryDto`.
- **API**: `CategoriesController` acepta `includeInactive`. Respuestas `400` y `409` nuevas.
- **Web**: pantalla nueva `CategoryList`; componentes y servicios nuevos (`SortHeader`, `SortState`, `ListState`); cambios en las pantallas de listado y en los formularios que cargan categorías; enlace en el menú.
- **Pruebas**: servicio de categorías (validación, duplicados, reactivar, filtrar activas) y la lógica pura de orden.
- **Base de datos**: sin cambios.
