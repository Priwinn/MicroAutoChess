# MicroAutoChess

## About
MicroAutoChess is a lightweight research environment and proof-of-concept for auto-battler style games, inspired by Teamfight Tactics and MicroRTS. The project aims to provide a clean-room, minimal implementation for research and experiments in game AI, strategy, and automated playtesting. 

## Current State
Currently, the environment is set up as a standalone PvE game. The player is given a certain budget to buy units and beat 10 different levels to reach the MOST MAGNIFICENT victory screen.


## Features
- Simple, seeded combat simulation suitable for reproducible experiments
- Configurable unit definitions, levels, and spells.
- Visualizer for playing and inspecting combat

## Getting Started (running the game)
If you are running windows you can access an .exe in [releases](https://github.com/Priwinn/MicroAutoChess/releases) or in the [itch page](https://priwinn.itch.io/microautochess)

## Getting Started (dev)
These instructions get you a local development environment running quickly.

### Prerequisites
- Python 3.8+ (3.10 recommended)

### Quickstart
1. Clone the repository:

   git clone <repo-url>
   cd MicroAutoChess

2. Create and activate a virtual environment:

   python -m venv .venv

   PowerShell

   .\.venv\Scripts\Activate.ps1

   cmd

   .\.venv\Scripts\activate.bat

    If you use the provided `build` scripts they expect the venv to be at `.venv`.

3. Install runtime deps:

   pip install -r requirements.txt

4. Run the visualizer:

   python src\core\visualize_combat.py

### Running headless
Headless mode has not been maintained, current intended behaviour is the one in `visualize_combat.py`. The headless mode is available by running `combat_test.py`, while it runs succesfully, behaviour has not been checked to match the one in `visualize_combat.py`. Specifically, sorting units by position ensures reproducibility in `visualize_combat.py`.


## Build (Windows)
The repository includes `build\build_exe.ps1` which packages the visualizer into a single-file Windows executable using PyInstaller. The script expects a virtualenv at `.venv`.

PowerShell:

   .\build\build_exe.ps1

## Future Roadmap

Game Mechanics

- Implement the meta game: random shop, refresh, level up, PvP.
- Other core mechanics: star up, items, traits.

Game AI
- Clearer separation between visualisation and game engine.
- Expose a minimal API for external agents to query game state and submit actions (for RL and search agents).
- Create a forward model.
- Implement parameter config files to study meta shifts and balance tuning (board types, sizes, numeric values, etc.)
- Integrate with RL libraries (Stable Baselines, RLlib) and provide baseline agents.
- Save and load replay files.

## Contributing
Contributions are welcome. Please:
1. Open an issue describing the feature or bug.
2. Create a branch with a descriptive name and include tests where appropriate.
3. Submit a pull request with a clear description of the change.

## License
This project is provided under the MIT License — see the `LICENSE` file for details.

## Acknowledgements
- This project would not be possible without the Hexagonal Grid Guide by Red Blob Games https://www.redblobgames.com/grids/hexagons/
