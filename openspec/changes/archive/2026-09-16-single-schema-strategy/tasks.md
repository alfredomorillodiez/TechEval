## 1. El guion completo se pone al día

- [x] 1.1 Añadir a `UserAnswers` en `create_database.sql` las cuatro columnas de la copia. Verificar que coinciden en nombre, tipo y longitud con el guion aditivo.
- [x] 1.2 Ejecutar el guion completo sobre una base nueva y comprobar que las columnas existen. Verificar con una consulta al catálogo.

## 2. La prueba de deriva

- [x] 2.1 Escribir la prueba que compara las tablas del modelo con los `CREATE TABLE` del guion. Verificar que pasa.
- [x] 2.2 Ampliarla a las columnas de cada tabla. Verificar que pasa.
- [x] 2.3 Comprobar que la prueba **falla** si se quita una columna del guion. Verificar quitándola y volviendo a ponerla.
- [x] 2.4 Dejar escrito en la prueba qué no comprueba: tipos, longitudes y claves foráneas. Verificar leyéndola.

## 3. El esquema deja de crearse solo

- [x] 3.1 Quitar `EnsureCreatedAsync` de `DbSeeder`. Verificar que no queda ninguna llamada en `src/`.
- [x] 3.2 Comprobar el esquema al arrancar y parar con un mensaje que nombre el guion. Verificar que el mensaje incluye la ruta.
- [x] 3.3 Arrancar contra una base vacía y comprobar que para con ese mensaje. Verificar contra el binario real.
- [x] 3.4 Arrancar contra una base con esquema y comprobar que sigue con normalidad. Verificar contra el binario real.

## 4. El contenido de ejemplo

- [x] 4.1 Separar en `DbSeeder` la siembra del administrador de la del contenido de ejemplo. Verificar leyendo el método.
- [x] 4.2 Sembrar el contenido de ejemplo solo en desarrollo. Verificar que el administrador se siembra siempre.
- [x] 4.3 Arrancar en producción contra una base vacía de contenido y comprobar que hay administrador y no hay categorías. Verificar con una consulta.

## 5. Documentación

- [x] 5.1 Quitar del `README.md` la instrucción de migraciones de EF. Verificar que no queda ninguna mención de `dotnet ef`.
- [x] 5.2 Decir en el `README.md` que el guion es obligatorio antes del primer arranque. Verificar leyéndolo.
- [x] 5.3 Revisar `documentacion.md` por la misma instrucción. Verificar con una búsqueda.

## 6. Cierre

- [x] 6.1 Ejecutar `dotnet test` completo. Verificar que sigue en verde.
- [x] 6.2 Recorrer el arranque completo desde cero: guion, API, login. Verificar que funciona sin ejecutar ningún guion aditivo.
