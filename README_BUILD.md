# Building an EXE for visualize_combat

This project uses a local virtual environment located at `.venv`. The included
scripts will install PyInstaller into that venv (if needed) and build a
single-file executable for `src/core/visualize_combat.py`.

PowerShell (recommended):

```powershell
.uild_exe.ps1
```

Windows cmd:

```bat
build_exe.bat
```

Notes:
- The build scripts expect a venv python at `.venv\Scripts\python.exe`.
- The PowerShell script runs `pip install pyinstaller` in the venv if missing.
- The produced binary will be in `dist\visualize_combat.exe` on success.
- If your game uses external assets (images, sounds), you may need to pass
  `--add-data` options to PyInstaller; edit `build_exe.ps1` to add them.
