## ADDED Requirements

### Requirement: El código se compila y se prueba en cada subida
El repositorio SHALL incluir un flujo de trabajo de integración continua que, en cada subida a una rama del repositorio y en cada solicitud de incorporación, restaure las dependencias, compile la solución completa y ejecute la batería de pruebas.

El flujo SHALL fallar cuando la compilación falle o cuando alguna prueba falle, de forma que un cambio que rompa algo se detecte antes de fusionarlo y no al usarlo.

#### Scenario: Subida con la batería en verde
- **WHEN** alguien sube un cambio que compila y cuyas pruebas pasan
- **THEN** el flujo SHALL terminar correctamente

#### Scenario: Subida que rompe una prueba
- **GIVEN** un cambio que hace fallar una prueba de la batería
- **WHEN** se sube o se abre una solicitud de incorporación
- **THEN** el flujo SHALL fallar señalando la prueba que no pasa

#### Scenario: Subida que no compila
- **WHEN** se sube un cambio que no compila
- **THEN** el flujo SHALL fallar en el paso de compilación, sin llegar a ejecutar las pruebas

### Requirement: La restauración de paquetes no depende de un feed privado
El repositorio SHALL declarar sus orígenes de paquetes en un `NuGet.config` propio que descarte los heredados de la máquina y deje únicamente los públicos que el proyecto necesita.

Sin esa declaración, la restauración hereda los feeds configurados en el equipo de quien compila. En esta organización eso incluye feeds privados que exigen credenciales, y la restauración falla con `401` aunque todos los paquetes del proyecto sean públicos.

#### Scenario: Restauración en una máquina sin credenciales
- **GIVEN** una máquina sin credenciales para los feeds privados de la organización
- **WHEN** se ejecuta la restauración de paquetes del repositorio
- **THEN** la restauración SHALL completarse, porque el repositorio declara sus propios orígenes

#### Scenario: Restauración en la integración continua
- **GIVEN** un ejecutor de integración continua, que nunca tiene esas credenciales
- **WHEN** el flujo restaura las dependencias
- **THEN** la restauración SHALL completarse sin configuración adicional
