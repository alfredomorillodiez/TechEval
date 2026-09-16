## Why

El repositorio lleva secretos escritos. `docker-compose.yml` tiene la contraseña de `sa`, la clave JWT y la contraseña del administrador. `appsettings.json` tiene otra vez la clave JWT y la contraseña del administrador. Y `appsettings.Local.json`, que el README afirmaba que git ignora, **está versionado desde el primer commit** con credenciales SMTP reales.

Cualquiera con acceso de lectura al repositorio las tiene. Y quien clonó alguna vez las conserva aunque se borren hoy.

Peor que estar ahí es que el código las prefiere: `Program.cs` arranca con `AdminPassword ?? "Admin@123!"`. Un despliegue que olvide configurar la contraseña del administrador no falla — arranca con la conocida por todo el mundo, y nadie se entera.

## What Changes

- **Ningún secreto queda en el repositorio.** `docker-compose.yml` sustituye cada literal por `${VARIABLE:?mensaje}`, que hace fallar el arranque si falta. `appsettings.json` deja vacíos los campos de secreto.
- **`appsettings.Local.json` deja de estar versionado.** Se añade a `.gitignore` y se saca del índice, conservándolo en disco.
- Se añade un `.env.example` con los nombres de las variables, sin valores, y con la indicación de cómo generar cada una.
- **La API se niega a arrancar fuera de desarrollo si falta un secreto**, o si lleva todavía el valor de desarrollo. Desaparece el `?? "Admin@123!"`: un despliegue mal configurado para en seco en vez de arrancar con la contraseña que conoce todo el mundo.
- Los valores de desarrollo pasan a `appsettings.Development.json`, donde se ven por lo que son. Siguen versionados a propósito: son de desarrollo, están documentados en el README y el arranque en producción los rechaza explícitamente.
- El README documenta las variables y la generación de la clave JWT.

**Lo que este cambio NO hace: rotar las credenciales.** Mover un secreto no lo limpia; lo que estuvo en git está quemado y hay que emitir valores nuevos. Eso exige generar credenciales reales y tocar los sistemas de destino, y lo tiene que hacer una persona. El cambio deja el sitio preparado y la lista de qué rotar.

Fuera de alcance:

- Reescribir el historial de git. Con los secretos ya rotados aporta poco, y obliga a un `push --force` y a que todos reclonen.
- Un gestor de secretos externo. La decisión depende del destino de despliegue.

## Capabilities

### New Capabilities
(ninguna)

### Modified Capabilities

- `deployment-ops`: se **añade** el requisito "Los secretos llegan por configuración, no por el repositorio", con el arranque que falla ante una configuración incompleta. No se toca ningún requisito existente.

## Impact

- **Despliegue**: `docker-compose.yml` pierde los cuatro literales. `.env.example` nuevo. `.gitignore` gana `appsettings.Local.json`.
- **API**: `Program.cs` valida los secretos al arrancar y deja de traer una contraseña por defecto. `appsettings.json` queda sin valores de secreto; `appsettings.Development.json` los recibe, marcados como de desarrollo.
- **Repositorio**: `git rm --cached src/TechEval.API/appsettings.Local.json`. El fichero se queda en disco, deja de viajar.
- **Documentación**: el README describe las variables necesarias y cómo generar la clave JWT.
- **Pruebas**: se añade la del validador de secretos, que es lógica pura.
