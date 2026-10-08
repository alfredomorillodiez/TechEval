## Context

Ver `proposal.md` — Why para la motivación.

Lo que condiciona el enfoque:

- La máquina de desarrollo tiene registrados dos feeds privados de Azure DevOps. Sin credenciales, `dotnet restore` falla con `401`.
- Durante todo este trabajo la solución solo ha compilado con `-p:RestoreSources=https://api.nuget.org/v3/index.json`. Es un rodeo, no un arreglo.
- La solución tiene doce avisos del compilador. `E4` —tratarlos como errores— sigue abierto.
- Las pruebas usan EF en memoria y dobles. Ninguna necesita SQL Server, así que el flujo no necesita servicios.

## Goals / Non-Goals

**Goals:**

- Que un cambio que rompa una prueba se vea antes de fusionarlo.
- Que clonar y compilar no exija acceso a nada.

**Non-Goals:**

- No se tratan los avisos como errores. Eso es `E4`, y hacerlo aquí haría fallar la primera ejecución por motivos que no tienen que ver con este cambio.
- No se publican artefactos ni se despliega nada.
- No se añaden pruebas de integración con base de datos. Eso es `E2`.

## Decisions

### `NuGet.config` con `<clear />`

Sin `<clear />`, el fichero del repositorio **añade** orígenes a los heredados en vez de sustituirlos, y los privados seguirían ahí.

**Riesgo asumido**: si algún día el proyecto usara un paquete privado de Pronet, el `<clear />` lo dejaría fuera y habría que declararlo aquí con su autenticación. Hoy no usa ninguno, y lo he comprobado restaurando sin ellos.

### El flujo corre en `ubuntu-latest`

Más rápido y más barato que Windows, y nada del proyecto lo impide: las pruebas no tocan SQL Server ni nada específico de Windows.

**Alternativa descartada**: `windows-latest`, por parecerse al entorno de desarrollo. Cuesta más y no aporta: lo que se comprueba es la solución, no el sistema operativo.

### Un solo trabajo, sin caché al principio

Restaurar, compilar y probar en un trabajo. Se podría separar y cachear `~/.nuget/packages`, pero con este tamaño de solución la ganancia es pequeña y la caché añade una forma nueva de que el flujo mienta: una caché envenenada da un verde falso.

Conviene añadirla cuando la ejecución empiece a molestar, no antes.

### Los avisos se ven, pero no rompen

El flujo los deja a la vista en el registro. Convertirlos en error es `E4` y pide antes limpiarlos, que es trabajo aparte.

## Risks / Trade-offs

**No puedo comprobar que el flujo se ejecute** → Eso ocurre en GitHub al subir. Mitigación: comprobar en local exactamente los tres comandos que ejecutará, y sin el feed privado, que es lo que falla primero y en silencio. Lo que quede por ver es la sintaxis del fichero de flujo, no si el proyecto compila.

**El `<clear />` puede sorprender a alguien** → Quien añada un paquete privado verá un error de paquete no encontrado en vez de uno de credenciales. El fichero lleva un comentario que lo explica.

**Un flujo que tarda acaba ignorándose** → Con 140 pruebas que corren en segundos no es un problema hoy.

## Migration Plan

Sin cambios de esquema. Sin cambios de API. Sin cambios de comportamiento.

El flujo empieza a ejecutarse en la primera subida después de fusionar.

**Vuelta atrás**: borrar los dos ficheros.
