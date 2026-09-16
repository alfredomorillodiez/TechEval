## 1. Derivación de contraseña

- [x] 1.1 Reescribir `src/TechEval.Application/Services/PasswordHasher.cs`: `Hash` produce `pbkdf2.sha256.<iteraciones>.<sal>.<hash>` con sal aleatoria de 16 bytes y 600 000 iteraciones. Verificar que dos llamadas con la misma contraseña devuelven valores distintos.
- [x] 1.2 Añadir `Verify(password, storedHash)` que acepte el formato nuevo y el SHA-256 anterior, y que informe de si el hash necesita migrarse. Verificar con pruebas de los dos formatos.
- [x] 1.3 Rechazar siempre un `storedHash` vacío o en blanco, sea cual sea la contraseña recibida. Verificar con su prueba, incluida la contraseña vacía.
- [x] 1.4 Comparar el hash con `CryptographicOperations.FixedTimeEquals` para no filtrar información por temporización. Verificar leyendo el método.

## 2. Login

- [x] 2.1 Sustituir `AuthController.VerifyPassword` por `PasswordHasher.Verify`. Verificar que el login con la contraseña correcta sigue funcionando contra un hash antiguo.
- [x] 2.2 Reescribir el `PasswordHash` del usuario cuando el login sea correcto y el hash estuviera en formato antiguo. Verificar que el segundo login del mismo usuario ya usa el formato nuevo.
- [x] 2.3 Comprobar que el login sigue negando el acceso a las cuentas con `IsActive = false`. Verificar que ese filtro no se ha tocado.

## 3. Cuentas de alumno

- [x] 3.1 Quitar el cálculo de contraseña de `ExamTokenService.GetOrCreateStudentAsync` (línea 167) y dejar `PasswordHash` vacío. Verificar que la cuenta se sigue creando con su email, nombre y `username`.
- [x] 3.2 Comprobar que una invitación con email ya existente sigue reutilizando la cuenta sin tocar su `PasswordHash`. Verificar con su prueba.

## 4. Administrador inicial

- [x] 4.1 Sustituir en `src/TechEval.API/Program.cs` (línea 130) el `SHA256.HashData` en línea por `PasswordHasher.Hash`. Verificar que no queda ningún uso de `SHA256` fuera de `PasswordHasher`.
- [x] 4.2 Comprobar que un arranque contra una base de datos vacía crea el administrador y que su login funciona. Verificar contra la API real.

## 5. Pruebas

- [x] 5.1 Añadir `tests/TechEval.Tests/Services/PasswordHasherTests.cs`: sal distinta por hash, verificación correcta e incorrecta, formato antiguo aceptado y marcado para migrar, hash vacío siempre rechazado. Verificar que pasan.
- [x] 5.2 Añadir la prueba de que una cuenta de alumno se aprovisiona sin contraseña utilizable. Verificar que pasa.
- [x] 5.3 Añadir la prueba de que entrar con la parte local del email de un alumno recién aprovisionado falla. Verificar que pasa.
- [x] 5.4 Ejecutar `dotnet test TechEval.sln` completo. Verificar que las 80 pruebas anteriores siguen en verde junto a las nuevas.

## 6. Cierre

- [x] 6.1 Reconstruir la solución completa antes de arrancar la API, por lo dicho en el hallazgo E8. Verificar que el binario es el nuevo.
- [x] 6.2 Comprobar contra la API real que el administrador entra con su contraseña actual, guardada en SHA-256. Verificar que responde 200.
- [x] 6.3 Comprobar en base de datos que su `PasswordHash` se reescribió al formato nuevo tras ese login. Verificar con una consulta.
- [x] 6.4 Comprobar contra la API real que un alumno recién aprovisionado no puede entrar con la parte local de su email. Verificar que responde 401.
- [x] 6.5 Añadir a las notas de despliegue la sentencia que vacía la contraseña de las cuentas de alumno ya creadas, y el aviso sobre la vuelta atrás. Verificar que la nota existe.
