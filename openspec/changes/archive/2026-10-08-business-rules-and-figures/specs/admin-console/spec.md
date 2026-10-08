## ADDED Requirements

### Requirement: Los resultados de una prueba cuentan bien a los reprobados
La pantalla de resultados de una prueba SHALL contar como «Reprobados» solo los resultados corregidos con `Passed = false`. Los pendientes de corrección SHALL NOT contarse como reprobados. Los porcentajes de aprobados y reprobados SHALL calcularse sobre los corregidos, y si no hay ninguno corregido, la pantalla SHALL NOT mostrar un porcentaje. El número de pendientes SHALL mostrarse aparte.

#### Scenario: Una prueba con pendientes
- **GIVEN** una prueba con 5 resultados: 2 aprobados, 1 reprobado y 2 pendientes de corrección
- **WHEN** se abre la pantalla de resultados de esa prueba
- **THEN** «Aprobados» muestra 2, «Reprobados» muestra 1 y «Pendientes» muestra 2
- **AND** los porcentajes son 67 % y 33 %, sobre los 3 corregidos

#### Scenario: Ningún resultado corregido
- **GIVEN** una prueba con 2 resultados, los dos pendientes
- **WHEN** se abre la pantalla
- **THEN** «Reprobados» muestra 0, «Pendientes» muestra 2 y no se muestra ningún porcentaje

### Requirement: El botón de resultados de una prueba abre sus resultados
En el listado de pruebas, el botón de resultados de cada prueba SHALL abrir la pantalla de resultados de esa prueba (`/admin/results/prueba/{id}`), no el listado global.

#### Scenario: Abrir los resultados de una prueba
- **WHEN** un administrador pulsa el botón de resultados de la prueba 7
- **THEN** la aplicación abre `/admin/results/prueba/7` y muestra solo los resultados de esa prueba

### Requirement: La importación de candidatos desde CSV dice qué importó y qué no
La importación de candidatos del envío masivo SHALL aceptar `;`, tabulador o `,` como separador, ignorar las líneas vacías y una primera línea de encabezado, y validar el correo de cada línea. El resultado SHALL indicar cuántos candidatos se importaron y, de cada línea ignorada, su número y el motivo. Cuando no se importe ninguno, SHALL decir por qué y SHALL NOT limitarse a dejar la lista vacía.

#### Scenario: CSV separado por punto y coma
- **GIVEN** un CSV de Excel con líneas «Juan García;juan@empresa.com»
- **WHEN** el administrador pulsa Importar
- **THEN** se importan los candidatos y la pantalla dice cuántos

#### Scenario: CSV con encabezado
- **GIVEN** un CSV cuya primera línea es «Nombre;Email»
- **WHEN** se importa
- **THEN** la línea de encabezado se ignora sin contarla como error

#### Scenario: Línea con un correo inválido
- **GIVEN** un CSV con la línea 3 «Ana López;ana-empresa»
- **WHEN** se importa
- **THEN** los candidatos válidos se importan
- **AND** la pantalla dice que la línea 3 se ignoró porque el correo no es válido

#### Scenario: Ninguna línea válida
- **GIVEN** un texto en el que ninguna línea tiene nombre y correo
- **WHEN** se importa
- **THEN** la pantalla dice que no se importó ningún candidato y explica el formato esperado

#### Scenario: Nombre con coma entre comillas
- **GIVEN** la línea `"García, Juan",juan@empresa.com`
- **WHEN** se importa
- **THEN** el candidato se importa con el nombre «García, Juan»

### Requirement: El motivo de un envío rechazado llega a la pantalla
Cuando el servidor rechaza una invitación, individual o masiva, la pantalla de envío SHALL mostrar el mensaje del servidor, y SHALL NOT mostrar un texto genérico sobre la configuración del correo que oculte la causa.

#### Scenario: Invitación a una prueba desactivada
- **GIVEN** una prueba desactivada
- **WHEN** el administrador intenta enviarla y el servidor responde `409` con un mensaje
- **THEN** la pantalla muestra ese mensaje
