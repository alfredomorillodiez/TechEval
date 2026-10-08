## REMOVED Requirements

### Requirement: El script de creación completa del esquema incluye las columnas de corrección
**Reason**: El requisito solo exigía alinear las columnas de la corrección manual, que eran las últimas añadidas cuando se escribió. Nada obligaba a mantener esa alineación para lo que viniera después, y el 16·09 se añadieron cuatro columnas a `UserAnswers` sin llevarlas al guion. Se sustituye por uno que cubre todo el modelo y que una prueba puede hacer cumplir.
**Migration**: Ver «El guion de creación completa refleja el modelo». No hay cambio de comportamiento: lo que antes exigía para dos tablas, ahora se exige para todas.

## ADDED Requirements

### Requirement: El guion de creación completa refleja el modelo
El sistema SHALL mantener `scripts/create_database.sql` alineado con el modelo de datos: toda entidad del modelo SHALL tener su tabla en el guion, y toda propiedad persistida SHALL tener su columna. Una base creada desde cero con ese guion y otra actualizada con los guiones aditivos SHALL converger en el mismo esquema.

El proyecto SHALL incluir una prueba que compare el modelo con el guion y falle cuando dejen de coincidir. La prueba SHALL entenderse como una red contra el olvido, no como una validación del esquema: comprueba nombres, no tipos ni longitudes ni claves foráneas.

#### Scenario: Creación desde cero sin necesidad de guiones aditivos
- **WHEN** se ejecuta `scripts/create_database.sql` sobre un servidor sin la base de datos
- **THEN** todas las tablas SHALL crearse ya con todas las columnas que el modelo persiste, sin necesidad de ejecutar después ningún guion aditivo

#### Scenario: Una columna nueva sin llevar al guion rompe la prueba
- **GIVEN** una propiedad persistida añadida al modelo
- **WHEN** no se añade la columna correspondiente a `scripts/create_database.sql`
- **THEN** la prueba de alineación SHALL fallar nombrando la columna que falta

#### Scenario: Una entidad nueva sin llevar al guion rompe la prueba
- **GIVEN** una entidad añadida al modelo
- **WHEN** no se añade su `CREATE TABLE` a `scripts/create_database.sql`
- **THEN** la prueba de alineación SHALL fallar nombrando la tabla que falta

### Requirement: El esquema lo crean los guiones, no la aplicación
La aplicación SHALL NOT crear el esquema al arrancar. `scripts/create_database.sql` SHALL ser la única forma de crearlo, y los guiones aditivos la única forma de actualizarlo.

Si al arrancar el esquema no existe, el sistema SHALL detenerse con un mensaje que nombre el guion que hay que ejecutar, en lugar de crearlo por su cuenta o de fallar con un error de base de datos que no explique nada.

La documentación SHALL NOT proponer `dotnet ef database update` como alternativa: crear el esquema desde el modelo impide que las migraciones de EF funcionen después, porque su tabla de historial no llega a existir.

#### Scenario: Arranque contra una base sin esquema
- **GIVEN** una base de datos alcanzable pero sin las tablas de la aplicación
- **WHEN** la API arranca
- **THEN** el sistema MUST detener el arranque con un mensaje que nombre `scripts/create_database.sql`
- **AND** el sistema SHALL NOT crear ninguna tabla

#### Scenario: Arranque contra una base con esquema
- **GIVEN** una base de datos creada con el guion
- **WHEN** la API arranca
- **THEN** el sistema SHALL continuar con normalidad y sembrar lo que corresponda

### Requirement: El contenido de ejemplo solo se siembra en desarrollo
El sistema SHALL sembrar el usuario administrador en cualquier entorno, porque sin él no hay forma de entrar. El contenido de ejemplo —categorías y preguntas de muestra— SHALL sembrarse únicamente en desarrollo.

#### Scenario: Primer arranque de un despliegue de cliente
- **GIVEN** un despliegue con el entorno distinto de desarrollo y una base de datos vacía de contenido
- **WHEN** la API arranca
- **THEN** el sistema SHALL crear el administrador
- **AND** el sistema SHALL NOT crear categorías ni preguntas de ejemplo, de forma que el banco de preguntas del cliente empiece vacío

#### Scenario: Primer arranque en desarrollo
- **GIVEN** el entorno de desarrollo y una base de datos vacía de contenido
- **WHEN** la API arranca
- **THEN** el sistema SHALL crear el administrador, las categorías y las preguntas de ejemplo, para que la aplicación sea utilizable sin preparar datos a mano
