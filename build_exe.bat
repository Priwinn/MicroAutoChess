@echo off
REM Build a single-file exe for visualize_combat.py using the project's .venv
IF NOT EXIST ".venv\Scripts\python.exe" (
  echo Virtualenv python not found at .venv\Scripts\python.exe
  echo Activate or create a venv named .venv and rerun this script.
  exit /b 1
)

.venv\Scripts\python.exe -m pip install --upgrade pip
.venv\Scripts\python.exe -m pip install pyinstaller

IF EXIST build rmdir /s /q build
IF EXIST dist rmdir /s /q dist

.venv\Scripts\python.exe -m PyInstaller --noconsole --onefile --name visualize_combat --distpath dist --workpath build --specpath build src\core\visualize_combat.py
IF %ERRORLEVEL% NEQ 0 (
  echo PyInstaller failed with exit code %ERRORLEVEL%
  exit /b %ERRORLEVEL%
)

echo Build finished. Output: dist\visualize_combat.exe
