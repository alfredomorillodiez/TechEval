## MODIFIED Requirements

### Requirement: Validación del enlace y pantalla de bienvenida
Al abrir la ruta `/prueba/{Token}`, la interfaz SHALL validar el token contra el backend antes de mostrar ningún contenido de la prueba. Si el token resulta válido, el backend SHALL además aprovisionar (o reutilizar) la cuenta del alumno y autenticarlo automáticamente, y la interfaz SHALL almacenar la sesión autenticada devuelta antes de presentar la pantalla de bienvenida con el título de la prueba, el nombre del candidato, el número de preguntas y el tiempo límite.

#### Scenario: Token válido muestra información de la prueba y autentica al alumno
- **WHEN** el candidato abre un enlace con un token que existe, no ha expirado y no ha sido usado
- **THEN** la interfaz recibe y almacena una sesión autenticada para el alumno, y muestra la pantalla de bienvenida con el título de la prueba, el nombre del candidato, el número de preguntas y el tiempo límite en minutos, junto con un botón para comenzar

#### Scenario: Token inválido o expirado bloquea el acceso
- **WHEN** el candidato abre un enlace con un token inexistente, expirado o ya utilizado
- **THEN** la interfaz muestra una pantalla de error con el mensaje devuelto por el backend y SHALL NOT mostrar la pantalla de bienvenida ni ninguna pregunta de la prueba, ni almacenar ninguna sesión autenticada

## ADDED Requirements

### Requirement: Continuidad sin fricción del aprovisionamiento
La interfaz SHALL completar el aprovisionamiento y auto-login del alumno de forma transparente al abrir el enlace, sin solicitar en ningún momento que el candidato introduzca usuario o contraseña durante el flujo de resolución de la prueba.

#### Scenario: Apertura del enlace no requiere credenciales
- **WHEN** el candidato abre el enlace de una invitación, sea la primera vez o una vez que ya tiene cuenta de una prueba anterior
- **THEN** la interfaz avanza directamente a la pantalla de bienvenida sin mostrar ningún formulario de login
