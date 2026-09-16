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

# Un .env en uso perdido deja el despliegue sin arrancar y sin forma de recuperar lo que
# tenía. Copia antes de escribir, siempre.
if (Test-Path $envPath) {
    $copia = "$envPath.$(Get-Date -Format 'yyyyMMdd-HHmmss').bak"
    Copy-Item $envPath $copia
    Write-Host "Se ha guardado el .env anterior en $(Split-Path -Leaf $copia)"
}

$contenido = @"
# Generado por scripts/rotar-secretos.ps1 el $(Get-Date -Format 'yyyy-MM-dd HH:mm').
# NO subir al repositorio. .gitignore ya lo cubre.
#
# Rotar estos valores aquí NO basta. Ver docs/rotacion-de-secretos.md para los pasos
# que hay que dar en SQL Server, en el proveedor de correo y con la cuenta de
# administrador. Sin ellos, los valores antiguos siguen siendo válidos.

SA_PASSWORD=$(New-ContrasenaSql)
JWT_SECRET_KEY=$(New-ClaveAleatoria -Bytes 48)
ADMIN_PASSWORD=$(New-ContrasenaSql)

# La clave del proveedor de correo NO se puede generar aquí: la emite el proveedor.
# Revoca la anterior en su panel, emite una nueva y pégala.
SENDGRID_API_KEY=
EMAIL_FROM=
"@

Set-Content -Path $envPath -Value $contenido -Encoding utf8

Write-Host ""
Write-Host "Escrito $envPath con valores nuevos."
Write-Host "No se muestran por pantalla a proposito."
Write-Host ""
Write-Host "FALTA lo que este guion no puede hacer:"
Write-Host "  1. Cambiar la contrasena de 'sa' en el SQL Server de destino."
Write-Host "  2. Revocar la clave de correo anterior en el panel del proveedor."
Write-Host "  3. Reasignar la contrasena del administrador ya creado."
Write-Host "  4. Rellenar SENDGRID_API_KEY y EMAIL_FROM."
Write-Host ""
Write-Host "AVISO: la clave JWT nueva cierra todas las sesiones abiertas, tambien las"
Write-Host "invitaciones de examen en curso. Elige una hora sin examenes."
Write-Host ""
Write-Host "Pasos completos: docs/rotacion-de-secretos.md"
