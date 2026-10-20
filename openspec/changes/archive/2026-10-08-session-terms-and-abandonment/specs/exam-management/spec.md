## ADDED Requirements

### Requirement: Editar una prueba no cambia las sesiones ya empezadas
Editar el tiempo límite o la nota de corte de una prueba SHALL afectar solo a las sesiones que empiecen después. La pantalla de edición SHALL avisar de ello junto a esos dos campos.

#### Scenario: Editar con candidatos en curso
- **GIVEN** una prueba con candidatos que ya empezaron
- **WHEN** un administrador cambia su tiempo límite
- **THEN** las sesiones ya empezadas conservan su tiempo, y la pantalla avisa de que el cambio solo afecta a las que empiecen después

### Requirement: El listado de pruebas cuenta las sesiones abandonadas
`GET /api/exams` SHALL incluir en cada prueba el número de sesiones abandonadas (`AbandonedCount`), y la tarjeta de la prueba SHALL mostrarlo cuando sea mayor que cero.

#### Scenario: Una prueba con abandonos
- **GIVEN** una prueba con tres sesiones `Abandoned`
- **WHEN** un administrador abre el listado de pruebas
- **THEN** la tarjeta de la prueba muestra «3 abandonadas»

#### Scenario: Una prueba sin abandonos
- **GIVEN** una prueba sin sesiones abandonadas
- **WHEN** se abre el listado
- **THEN** la tarjeta no muestra ningún contador de abandonadas
