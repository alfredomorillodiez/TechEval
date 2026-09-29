## ADDED Requirements

### Requirement: El detalle muestra quién corrigió
El detalle de un resultado que el administrador consulta SHALL incluir, cuando el resultado tiene preguntas abiertas corregidas, el nombre de quien lo corrigió y la fecha de la corrección. Si la cuenta del corrector está desactivada, el detalle SHALL mostrar igualmente su nombre.

#### Scenario: Resultado corregido por un evaluador
- **GIVEN** un resultado corregido por el evaluador "Laura Gil"
- **WHEN** un administrador consulta su detalle
- **THEN** el detalle indica que lo corrigió Laura Gil, y cuándo

#### Scenario: Resultado sin preguntas abiertas
- **GIVEN** un resultado corregido de forma automática, sin preguntas abiertas
- **WHEN** un administrador consulta su detalle
- **THEN** el detalle no indica ningún corrector
