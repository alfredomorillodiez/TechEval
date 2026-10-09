<#
.SYNOPSIS
    Comprueba que los secretos del .env no son los que estuvieron en el historial de git.

.DESCRIPTION
    Rotar un secreto es sustituirlo por otro. La única prueba objetiva de que se hizo es que el
    valor que está en uso NO aparece en ningún commit: un despliegue con los valores viejos
    funciona igual de bien que uno con los nuevos.

    Para SA_PASSWORD, JWT_SECRET_KEY, ADMIN_PASSWORD y O365_CLIENT_SECRET, busca el valor del .env
    en todo el historial (git log --all -S). Falla si:
      - alguno aparece en cualquier commit;
      - falta alguno, o es demasiado corto para comprobarlo con fiabilidad (menos de 8 caracteres);
      - .env o appsettings.Local.json están rastreados por git;
      - el repositorio es una copia superficial: un historial incompleto daría un falso «todo bien».

    NO imprime ningún valor, solo el nombre de la variable y el commit donde apareció. Pero git
    recibe el valor como argumento, así que durante la búsqueda es visible para los procesos de
    esta máquina. Ejecútalo en una máquina de confianza.

    No comprueba lo que no está en git: que la contraseña de 'sa' del servidor sea la del .env, que
    el secreto de cliente anterior esté revocado en Entra ID, o que la cuenta de administrador ya
    creada tenga la contraseña nueva. Eso son los pasos manuales de docs/rotacion-de-secretos.md.

    Código de salida: 0 si todo está bien; 1 si hay algo que corregir.

.PARAMETER Ruta
    Raíz del repositorio. Por defecto, la de este guion.

.EXAMPLE
    pwsh scripts/comprobar-rotacion.ps1
#>
[CmdletBinding()]
param(
    [string]$Ruta = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$LongitudMinima = 8
$Secretos = 'SA_PASSWORD', 'JWT_SECRET_KEY', 'ADMIN_PASSWORD', 'O365_CLIENT_SECRET'
$Ficheros = '.env', 'src/TechEval.API/appsettings.Local.json'

Set-Location $Ruta

$problemas = [System.Collections.Generic.List[string]]::new()
$buenos = [System.Collections.Generic.List[string]]::new()

& git rev-parse --git-dir *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Host "No es un repositorio de git: $Ruta" -ForegroundColor Red
    exit 1
}

# Un clon con --depth no trae el historial entero: un valor antiguo podría no aparecer y la
# comprobación diría que todo va bien.
if ((& git rev-parse --is-shallow-repository) -eq 'true') {
    $problemas.Add("el repositorio es una copia superficial: el historial esta incompleto y la comprobacion no es fiable. Ejecuta 'git fetch --unshallow'.")
}

foreach ($f in $Ficheros) {
    & git ls-files --error-unmatch $f *> $null
    if ($LASTEXITCODE -eq 0) { $problemas.Add("$f esta rastreado por git: sacalo con 'git rm --cached $f'.") }
}

$envPath = Join-Path $Ruta '.env'
if (-not (Test-Path $envPath)) {
    Write-Host "No existe $envPath. Genera uno con scripts/rotar-secretos.ps1." -ForegroundColor Red
    exit 1
}

# Lee el .env sin imprimir nada. Quita las comillas que alguien haya puesto alrededor del valor.
$valores = @{}
foreach ($linea in Get-Content $envPath) {
    if ($linea -match '^\s*([A-Za-z0-9_]+)\s*=\s*(.*?)\s*$') {
        $v = $Matches[2]
        if ($v.Length -ge 2 -and (($v[0] -eq '"' -and $v[-1] -eq '"') -or ($v[0] -eq "'" -and $v[-1] -eq "'"))) {
            $v = $v.Substring(1, $v.Length - 2)
        }
        $valores[$Matches[1]] = $v
    }
}

foreach ($nombre in $Secretos) {
    $valor = if ($valores.ContainsKey($nombre)) { $valores[$nombre] } else { '' }

    if ($valor -eq '') { $problemas.Add("$nombre falta o esta vacio en el .env."); continue }
    if ($valor.Length -lt $LongitudMinima) {
        $problemas.Add("$nombre es demasiado corto para comprobarlo (menos de $LongitudMinima caracteres): buscaria una cadena trivial.")
        continue
    }

    # -S busca los commits en los que cambia el numero de veces que aparece la cadena, es decir,
    # los que la añadieron o la quitaron. Cadena literal, no expresion regular.
    $commit = & git log --all --format=%h --max-count=1 "-S$valor" 2>$null
    if ($LASTEXITCODE -ne 0) { $problemas.Add("${nombre}: git no pudo buscar en el historial."); continue }

    if ($commit) { $problemas.Add("${nombre}: su valor aparece en el historial de git (commit $commit). No esta rotado.") }
    else { $buenos.Add($nombre) }
}

Write-Host ""
foreach ($b in $buenos) { Write-Host "  [ok]    $b no aparece en el historial" -ForegroundColor Green }
foreach ($p in $problemas) { Write-Host "  [FALLO] $p" -ForegroundColor Red }
Write-Host ""

if ($problemas.Count -gt 0) {
    Write-Host "La rotacion NO esta completa. Pasos: docs/rotacion-de-secretos.md" -ForegroundColor Red
    exit 1
}

Write-Host "Los $($Secretos.Count) secretos del .env son distintos de todo lo que hay en el historial." -ForegroundColor Green
Write-Host "Falta lo que esta comprobacion no ve: 'sa' en el servidor, Entra ID y el administrador ya creado."
exit 0
