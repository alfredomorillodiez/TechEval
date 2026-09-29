## ADDED Requirements

### Requirement: Reparto por nivel de dificultad en la generación automática
El sistema SHALL aceptar en `POST /api/exams/generate`, en lugar de un nivel único, un porcentaje entero para cada nivel de dificultad (`Basic`, `Intermediate`, `Advanced`). Cada porcentaje SHALL estar entre 0 y 100 y los tres SHALL sumar 100; en otro caso, el sistema SHALL responder `400 Bad Request`. Un nivel al 0 % queda fuera del reparto y la prueba no tiene ninguna pregunta de ese nivel. Indicar a la vez un nivel único y un reparto SHALL responder `400 Bad Request`. El porcentaje SHALL referirse al número de preguntas, no a los puntos.

El número de preguntas de cada nivel SHALL calcularse con el método del mayor resto: cada nivel recibe la parte entera de su cuota (`QuestionCount × porcentaje / 100`), y las preguntas que faltan hasta `QuestionCount` van, una a una, a los niveles con mayor parte decimal. Si dos niveles empatan en la parte decimal, la pregunta va al de mayor porcentaje, y si también empatan, al nivel más fácil.

Las preguntas de cada nivel SHALL elegirse al azar entre las preguntas activas de ese nivel en las categorías indicadas, o en todas si no se indica ninguna. El orden de las preguntas en la prueba SHALL mezclar los niveles, no agruparlos.

#### Scenario: Reparto exacto
- **GIVEN** un banco con suficientes preguntas activas de los tres niveles
- **WHEN** un administrador genera una prueba de 10 preguntas con el reparto 30 / 50 / 20
- **THEN** la prueba tiene 3 preguntas básicas, 5 intermedias y 2 avanzadas

#### Scenario: Redondeo por mayor resto
- **WHEN** un administrador genera una prueba de 10 preguntas con el reparto 33 / 33 / 34
- **THEN** la prueba tiene 3 preguntas básicas, 3 intermedias y 4 avanzadas, que suman 10

#### Scenario: Empate en el redondeo
- **WHEN** un administrador genera una prueba de 3 preguntas con el reparto 50 / 50 / 0
- **THEN** la prueba tiene 2 preguntas básicas y 1 intermedia, porque en el empate gana el nivel más fácil, y ninguna avanzada

#### Scenario: Un nivel al 0 % no aparece
- **WHEN** un administrador genera una prueba con el reparto 0 / 100 / 0
- **THEN** todas las preguntas de la prueba son intermedias

#### Scenario: Porcentajes que no suman 100
- **WHEN** un administrador genera una prueba con el reparto 30 / 30 / 30
- **THEN** el sistema responde `400 Bad Request` y no crea la prueba

#### Scenario: Nivel único y reparto a la vez
- **WHEN** la petición indica el nivel `Advanced` y además un reparto
- **THEN** el sistema responde `400 Bad Request` y no crea la prueba

#### Scenario: Los niveles salen mezclados
- **WHEN** un administrador genera una prueba con un reparto de dos o más niveles
- **THEN** el orden de la prueba no agrupa las preguntas por nivel de forma sistemática

### Requirement: Rechazo si un nivel del reparto no alcanza
Si algún nivel del reparto necesita más preguntas de las que hay activas de ese nivel en las categorías indicadas, el sistema SHALL rechazar la generación con `400 Bad Request` y SHALL NOT crear la prueba. El mensaje SHALL nombrar cada nivel que no alcanza, con las preguntas que necesita y las que hay. El sistema SHALL NOT completar nunca un nivel con preguntas de otro nivel ni de otra categoría.

#### Scenario: Faltan preguntas avanzadas
- **GIVEN** unas categorías con 8 preguntas básicas, 15 intermedias y 2 avanzadas activas
- **WHEN** un administrador genera una prueba de 10 preguntas con el reparto 20 / 30 / 50
- **THEN** el sistema rechaza la generación con un mensaje que dice que el nivel avanzado necesita 5 preguntas y hay 2, y no crea la prueba

#### Scenario: Faltan preguntas de dos niveles
- **GIVEN** unas categorías con 1 pregunta básica, 20 intermedias y 1 avanzada activas
- **WHEN** un administrador genera una prueba de 10 preguntas con el reparto 30 / 40 / 30
- **THEN** el mensaje nombra el nivel básico (necesita 3, hay 1) y el avanzado (necesita 3, hay 1)

#### Scenario: Sobran preguntas de otros niveles
- **GIVEN** unas categorías con 50 preguntas básicas y 1 intermedia activas
- **WHEN** un administrador genera una prueba de 5 preguntas con el reparto 0 / 100 / 0
- **THEN** el sistema rechaza la generación, aunque haya preguntas básicas de sobra

### Requirement: El nivel único no cambia
Cuando la generación indica un nivel único, la prueba SHALL tener solo preguntas de ese nivel, y el sistema SHALL rechazar la generación si no hay bastantes, como hoy. Sin nivel ni reparto, el comportamiento SHALL seguir como hoy: preguntas al azar de todos los niveles.

#### Scenario: Nivel único sin preguntas suficientes
- **GIVEN** unas categorías con 2 preguntas avanzadas activas
- **WHEN** un administrador genera una prueba de 5 preguntas con el nivel único `Advanced`
- **THEN** el sistema rechaza la generación y no crea la prueba
