## ADDED Requirements

### Requirement: Reparto por nivel en la pantalla de generación
La pantalla `/admin/pruebas/generate` SHALL ofrecer tres modos de dificultad: «Todos los niveles», un nivel único y «Reparto por nivel». En el modo de reparto, SHALL mostrar un campo de porcentaje para cada nivel y, al lado, cuántas preguntas saldrán de él y cuántas hay disponibles en las categorías elegidas. La vista previa SHALL actualizarse al cambiar el número de preguntas, las categorías o los porcentajes, y SHALL calcularse con la misma regla que el servidor.

Mientras los porcentajes no sumen 100, la pantalla SHALL indicar cuánto falta o sobra y SHALL mantener bloqueado «Generar prueba». Si algún nivel necesita más preguntas de las disponibles, la pantalla SHALL marcar ese nivel, decir cuántas necesita y cuántas hay, y SHALL mantener bloqueado «Generar prueba». Si el servidor rechaza la generación, la pantalla SHALL mostrar su mensaje.

#### Scenario: Vista previa sin problemas
- **GIVEN** un administrador en el modo de reparto, con 10 preguntas y el reparto 30 / 50 / 20, y disponibilidad suficiente
- **WHEN** consulta la vista previa
- **THEN** la pantalla muestra 3 / 5 / 2 preguntas, ningún aviso, y «Generar prueba» habilitado

#### Scenario: Porcentajes que no suman 100
- **WHEN** el administrador escribe el reparto 30 / 30 / 30
- **THEN** la pantalla indica que falta un 10 % y «Generar prueba» está bloqueado

#### Scenario: Un nivel no alcanza
- **GIVEN** unas categorías con solo 2 preguntas avanzadas
- **WHEN** el administrador pide 10 preguntas con el reparto 20 / 30 / 50
- **THEN** la pantalla marca el nivel avanzado con «necesita 5, hay 2» y «Generar prueba» está bloqueado

### Requirement: Reparto real en el detalle de la prueba
El detalle de una prueba (`/admin/pruebas/{id}`) SHALL mostrar cuántas preguntas tiene de cada nivel y qué porcentaje del total representa cada uno.

#### Scenario: Detalle de una prueba generada con reparto
- **GIVEN** una prueba con 3 preguntas básicas, 5 intermedias y 2 avanzadas
- **WHEN** el administrador abre su detalle
- **THEN** la pantalla muestra 3 (30 %) básicas, 5 (50 %) intermedias y 2 (20 %) avanzadas
