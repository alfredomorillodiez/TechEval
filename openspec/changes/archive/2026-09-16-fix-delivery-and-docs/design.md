## Context

Ver `proposal.md` — Why para la motivación.

Lo que condiciona el enfoque:

- La imagen de SQL Server 2022 trae `/opt/mssql-tools18`, no `/opt/mssql-tools`. La versión 18 de `sqlcmd` exige además `-C` o `-N` para el certificado del servidor.
- El `integrity` de Bootstrap que hay en `index.html` comparte los 22 primeros caracteres con el del contenido real. Dos contenidos distintos dan resúmenes distintos desde el primer carácter.
- `documentacion.md` son 57 KB. La retirada de la IA ya se hizo en el `README.md`, no aquí.
- Yo no puedo rotar credenciales: exige emitir valores reales y tocar los sistemas de destino.

## Goals / Non-Goals

**Goals:**

- Que `docker compose up` levante el stack entero.
- Que Bootstrap cargue.
- Que la documentación no describa funciones retiradas.
- Que la rotación sea un paso mecánico para quien tenga los accesos.

**Non-Goals:**

- No se sirve Bootstrap desde `wwwroot`. Ver la decisión.
- No se reescribe `documentacion.md` entera: solo se retira lo que describe la IA.
- No se rota ninguna credencial.

## Decisions

### El `integrity` se corrige, no se sirve Bootstrap en local

Que el resumen declarado y el real compartan los 22 primeros caracteres descarta que el CDN sirva otro contenido: apunta a una cadena corrompida al escribirla. Lo confirma que el CSS, con su propio `integrity` y del mismo CDN, sí carga.

Se corrige la cadena con el valor del contenido servido, comprobado en el navegador.

**Alternativa descartada por ahora**: servir Bootstrap desde `wwwroot`. Es más sólido —elimina la dependencia del CDN y la pregunta del resumen— y es lo que recomendaba la auditoría. Exige descargar los ficheros y versionarlos, que es una decisión del dueño del repositorio, no mía. Queda ofrecida.

**Alternativa descartada**: quitar el `integrity`. Haría cargar el paquete hoy y dejaría la aplicación sin ninguna defensa si el CDN cambiara mañana.

### El guion de rotación no ve los valores que genera

Los genera con el generador criptográfico de la plataforma y los escribe directamente al fichero. No los imprime. Quien lo ejecuta no los ve tampoco, y no le hace falta: van al `.env`, que es donde los lee Compose.

**Sobre el fichero previo**: si ya hay un `.env`, el guion guarda una copia con marca de tiempo antes de escribir. Perder un `.env` en uso deja un despliegue sin poder arrancar y sin forma de recuperar lo que tenía.

### La lista de la rotación vive en el repositorio, no en un cambio archivado

Estaba en `openspec/changes/secrets-out-of-the-repo/README.md`, que al archivarse pasó a `openspec/changes/archive/…`. Un procedimiento operativo no debería vivir en el historial de una propuesta. Pasa a `docs/rotacion-de-secretos.md`, y el `README.md` apunta ahí.

### `documentacion.md` pierde la sección de IA, no se reescribe

Se retiran la sección 8, las entradas de configuración de `Ollama`, el paso de levantar el modelo, las tablas de trabajos de generación y las preguntas frecuentes sobre el modelo local. El resto del documento se queda como está.

## Risks / Trade-offs

**El `integrity` corregido depende de lo que sirve el CDN hoy** → Si jsDelivr cambiara el contenido de una versión fija, volvería a bloquearse. Es lo que SRI debe hacer. Mitigación real: servir el fichero en local, que queda ofrecido.

**Arreglar el `healthcheck` cambia lo que hace `docker compose up`** → Donde antes se colgaba, ahora arranca la API. Si alguien dependía de ese comportamiento para levantar solo la base de datos, ahora tiene que pedirlo por nombre.

**Bootstrap vuelve a cargar tras meses sin hacerlo** → Las pantallas llevan tiempo comportándose sin su JavaScript. Es posible que algo cambie de aspecto. Conviene mirar las que usen desplegables o ventanas modales.

**Quitar contenido de la documentación puede dejar huecos** → Referencias cruzadas a la sección 8 desde otros apartados. Se revisan las que queden.

## Migration Plan

Sin cambios de esquema. Sin cambios de API.

1. Desplegar como siempre.
2. La rotación es aparte, y la ejecuta una persona con acceso a los sistemas de destino.

**Vuelta atrás**: revertir el commit. El `healthcheck` vuelve a fallar y Bootstrap a bloquearse, que es el estado anterior.
