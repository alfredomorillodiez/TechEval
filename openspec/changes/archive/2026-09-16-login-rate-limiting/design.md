## Context

Ver `proposal.md` — Why para la motivación.

Lo que condiciona el enfoque:

- .NET 9 trae `AddRateLimiter` en el marco. No hace falta paquete externo.
- `ErrorHandlingMiddleware` ya responde `ProblemDetails` en un solo sitio, desde el arreglo de `S6`. El limitador debe usar el mismo formato, pero rechaza antes de llegar al middleware.
- `AuthController` accede al `DbContext` directamente (`A2`), así que no hay servicio donde poner la cuenta de intentos aunque se quisiera.
- El despliegue es de una sola instancia de la API.

## Goals / Non-Goals

**Goals:**

- Que un volumen moderado de peticiones no deje al servidor sin hilos.
- Que el rechazo sea legible y diga cuándo reintentar.
- Que un candidato legítimo no se quede fuera de su examen.

**Non-Goals:**

- No se bloquean cuentas. Bloquear por usuario permite que un tercero deje fuera a quien quiera con solo fallar su contraseña.
- No se guarda el histórico de intentos ni se notifica nada. Eso es traza de auditoría, y va aparte.
- No se comparte el recuento entre instancias.

## Decisions

### Ventana fija por dirección de origen

`AddFixedWindowLimiter` particionado por la dirección remota. Es lo más simple que resuelve el problema, y el problema es de coste en CPU, no de precisión estadística.

**Alternativa descartada**: `SlidingWindowLimiter`. Reparte mejor en el borde de la ventana, pero guarda más estado por partición y aquí no aporta: lo que importa es que el cupo exista, no que sea suave.

**Alternativa descartada**: `ConcurrencyLimiter`, que limita peticiones simultáneas en vez de por minuto. Encaja bien con el agotamiento de hilos, pero deja pasar un goteo indefinido de intentos, que es el otro medio problema.

### Dos cupos distintos

El inicio de sesión es caro y su uso legítimo es escaso: nadie entra diez veces por minuto. La validación del enlace es barata y su uso legítimo es frecuente: recargar la página la llama otra vez.

Un cupo único obligaría a elegir entre dejar pasar ataques al login o echar a candidatos de su examen.

### Se cuenta la dirección real, no la del proxy

Sin `UseForwardedHeaders`, detrás de un proxy inverso todas las peticiones llegan con la misma dirección y el cupo se comparte entre todos los candidatos. El primero en llegar agota el de los demás.

Se añade la configuración de cabeceras reenviadas. **Aviso:** confiar en `X-Forwarded-For` sin restringir qué proxies pueden fijarla permite que cualquiera se invente su origen y se salte el límite. La configuración exige declarar las redes de confianza; mientras no haya proxy, la lista va vacía y la cabecera se ignora.

### El `429` lo escribe el propio limitador

El rechazo ocurre antes de que la petición llegue a `ErrorHandlingMiddleware`, así que el middleware no puede darle forma. El limitador escribe su propio `ProblemDetails` en `OnRejected`, con el mismo aspecto que el resto.

## Risks / Trade-offs

**Un cupo demasiado estrecho echa a gente legítima** → Es el riesgo real de este cambio, más que el ataque que previene. Mitigación: cupos holgados para el uso humano, y el de examen más ancho que el de login.

**Varias instancias multiplican el límite efectivo** → Con dos instancias, el cupo real es el doble. Sigue siendo mucho mejor que ninguno. Un limitador compartido pide almacén externo, y eso es otra decisión.

**Una oficina entera comparte dirección de salida** → Varios candidatos de la misma empresa cuentan como uno. Por eso el cupo de `validate` es ancho.

**El límite no protege de un origen distribuido** → Quien disponga de muchas direcciones lo sortea. Contra eso hace falta algo delante de la aplicación, no dentro.

## Migration Plan

Sin cambios de esquema. Sin script SQL. Despliegue normal.

**Vuelta atrás**: quitar las dos líneas que registran y activan el limitador.
