# list-pagination Specification

## Purpose
Que el coste de abrir un listado dependa del tamaño de la página y no del de la tabla. Fija el contrato de paginación, búsqueda, filtro y orden en servidor de los listados de resultados, preguntas, pruebas y usuarios, y las cifras de resultados que antes calculaba el cliente.

## Requirements

### Requirement: Contrato de paginación de los listados
Los listados de resultados, preguntas, pruebas y usuarios SHALL aceptar los parámetros de consulta `page`, `pageSize`, `q`, `sort` y `desc`, y SHALL responder `{ items, total, page, pageSize }`, donde `total` es el número de filas que cumplen el filtro y `items` solo las de la página pedida. `page` empieza en 1 y vale 1 por defecto. `pageSize` vale 25 por defecto y 100 como máximo. Un `page` menor que 1 SHALL tratarse como 1. Un `pageSize` menor que 1 SHALL tratarse como el valor por defecto, y uno mayor que el máximo SHALL tratarse como el máximo. Un valor fuera de rango SHALL NOT dar un error. Un valor que no es un número SHALL rechazarse con `400`. Una `sort` desconocida SHALL ignorarse y aplicar el orden por defecto del listado.

#### Scenario: Primera página por defecto
- **GIVEN** 60 resultados
- **WHEN** se pide `GET /api/results` sin parámetros
- **THEN** la respuesta trae 25 elementos, `total` = 60, `page` = 1 y `pageSize` = 25

#### Scenario: Última página incompleta
- **GIVEN** 60 resultados y `pageSize` = 25
- **WHEN** se pide `page` = 3
- **THEN** la respuesta trae 10 elementos y `total` = 60

#### Scenario: Una página más allá del final
- **GIVEN** 60 resultados y `pageSize` = 25
- **WHEN** se pide `page` = 9
- **THEN** la respuesta trae 0 elementos, `total` = 60 y `page` = 9

#### Scenario: Tamaño de página excesivo
- **GIVEN** cualquier listado
- **WHEN** se pide `pageSize` = 5000
- **THEN** la respuesta usa `pageSize` = 100

#### Scenario: Valores fuera de rango
- **GIVEN** cualquier listado
- **WHEN** se pide `page` = 0 y `pageSize` = -3
- **THEN** la respuesta usa `page` = 1 y `pageSize` = 25, sin error

### Requirement: Orden estable entre páginas
Cada listado SHALL ordenar por la columna pedida y desempatar siempre por el identificador, de modo que dos peticiones seguidas de páginas distintas no repitan ni omitan filas. Sin `sort`, cada listado SHALL usar su orden natural: resultados por fecha de envío descendente, preguntas por categoría y nivel, pruebas por fecha de creación descendente y usuarios por rol y nombre. Los valores vacíos SHALL quedar al final en cualquier dirección. Los textos SHALL ordenarse sin distinguir mayúsculas.

#### Scenario: Empates en la columna de orden
- **GIVEN** 30 resultados con la misma nota
- **WHEN** se piden las páginas 1 y 2 ordenadas por nota
- **THEN** ninguna fila aparece en las dos páginas y no falta ninguna

#### Scenario: Pendientes al ordenar por nota
- **GIVEN** resultados corregidos y resultados pendientes de corrección
- **WHEN** se ordena por nota, ascendente o descendente
- **THEN** los pendientes quedan al final

### Requirement: Resultados paginados con filtros en servidor
`GET /api/results` y `GET /api/results/exam/{examId}` SHALL aceptar, además del contrato común, los filtros `examId` (solo en la global), `outcome` (`pending`, `passed` o `failed`), `scoreMin`, `scoreMax`, `from` y `to`. `q` SHALL buscar en el nombre y en el correo del candidato, sin distinguir mayúsculas. `from` SHALL ser un instante UTC inclusivo y `to` uno exclusivo. `scoreMin` y `scoreMax` SHALL excluir los resultados pendientes, que no tienen nota definitiva. Las columnas de orden SHALL ser `candidate`, `exam`, `score`, `outcome` y `date`.

#### Scenario: Buscar por correo
- **GIVEN** resultados de `ana@empresa.com` y de `luis@empresa.com`
- **WHEN** se pide `q=ANA@`
- **THEN** la respuesta solo trae los de Ana y `total` cuenta solo esos

#### Scenario: Filtrar por reprobados
- **GIVEN** resultados aprobados, reprobados y pendientes
- **WHEN** se pide `outcome=failed`
- **THEN** la respuesta solo trae resultados corregidos con `Passed = false`

#### Scenario: Rango de nota sin pendientes
- **GIVEN** un resultado pendiente con puntuación parcial del 80 %
- **WHEN** se pide `scoreMin=70`
- **THEN** ese resultado no aparece

#### Scenario: Filtrar por día
- **GIVEN** un resultado enviado el `2026-10-08T22:30Z`
- **WHEN** se pide `from=2026-10-08T22:00Z` y `to=2026-10-09T22:00Z`
- **THEN** el resultado aparece

