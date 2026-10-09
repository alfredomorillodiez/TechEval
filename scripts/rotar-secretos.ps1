<#
.SYNOPSIS
    Genera valores nuevos para los secretos del despliegue y los escribe en .env

.DESCRIPTION
    Este guion NO rota nada por sí solo. Genera los valores y deja el fichero listo.
    Cambiarlos en los sistemas de destino son pasos manuales: están en
    docs/rotacion-de-secretos.md y hay que darlos por obligatorios.

    Los valores se generan en esta máquina con el generador criptográfico del sistema.
    El guion no los imprime ni los envía a ninguna parte. No hace falta verlos: van al
    .env, que es de donde los lee Docker Compose.

    Del .env anterior conserva lo que NO es un secreto y no cambia al rotar: la edición de SQL
    Server (MSSQL_PID), las dos URL (FRONTEND_BASE_URL y API_BASE_URL), y el tenant, el cliente y
    el remitente del correo. Sin eso, el .env nuevo no bastaba para levantar el stack. Los cuatro
    secretos NUNCA se copian del anterior.

    Después de rotar, scripts/comprobar-rotacion.ps1 dice si algún valor en uso sigue en el
    historial de git.

.PARAMETER Ruta
    Directorio donde escribir el .env. Por defecto, la raíz del repositorio.

.EXAMPLE
    pwsh scripts/rotar-secretos.ps1
#>
[CmdletBinding()]
param(
    [string]$Ruta = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

function New-ClaveAleatoria {
    param([int]$Bytes)
    $b = [byte[]]::new($Bytes)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($b)
    return [Convert]::ToBase64String($b)
}

function New-ContrasenaSql {
    # SQL Server exige mayúsculas, minúsculas y dígitos o símbolos. Base64 puede salir sin
    # alguno de los tres, así que se garantiza con un sufijo fijo y suficiente entropía antes.
    return (New-ClaveAleatoria -Bytes 24).Replace('/', '_').Replace('+', '-') + 'aA1!'
}

$envPath = Join-Path $Ruta '.env'

# Lo que se conserva del .env anterior: nada de esto es un secreto.
$previo = @{}

# Un .env en uso perdido deja el despliegue sin arrancar y sin forma de recuperar lo que
# tenía. Copia antes de escribir, siempre.
if (Test-Path $envPath) {
    $copia = "$envPath.$(Get-Date -Format 'yyyyMMdd-HHmmss').bak"
    Copy-Item $envPath $copia
    Write-Host "Se ha guardado el .env anterior en $(Split-Path -Leaf $copia)"

    foreach ($linea in Get-Content $envPath) {
        if ($linea -match '^\s*([A-Za-z0-9_]+)\s*=\s*(.*?)\s*$') {
            $previo[$Matches[1]] = $Matches[2]
        }
    }
}

function Get-Conservado {
    param([string]$Nombre, [string]$Defecto)
    if ($previo.ContainsKey($Nombre) -and $previo[$Nombre] -ne '') { return $previo[$Nombre] }
    return $Defecto
}

$mssqlPid = Get-Conservado 'MSSQL_PID' ''
$frontend = Get-Conservado 'FRONTEND_BASE_URL' 'http://localhost:5001'
$api = Get-Conservado 'API_BASE_URL' 'http://localhost:5000/'
$tenant = Get-Conservado 'O365_TENANT_ID' ''
$cliente = Get-Conservado 'O365_CLIENT_ID' ''
$remitente = Get-Conservado 'EMAIL_FROM' ''

$contenido = @"
# Generado por scripts/rotar-secretos.ps1 el $(Get-Date -Format 'yyyy-MM-dd HH:mm').
# NO subir al repositorio. .gitignore ya lo cubre.
#
# Rotar estos valores aquí NO basta. Ver docs/rotacion-de-secretos.md para los pasos
# que hay que dar en SQL Server, en el proveedor de correo y con la cuenta de
# administrador. Sin ellos, los valores antiguos siguen siendo válidos.
#
# Los secretos de abajo son nuevos. Lo demás se ha copiado del .env anterior.

# Edición de SQL Server: Developer (solo desarrollo), Express, Standard o Enterprise.
MSSQL_PID=$mssqlPid

SA_PASSWORD=$(New-ContrasenaSql)
JWT_SECRET_KEY=$(New-ClaveAleatoria -Bytes 48)
ADMIN_PASSWORD=$(New-ContrasenaSql)

# La web y la API, vistas desde el navegador. Sin barra final en FRONTEND_BASE_URL.
FRONTEND_BASE_URL=$frontend
API_BASE_URL=$api

# El secreto de la aplicación de correo NO se puede generar aquí: lo emite Entra ID.
# Emite uno nuevo en el registro de la aplicación, revoca el anterior y pégalo.
O365_TENANT_ID=$tenant
O365_CLIENT_ID=$cliente
O365_CLIENT_SECRET=
EMAIL_FROM=$remitente
"@

Set-Content -Path $envPath -Value $contenido -Encoding utf8

Write-Host ""
Write-Host "Escrito $envPath con valores nuevos."
Write-Host "No se muestran por pantalla a proposito."
Write-Host ""
Write-Host "FALTA lo que este guion no puede hacer:"
Write-Host "  1. Cambiar la contrasena de 'sa' en el SQL Server de destino."
Write-Host "  2. Revocar en Entra ID el secreto de cliente anterior de la aplicación de correo y pegar el nuevo en O365_CLIENT_SECRET."
Write-Host "  3. Reasignar la contrasena del administrador ya creado."
Write-Host "  4. Cambiar la contrasena del buzon cuyas credenciales SMTP estuvieron en git."

$faltan = @()
if ($mssqlPid -eq '') { $faltan += 'MSSQL_PID (Developer solo para desarrollo; Express, Standard o Enterprise en produccion)' }
if ($tenant -eq '') { $faltan += 'O365_TENANT_ID' }
if ($cliente -eq '') { $faltan += 'O365_CLIENT_ID' }
if ($remitente -eq '') { $faltan += 'EMAIL_FROM' }
if ($faltan.Count -gt 0) {
    Write-Host ""
    Write-Host "El .env anterior no las tenia y hay que rellenarlas a mano:"
    foreach ($f in $faltan) { Write-Host "  - $f" }
}

Write-Host ""
Write-Host "AVISO: la clave JWT nueva cierra todas las sesiones abiertas, tambien las"
Write-Host "invitaciones de examen en curso. Elige una hora sin examenes."
Write-Host ""
Write-Host "Cuando termines, comprueba que ningun valor en uso sigue en el historial:"
Write-Host "  pwsh scripts/comprobar-rotacion.ps1"
Write-Host ""
Write-Host "Pasos completos: docs/rotacion-de-secretos.md"
