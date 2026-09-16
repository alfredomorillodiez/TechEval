# continuous-integration

Que cada subida compile y pase la batería sola, y que para conseguirlo no haga falta un feed privado

## Why

Hallazgos `E1` y `E3`. Van juntos porque el segundo bloquea al primero.

**E1 — nada comprueba el código al subirlo.** `.github/` solo tiene instrucciones para Copilot y una carpeta `workflows/` vacía. Las 140 pruebas valen lo que valga acordarse de ejecutarlas.

Esto pesa más hoy que cuando se escribió la auditoría. El 16·09 se cerraron diecisiete hallazgos, y cada uno dejó pruebas que guardan lo arreglado: la reanudación del examen, el envío idempotente, la propiedad de la sesión, el plazo en servidor, la copia de lo preguntado, el límite de ritmo, la alineación del esquema. **Sin integración continua, cualquiera de esas diecisiete cosas puede deshacerse sin que nadie se entere hasta que un candidato lo sufra.**

**E3 — la restauración de paquetes exige el feed privado de Pronet.** El repositorio no trae `NuGet.config`, así que hereda el del usuario, con los feeds de Azure DevOps de la casa. Sin credenciales, `dotnet restore` falla con `401` aunque todos los paquetes del proyecto sean públicos.

Un ejecutor de integración continua no tiene esas credenciales. Sin resolver E3, el flujo de trabajo de E1 falla en el primer paso.

## What Changes

- `NuGet.config` en la raíz, con `<clear />` y solo nuget.org.
- Un flujo de trabajo de GitHub Actions que restaura, compila y ejecuta las pruebas en cada subida y en cada solicitud de incorporación.
- El flujo trata los avisos del compilador como información, no como error: `E4` sigue abierto y convertirlos en error ahora rompería la primera ejecución.

## Capabilities

- `project-architecture` — la comprobación automática del código

## Impact

**Deja de hacer falta el rodeo de `-p:RestoreSources=`.** Cualquiera puede clonar y compilar sin pedir acceso a nada.

**Si el proyecto llegara a usar un paquete privado de Pronet**, el `<clear />` lo dejaría fuera. Hoy no usa ninguno: la restauración contra nuget.org funciona.

**No puedo comprobar que el flujo se ejecute.** Eso pasa en GitHub, al subir. Lo que sí puedo comprobar es lo que falla primero y en silencio: que la restauración y la batería funcionen sin el feed privado, con el mismo comando que ejecutará el flujo.
