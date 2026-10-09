## ADDED Requirements

### Requirement: El guion de rotación conserva lo que no es secreto
El guion de rotación SHALL copiar del fichero de entorno anterior, sin cambiarlos, los valores de `MSSQL_PID`, `FRONTEND_BASE_URL`, `API_BASE_URL`, `O365_TENANT_ID`, `O365_CLIENT_ID` y `EMAIL_FROM`, para que el fichero nuevo siga bastando para levantar el stack. Si el anterior no los tenía, SHALL escribir los valores de ejemplo de `.env.example` para las dos URL y SHALL dejar `MSSQL_PID` vacío, de modo que Compose obligue a elegir la edición. `SA_PASSWORD`, `JWT_SECRET_KEY` y `ADMIN_PASSWORD` SHALL generarse nuevos, y `O365_CLIENT_SECRET` SHALL quedar vacío porque lo emite Entra ID. El guion NEVER SHALL copiar al fichero nuevo el valor de un secreto del anterior.

#### Scenario: Rotar con un .env en uso
- **GIVEN** un `.env` con `MSSQL_PID=Express`, `FRONTEND_BASE_URL=https://techeval.empresa.com` y `EMAIL_FROM=correo@empresa.com`
- **WHEN** se ejecuta el guion de rotación
- **THEN** el `.env` nuevo conserva esos tres valores
- **AND** los tres secretos generados difieren de los anteriores

#### Scenario: Rotar sin las variables nuevas
- **GIVEN** un `.env` anterior a `docker-outside-localhost`, sin `MSSQL_PID` ni las dos URL
- **WHEN** se ejecuta el guion de rotación
- **THEN** el `.env` nuevo trae las dos URL de ejemplo y `MSSQL_PID` vacío
- **AND** el guion avisa de que `MSSQL_PID` falta

#### Scenario: El secreto de cliente no se copia
- **GIVEN** un `.env` con un `O365_CLIENT_SECRET` en uso
- **WHEN** se ejecuta el guion de rotación
- **THEN** el `.env` nuevo deja `O365_CLIENT_SECRET` vacío

### Requirement: Una comprobación dice si la rotación se hizo
El repositorio SHALL incluir un guion `comprobar-rotacion` que lea el fichero de entorno y, para `SA_PASSWORD`, `JWT_SECRET_KEY`, `ADMIN_PASSWORD` y `O365_CLIENT_SECRET`, busque cada valor en todo el historial de git. Un valor que aparezca en cualquier commit SHALL contar como no rotado. El guion SHALL fallar también si falta uno de los cuatro, si un valor es demasiado corto para comprobarlo con fiabilidad (menos de 8 caracteres), si `.env` o `appsettings.Local.json` están rastreados por git, o si el repositorio es una copia superficial, porque un historial incompleto daría un falso «todo bien». El guion NEVER SHALL escribir ningún valor en la salida estándar ni en un fichero: SHALL nombrar solo la variable. Sale con código 0 únicamente si todo está bien.

#### Scenario: Un secreto que está en el historial
- **GIVEN** un `.env` cuyo `SA_PASSWORD` es el valor que un commit anterior escribió en `docker-compose.yml`
- **WHEN** se ejecuta la comprobación
- **THEN** el guion dice que `SA_PASSWORD` está en el historial y sale con error
- **AND** no muestra el valor

#### Scenario: Todos los secretos son nuevos
- **GIVEN** un `.env` con cuatro valores que no aparecen en ningún commit
- **WHEN** se ejecuta la comprobación
- **THEN** el guion dice que los cuatro están rotados y sale con 0

#### Scenario: Falta un secreto
- **GIVEN** un `.env` con `O365_CLIENT_SECRET` vacío
- **WHEN** se ejecuta la comprobación
- **THEN** el guion dice que falta `O365_CLIENT_SECRET` y sale con error

#### Scenario: Un valor demasiado corto
- **GIVEN** un `.env` con `ADMIN_PASSWORD=abc`
- **WHEN** se ejecuta la comprobación
- **THEN** el guion dice que el valor es demasiado corto para comprobarlo y sale con error, en lugar de dar por bueno uno que buscaría por una cadena trivial

#### Scenario: Un fichero con secretos entra en git
- **GIVEN** un `.env` rastreado por git
- **WHEN** se ejecuta la comprobación
- **THEN** el guion sale con error y nombra el fichero

#### Scenario: Una copia superficial del repositorio
- **GIVEN** un repositorio clonado con `--depth 1`
- **WHEN** se ejecuta la comprobación
- **THEN** el guion sale con error y dice que el historial está incompleto
