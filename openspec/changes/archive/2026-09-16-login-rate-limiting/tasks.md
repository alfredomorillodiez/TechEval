## 1. Limitador

- [x] 1.1 Registrar `AddRateLimiter` en `Program.cs` con dos políticas de ventana fija, particionadas por dirección de origen. Verificar que los cupos quedan en un solo sitio y con nombre.
- [x] 1.2 Escribir el `OnRejected` que responde `429` con `ProblemDetails` y cabecera `Retry-After`. Verificar que el cuerpo tiene la misma forma que el resto de errores.
- [x] 1.3 Activar `UseRateLimiter` en el orden correcto del pipeline. Verificar que va antes de que la petición llegue al controlador.

## 2. Aplicación a los dos endpoints

- [x] 2.1 Marcar `POST /api/auth/login` con la política estricta. Verificar que ningún otro endpoint de `AuthController` queda limitado sin querer.
- [x] 2.2 Marcar `GET /api/exam/validate/{token}` con la política ancha. Verificar que `start`, `answer` y `submit` no quedan limitados.

## 3. Dirección de origen real

- [x] 3.1 Añadir `UseForwardedHeaders` con la lista de proxies de confianza vacía por defecto. Verificar que sin proxy la cabecera se ignora.
- [x] 3.2 Documentar en `README.md` qué hay que configurar si se despliega detrás de un proxy inverso. Verificar que dice qué pasa si no se hace.

## 4. Pruebas

- [x] 4.1 Añadir pruebas de la política: dentro del cupo pasa, fuera del cupo devuelve `429`. Verificar que pasan.
- [x] 4.2 Añadir la prueba de que el `429` lleva `Retry-After`. Verificar que pasa. **La prueba cubre el ayudante, no la respuesta real**: el tipo de contenido se comprobó contra la API, y allí apareció un fallo que la prueba no veía — `WriteAsJsonAsync` pisaba `application/problem+json` con `application/json`. Corregido.
- [x] 4.3 Ejecutar `dotnet test` completo. Verificar que las pruebas anteriores siguen en verde.

## 5. Corrección de la spec de autenticación

- [x] 5.1 Corregir el escenario que afirma que el alumno entra con la parte local de su email. Es falso desde `S2`, y mi delta de entonces no lo tocó. Verificar leyendo la spec principal tras archivar.

## 6. Cierre

- [x] 6.1 Reconstruir y arrancar la API. Comprobar contra la API real que el intento número N+1 de login devuelve `429`. Verificar el código y la cabecera.
- [x] 6.2 Comprobar que la validación del enlace aguanta varias recargas seguidas sin rechazar. Verificar contra la API real.
- [x] 6.3 Comprobar que el `429` no revela si la contraseña era correcta. Verificar el cuerpo de la respuesta.
