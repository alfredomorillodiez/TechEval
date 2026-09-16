## 1. Orígenes de paquetes (E3)

- [x] 1.1 Escribir `NuGet.config` en la raíz con `<clear />` y solo nuget.org. Verificar que lleva un comentario que explica por qué está el `<clear />`.
- [x] 1.2 Restaurar **sin** el rodeo de `-p:RestoreSources=`. Verificar que ya no hace falta.
- [x] 1.3 Compilar y ejecutar la batería completa sin el rodeo. Verificar que pasan las 140.

## 2. Flujo de integración continua (E1)

- [x] 2.1 Escribir `.github/workflows/ci.yml` con restaurar, compilar y probar. Verificar que se dispara en subida y en solicitud de incorporación.
- [x] 2.2 Fijar la versión del SDK a la del proyecto. Verificar contra `global.json` o el `TargetFramework`.
- [x] 2.3 Comprobar que el fichero es YAML válido. Verificar con un analizador.

## 3. Cierre

- [x] 3.1 Ejecutar en local los mismos tres comandos del flujo, en el mismo orden. Verificar que los tres terminan bien.
- [x] 3.2 Dejar constancia de lo que **no** se ha podido comprobar: la ejecución del flujo en GitHub. Verificar que queda escrito en el commit.
