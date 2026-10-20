## ADDED Requirements

### Requirement: Buscador y orden por columna en los listados
Los listados de la interfaz SHALL ofrecer un buscador de texto y ordenar por columna. Esto aplica a: preguntas, resultados, resultados de una prueba, usuarios, categorías, correcciones pendientes del administrador, cola e historial del evaluador y pruebas. Pulsar la cabecera de una columna SHALL ordenar por ella de forma ascendente; pulsarla de nuevo SHALL invertir el orden. La cabecera ordenada SHALL indicar la dirección con una flecha y con `aria-sort`. El orden de los textos SHALL seguir las reglas del castellano, sin distinguir mayúsculas, y los valores vacíos SHALL quedar al final. El listado de pruebas, que son tarjetas, SHALL ofrecer en su lugar un selector de orden.

#### Scenario: Ordenar por una columna
- **GIVEN** el listado de resultados
- **WHEN** el administrador pulsa la cabecera «Nota»
- **THEN** las filas se ordenan por nota de menor a mayor, y la cabecera muestra una flecha ascendente y `aria-sort="ascending"`

#### Scenario: Invertir el orden
- **GIVEN** un listado ordenado por «Nota» ascendente
- **WHEN** el administrador pulsa otra vez «Nota»
- **THEN** el orden pasa a descendente y `aria-sort="descending"`

#### Scenario: Cambiar de columna
- **GIVEN** un listado ordenado por «Nota» descendente
- **WHEN** el administrador pulsa «Candidato»
- **THEN** el orden pasa a «Candidato» ascendente

#### Scenario: Orden de textos con tildes
- **GIVEN** candidatos llamados «Álvaro», «Zoe» y «Andrés»
- **WHEN** se ordena por nombre ascendente
- **THEN** el orden es «Álvaro», «Andrés», «Zoe»

#### Scenario: Los valores vacíos van al final
- **GIVEN** resultados pendientes, que no tienen nota definitiva
- **WHEN** se ordena por nota en cualquier dirección
- **THEN** los pendientes quedan al final de la lista

#### Scenario: Buscar en la cola de correcciones
- **GIVEN** una cola con pruebas de «SQL» y de «C#»
- **WHEN** el evaluador escribe «sql» en el buscador
- **THEN** la cola muestra solo las pruebas de «SQL»

### Requirement: Los filtros de un listado se conservan al volver
Los listados con búsqueda, filtros u orden SHALL conservar ese estado mientras dure la sesión del navegador y restaurarlo cuando la persona vuelva a la pantalla, por ejemplo tras abrir el detalle de un elemento. El estado SHALL guardarse solo en el cliente, SHALL NOT guardarse en disco, y SHALL borrarse al cerrar la sesión. El estado de una pantalla SHALL ser independiente del de las demás, y el de los resultados de cada prueba SHALL ser independiente del de las otras pruebas.

#### Scenario: Volver desde el detalle
- **GIVEN** el listado de preguntas filtrado por la categoría «SQL» y con la búsqueda «join»
- **WHEN** el administrador abre una pregunta y vuelve al listado
- **THEN** el listado muestra el mismo filtro, la misma búsqueda y el mismo orden

#### Scenario: Cerrar la sesión
- **GIVEN** un listado con filtros
- **WHEN** la persona cierra la sesión y otra inicia sesión en el mismo navegador
- **THEN** los listados empiezan sin filtros

#### Scenario: Listados independientes
- **GIVEN** el listado de usuarios filtrado por el rol Evaluador
- **WHEN** el administrador abre el listado de resultados
- **THEN** el listado de resultados no hereda ese filtro

#### Scenario: Resultados de dos pruebas distintas
- **GIVEN** los resultados de la prueba 3 filtrados por «Reprobado»
- **WHEN** el administrador abre los resultados de la prueba 4
- **THEN** los resultados de la prueba 4 empiezan sin filtros
