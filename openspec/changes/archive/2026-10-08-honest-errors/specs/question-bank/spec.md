## ADDED Requirements

### Requirement: Editar una pregunta no cambia su estado activo
Editar una pregunta desde la interfaz SHALL conservar su estado activo. El estado SHALL cambiar solo cuando la persona usa el interruptor «Activa» del formulario. Una pregunta desactivada que se edita y se guarda sin tocar el interruptor SHALL seguir desactivada.

#### Scenario: Guardar una pregunta desactivada sin tocar el interruptor
- **GIVEN** una pregunta con `IsActive = false`
- **WHEN** un administrador la edita desde el formulario y guarda sin activar el interruptor
- **THEN** la petición de actualización lleva `IsActive = false` y la pregunta sigue desactivada

#### Scenario: Reactivar con el interruptor
- **GIVEN** una pregunta con `IsActive = false`
- **WHEN** el administrador activa el interruptor y guarda
- **THEN** la petición lleva `IsActive = true` y la pregunta queda activa
