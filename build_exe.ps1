<#
Build script for creating a single-file executable of `visualize_combat.py`
using the project's virtual environment and PyInstaller.

Run from the workspace root in PowerShell:
  .\build_exe.ps1

This script will: install PyInstaller into the venv if needed, clean previous
builds, and run PyInstaller producing `dist\visualize_combat.exe`.
#>

$venv_python = Join-Path $PSScriptRoot ".venv\Scripts\python.exe"
if (-not (Test-Path $venv_python)) {
    Write-Host "Virtualenv python not found at $venv_python. Activate your venv or create one named .venv." -ForegroundColor Yellow
    exit 1
}

# Ensure pip and PyInstaller are available in the venv
& $venv_python -m pip install --upgrade pip
& $venv_python -m pip install pyinstaller

# Remove previous build artifacts (ignore errors)
Try { Remove-Item -Recurse -Force .\build, .\dist -ErrorAction SilentlyContinue } Catch {}

# Build: onefile exe, windowed (no console). Remove --noconsole if you want the console.
$entry = Join-Path $PSScriptRoot "src\core\visualize_combat.py"
& $venv_python -m PyInstaller --noconsole --onefile --name MicroAutoChess --distpath .\dist --workpath .\build --specpath .\build $entry
if ($LASTEXITCODE -ne 0) {
    Write-Error "PyInstaller failed (exit code $LASTEXITCODE)"
    exit $LASTEXITCODE
}

Write-Host "Build finished. Output: .\dist\MicroAutoChess.exe" -ForegroundColor Green
