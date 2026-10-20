## ADDED Requirements

### Requirement: Los listados de administración muestran una página
Los listados de resultados, resultados de una prueba, preguntas, pruebas y usuarios SHALL mostrar una sola página y un control de paginación. El control SHALL indicar el rango mostrado y el total («Mostrando 26–50 de 312»), permitir ir a la página anterior, a la siguiente, a la primera y a la última, y elegir el tamaño de página entre 10, 25, 50 y 100. El control SHALL ser accesible: botones reales, la página actual con `aria-current="page"` y los botones sin efecto con `disabled`. Con una sola página, SHALL mostrar solo el total.

#### Scenario: Pasar de página
- **GIVEN** el listado de resultados con 60 filas y 25 por página
- **WHEN** el administrador pulsa «Siguiente»
- **THEN** se muestran las filas 26 a 50 y el control dice «Mostrando 26–50 de 60»

#### Scenario: Cambiar el tamaño de página
- **GIVEN** un listado en la página 3 con 25 por página
- **WHEN** el administrador elige 50 por página
- **THEN** el listado vuelve a la página 1

#### Scenario: Una sola página
- **GIVEN** un listado con 12 filas
- **WHEN** se muestra
- **THEN** no hay botones de página y el control dice «12 resultados»

### Requirement: Buscar, filtrar u ordenar vuelve a la primera página
Cambiar el texto de búsqueda, un filtro o el orden de un listado paginado SHALL volver a la página 1 y pedir los datos al servidor. La búsqueda SHALL esperar 300 ms desde la última pulsación antes de pedir. Una respuesta que llega tarde SHALL NOT sustituir a una más reciente.

#### Scenario: Buscar desde la página 3
- **GIVEN** el listado de resultados en la página 3
- **WHEN** el administrador escribe un texto en el buscador
- **THEN** el listado pide la página 1 con ese texto

#### Scenario: Escribir deprisa
- **GIVEN** el buscador de usuarios
- **WHEN** el administrador escribe cinco letras en medio segundo
- **THEN** la pantalla hace una sola petición

#### Scenario: Respuesta tardía
- **GIVEN** una búsqueda lenta que se pidió antes que otra rápida
- **WHEN** llega primero la rápida y después la lenta
- **THEN** la pantalla conserva el resultado de la rápida

### Requirement: La página se conserva al volver
El estado guardado de un listado (búsqueda, filtros y orden) SHALL incluir también la página y el tamaño de página. Al abrir una ficha y volver, SHALL mostrarse la misma página. Si la página guardada ya no existe porque hay menos filas, SHALL mostrarse la última.

#### Scenario: Volver desde una ficha
- **GIVEN** el administrador en la página 3 de los resultados
- **WHEN** abre una ficha y vuelve
- **THEN** el listado muestra la página 3

#### Scenario: La página guardada ya no existe
- **GIVEN** una página guardada que es la 5 y un listado que ahora tiene 3 páginas
- **WHEN** el administrador vuelve
- **THEN** el listado muestra la página 3

### Requirement: Los filtros de resultados se aplican en servidor
Las pantallas de resultados y de resultados de una prueba SHALL pedir al servidor la búsqueda, el filtro de resultado, el rango de nota, el rango de fechas y el orden. Las cifras de arriba SHALL venir de `GET /api/results/stats` y SHALL NOT cambiar con los filtros. El filtro por prueba SHALL usar el identificador de la prueba, con las opciones de `GET /api/exams/options`. Las fechas SHALL convertirse del día local del administrador a un instante UTC antes de pedirse.

#### Scenario: Las cifras no dependen del filtro
- **GIVEN** las cifras de la pantalla de resultados
- **WHEN** el administrador filtra por «Reprobados»
- **THEN** las tarjetas de arriba no cambian

#### Scenario: Filtrar por una prueba
- **GIVEN** el desplegable de pruebas de la pantalla de resultados
- **WHEN** el administrador elige una prueba
- **THEN** el listado pide `examId` y muestra solo sus resultados

### Requirement: El listado de preguntas filtra una sola vez
El listado de preguntas SHALL ofrecer un único conjunto de filtros: categoría, nivel y tipo arriba; búsqueda de texto y estado en la fila de la tabla. Todos SHALL aplicarse en servidor.

#### Scenario: Filtro de categoría
- **GIVEN** el listado de preguntas
- **WHEN** el administrador elige una categoría arriba
- **THEN** el listado pide `categoryId` y vuelve a la página 1

#### Scenario: Estado por defecto
- **GIVEN** un administrador que abre el listado de preguntas por primera vez
- **WHEN** se muestra
- **THEN** el filtro «Estado» está en «Activas» y solo se ven las activas

#### Scenario: Ver las desactivadas
- **GIVEN** una pregunta dada de baja
- **WHEN** el administrador elige «Inactivas» en «Estado»
- **THEN** el listado la muestra

### Requirement: Las pantallas que necesitan la lista entera la recorren por páginas
La composición manual de pruebas y la lista de evaluadores del detalle de una prueba SHALL obtener sus datos pidiendo páginas de 100 hasta agotar el total. Si una página falla, SHALL mostrarse el error de carga, no una lista incompleta.

#### Scenario: Más de 100 preguntas en la composición
- **GIVEN** un banco con 250 preguntas
- **WHEN** el administrador abre la composición manual de una prueba
- **THEN** la pantalla ofrece las 250 preguntas

#### Scenario: Falla la segunda página
- **GIVEN** un banco con 250 preguntas
- **WHEN** la segunda petición falla
- **THEN** la pantalla muestra el error de carga con «Reintentar» y no una lista de 100
