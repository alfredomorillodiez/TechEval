## MODIFIED Requirements

### Requirement: Auto-envío al agotarse el tiempo
La interfaz SHALL enviar automáticamente la prueba completa, con todas las respuestas registradas hasta ese momento, en el instante en que el temporizador alcanza cero, sin requerir confirmación del candidato. La interfaz SHALL NOT lanzar un segundo envío mientras haya uno en curso.

#### Scenario: El tiempo se agota durante la resolución
- **WHEN** el temporizador en cuenta atrás llega a cero mientras el candidato aún está respondiendo la prueba
- **THEN** la interfaz detiene el temporizador y envía automáticamente la prueba con el conjunto de respuestas disponible en ese momento, sin mostrar el diálogo de confirmación manual

#### Scenario: El tiempo se agota con un envío manual ya en vuelo
- **GIVEN** que el candidato ha pulsado "Finalizar" y su envío todavía no ha respondido
- **WHEN** el temporizador alcanza cero
- **THEN** la interfaz SHALL NOT lanzar un segundo envío
- **AND** el candidato ve el resultado del envío que ya estaba en curso
