## ADDED Requirements

### Requirement: Marca de agua con la identidad del candidato
Durante la resolución de la prueba, la interfaz SHALL mostrar sobre el contenido una marca de agua con el nombre y el correo del candidato, en diagonal, repetida y semitransparente. El detalle de la sesión SHALL incluir el correo del candidato para este fin. La marca SHALL cubrir el área de las preguntas, de forma que una captura de una pregunta incluya la identidad del candidato.

La marca SHALL NOT impedir leer, elegir una opción, escribir en un área de texto ni pulsar un botón. La marca SHALL NOT poderse seleccionar ni copiar, y las tecnologías de asistencia SHALL ignorarla.

#### Scenario: La marca de agua aparece durante la prueba
- **WHEN** la interfaz muestra la pantalla de preguntas
- **THEN** la marca de agua con el nombre y el correo del candidato cubre el área de las preguntas

#### Scenario: La marca de agua no bloquea la interacción
- **WHEN** el candidato pulsa una opción, escribe en un área de texto o pulsa un botón de navegación que está debajo de la marca
- **THEN** la acción llega al control, igual que sin la marca

#### Scenario: La marca de agua no llega al lector de pantalla
- **WHEN** un lector de pantalla recorre la pantalla de preguntas
- **THEN** el lector no anuncia el texto de la marca de agua

#### Scenario: La marca de agua no aparece fuera de la resolución
- **WHEN** la interfaz muestra la validación del enlace, la pantalla de bienvenida o la pantalla final
- **THEN** la interfaz no muestra la marca de agua

### Requirement: Aviso de registro de actividad antes de empezar
La pantalla de bienvenida SHALL informar, antes del botón "Comenzar prueba", de que durante la prueba:
- Se registran las salidas de la página y su duración.
- Se registran los pegados en las respuestas abiertas, con el número de caracteres, pero no su texto.
- Las preguntas muestran una marca de agua con el nombre y el correo del candidato.
- El equipo evaluador ve este registro, y el registro no cambia la nota de forma automática.

#### Scenario: El aviso aparece en la bienvenida
- **WHEN** la interfaz muestra la pantalla de bienvenida
- **THEN** el aviso de registro aparece antes del botón "Comenzar prueba" y enumera los cuatro puntos

#### Scenario: La reanudación no repite el aviso
- **GIVEN** un candidato que ya vio el aviso y empezó la prueba
- **WHEN** el candidato vuelve a abrir el enlace de la prueba en curso
- **THEN** la interfaz entra en las preguntas sin mostrar la bienvenida, conforme al requisito de validación del enlace