#### Scenario: Resultados de una prueba
- **GIVEN** resultados de dos pruebas
- **WHEN** se pide `GET /api/results/exam/7`
- **THEN** solo aparecen los de la prueba 7

### Requirement: Cifras de resultados calculadas en servidor
`GET /api/results/stats` SHALL devolver, sobre **todos** los resultados y sin aplicar filtros, el total, los corregidos, los pendientes, los aprobados, el porcentaje de aprobados, la nota media, y la distribución en los tramos 0–49, 50–59, 60–69, 70–79, 80–89 y 90–100. Con `examId`, SHALL calcularlo solo sobre esa prueba y SHALL añadir su título, la nota máxima y la nota mínima. Las notas y la distribución SHALL calcularse solo sobre los corregidos. Si la prueba no existe, SHALL responder `404`.

#### Scenario: Cifras globales
- **GIVEN** 10 resultados corregidos (6 aprobados) y 4 pendientes
- **WHEN** se pide `GET /api/results/stats`
- **THEN** `total` = 14, `reviewed` = 10, `pending` = 4, `passed` = 6 y `passRate` = 60

#### Scenario: Los pendientes no entran en la nota media
- **GIVEN** un resultado corregido con 80 % y uno pendiente con 10 % de puntuación parcial
- **WHEN** se piden las cifras
- **THEN** la nota media es 80

#### Scenario: Sin resultados corregidos
- **GIVEN** una prueba cuyos resultados están todos pendientes
- **WHEN** se piden sus cifras
- **THEN** la nota media, la máxima y la mínima valen 0, y `passRate` vale 0

#### Scenario: Prueba inexistente
- **GIVEN** que no existe la prueba 999
- **WHEN** se pide `GET /api/results/stats?examId=999`
- **THEN** la respuesta es `404`

### Requirement: Preguntas paginadas con filtros en servidor
`GET /api/questions` SHALL aceptar, además del contrato común, `categoryId`, `difficulty`, `type` y `active`. `q` SHALL buscar en el enunciado. Las columnas de orden SHALL ser `text`, `category`, `difficulty`, `type`, `points` y `status`. `active` SHALL ser opcional: sin él, la respuesta SHALL incluir las preguntas activas y las desactivadas. La respuesta SHALL NOT cargar las opciones de cada pregunta.

#### Scenario: Combinar filtros
- **GIVEN** preguntas de varias categorías, niveles y tipos
- **WHEN** se piden `categoryId=2`, `difficulty=Advanced` y `q=join`
- **THEN** solo aparecen las que cumplen las tres condiciones y `total` las cuenta

#### Scenario: Solo las activas
- **GIVEN** preguntas activas e inactivas
- **WHEN** se pide `active=true`
- **THEN** no aparece ninguna inactiva

#### Scenario: Solo las inactivas
- **GIVEN** preguntas activas e inactivas
- **WHEN** se pide `active=false`
- **THEN** solo aparecen las inactivas

#### Scenario: Sin filtro de estado
- **GIVEN** preguntas activas e inactivas
- **WHEN** se pide sin `active`
- **THEN** aparecen las dos clases

### Requirement: Pruebas paginadas con los recuentos calculados en SQL
`GET /api/exams` SHALL aceptar, además del contrato común, `active`. `q` SHALL buscar en el título. Las columnas de orden SHALL ser `title`, `created`, `questions`, `time`, `invitations` y `results`. El número de preguntas, los puntos, las invitaciones, los resultados y las sesiones abandonadas SHALL calcularse en la consulta, sin cargar las preguntas, invitaciones, sesiones ni resultados de cada prueba. `GET /api/exams/options` SHALL devolver el identificador y el título de todas las pruebas, ordenadas por título.

#### Scenario: Los recuentos no cambian
- **GIVEN** una prueba con 4 preguntas de 5 puntos, 3 invitaciones, 2 resultados y 1 sesión abandonada
- **WHEN** se lista
- **THEN** la prueba muestra 4 preguntas, 20 puntos, 3 invitaciones, 2 resultados y 1 abandonada

#### Scenario: Opciones de pruebas
- **GIVEN** 40 pruebas
- **WHEN** se pide `GET /api/exams/options`
- **THEN** la respuesta trae las 40 con solo identificador y título

### Requirement: Usuarios paginados
`GET /api/users` SHALL aceptar, además del contrato común, `role` y `active`. `q` SHALL buscar en el nombre y en el correo. Las columnas de orden SHALL ser `name`, `email`, `role`, `status` y `created`.

#### Scenario: Filtrar evaluadores activos
- **GIVEN** administradores, evaluadores activos, evaluadores desactivados y alumnos
- **WHEN** se piden `role=Evaluador` y `active=true`
- **THEN** solo aparecen los evaluadores activos
