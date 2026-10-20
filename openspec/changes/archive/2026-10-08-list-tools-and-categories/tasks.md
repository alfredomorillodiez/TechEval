## 1. Servidor

- [x] 1.1 `CategoryDto.IsActive`, `UpdateCategoryDto.IsActive`, `includeInactive`, validación y duplicados en `CategoryService`, y el parámetro en `CategoriesController` (design.md D4). Verificar con pruebas: listado por defecto y con inactivas; nombre vacío, de 101 caracteres y descripción de 501; duplicado en el alta (con otras mayúsculas y espacios); renombrar a un nombre usado; guardar sin cambiar el nombre; reactivar; editar sin `IsActive` conserva el estado; el nombre se guarda recortado.
- [x] 1.2 `ListSort` y `SortState` en Aplicación (design.md D1). Verificar con pruebas: alternar dirección, cambiar de columna, clave desconocida, textos con tildes, nulos y vacíos al final en las dos direcciones, números, fechas y desempate estable.

## 2. Infraestructura de la Web

- [x] 2.1 `ListState` (Scoped) y su limpieza al cerrar y al abrir sesión en `AuthStateService` (design.md D3). Verificar que la Web compila y que `ListState` está registrado.
- [x] 2.2 `Shared/SortHeader.razor` (design.md D2). Verificar que compila.
- [x] 2.3 `ApiService.GetCategoriesAsync(includeInactive)`, y las llamadas de crear y editar categoría con código y mensaje. Verificar que compila y que no quedan usos del contrato anterior.

## 3. Pantalla de categorías

- [x] 3.1 `Pages/Admin/Categories/CategoryList.razor` y el enlace del menú (design.md D5). Verificar en el navegador: alta, nombre repetido (mensaje en el diálogo), edición, baja con confirmación, reactivar, buscador, orden y error de carga con la API parada.
- [x] 3.2 Desplegables de categorías activas (design.md D6): `QuestionForm`, `QuestionList`, `ExamNew`, `GenerateExam`. Verificar en el navegador: una categoría dada de baja no se ofrece en el alta; en la edición de una pregunta suya aparece como «(desactivada)».

## 4. Listados

- [x] 4.1 Preguntas, resultados y resultados de una prueba: orden, y estado conservado al volver (design.md D3, D7). Verificar en el navegador: ordenar, abrir un detalle, volver y ver el mismo filtro y orden; los resultados de dos pruebas no se mezclan.
- [x] 4.2 Usuarios, correcciones pendientes, cola e historial del evaluador: buscador, orden y estado conservado. Verificar en el navegador.
- [x] 4.3 Pruebas (tarjetas): buscador, selector de orden y estado conservado. Verificar en el navegador.
- [x] 4.4 Verificar que cerrar sesión y entrar con otra cuenta deja todos los listados sin filtros.

## 5. Cierre

- [x] 5.1 Documentar la pantalla de categorías y el parámetro `includeInactive` en `documentacion.md` y `README.md`. Verificar con una búsqueda.
- [x] 5.2 Ejecutar la batería completa y `openspec validate list-tools-and-categories --strict`. Verificar que pasan.
- [x] 5.3 Recorrido final en el navegador contra una base de prueba (nunca `TechEvalDb`), sin credenciales de correo.
