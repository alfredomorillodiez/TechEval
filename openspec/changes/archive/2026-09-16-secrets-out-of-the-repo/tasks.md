## 1. Validador de arranque

- [x] 1.1 Añadir en `src/TechEval.API` un validador que compruebe los secretos obligatorios y falle si falta alguno o si conserva su valor de desarrollo. Verificar que devuelve el nombre de cada uno que falla.
- [x] 1.2 Invocarlo en `Program.cs` antes de construir la aplicación, y solo fuera del entorno de desarrollo. Verificar que un arranque en desarrollo no lo ejecuta.
- [x] 1.3 Quitar el `?? "Admin@123!"` de `Program.cs`. Verificar que no queda ninguna credencial por defecto en el código.

## 2. Configuración

- [x] 2.1 Vaciar en `src/TechEval.API/appsettings.json` la clave JWT y quitar `AdminPassword`. Verificar que no queda ningún valor de secreto en ese fichero.
- [x] 2.2 Poner los valores de desarrollo en `src/TechEval.API/appsettings.Development.json`, con un comentario que diga lo que son. Verificar que el arranque en desarrollo sigue funcionando sin variables de entorno.
- [x] 2.3 Sustituir en `docker-compose.yml` los cuatro literales por `${VARIABLE:?mensaje}`. Verificar que `docker compose config` falla nombrando la variable cuando no está definida.
- [x] 2.4 Crear `.env.example` con los nombres de las variables, sin valores, y con la indicación de cómo generar la clave JWT. Verificar que no contiene ningún valor real.

## 3. Repositorio

- [x] 3.1 Añadir `appsettings.Local.json` a `.gitignore`. Verificar que `.env` ya estaba.
- [x] 3.2 Sacar `src/TechEval.API/appsettings.Local.json` del índice con `git rm --cached`, conservándolo en disco. Verificar que `git ls-files` ya no lo devuelve y que el fichero sigue existiendo.

## 4. Documentación

- [x] 4.1 Documentar en el README las variables necesarias y cómo generar la clave JWT. Verificar que la tabla de configuración ya no muestra valores por defecto de secretos.
- [x] 4.2 Corregir en el README el aviso de secretos, que hoy dice "cámbialas antes de desplegar" y debe decir que hay que rotarlas porque estuvieron en git. Verificar el texto.
- [x] 4.3 Escribir las notas de despliegue del cambio, con la lista de lo que hay que rotar y el aviso de que la clave JWT nueva caduca las invitaciones en curso. Verificar que la nota existe.

## 5. Pruebas

- [x] 5.1 Añadir las pruebas del validador: falta un secreto, secreto con valor de desarrollo, y configuración completa. Verificar que pasan.
- [x] 5.2 Ejecutar `dotnet test TechEval.sln` completo. Verificar que las 103 pruebas anteriores siguen en verde junto a las nuevas.

## 6. Cierre

- [x] 6.1 Reconstruir la solución completa antes de arrancar la API, por lo dicho en el hallazgo E8. Verificar que el binario es el nuevo.
- [x] 6.2 Comprobar que la API arranca en desarrollo sin configurar nada. Verificar que responde.
- [x] 6.3 Comprobar que la API se niega a arrancar con el entorno en producción y sin secretos. Verificar el mensaje de error.
- [x] 6.4 Comprobar que se niega también con el entorno en producción y la clave de desarrollo. Verificar el mensaje.
- [x] 6.5 Comprobar que no queda ningún secreto en los ficheros versionados. Verificar con una búsqueda sobre el contenido seguido por git.
