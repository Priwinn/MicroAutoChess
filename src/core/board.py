"""
Game board representation and combat mechanics.
"""

import math
import numpy as np
from typing import List, Optional, Tuple, Dict
from dataclasses import dataclass
from enum import Enum
from queue import PriorityQueue
from player import Player
from player import Player
from units import Unit, UnitType
from math_utils import float_less_than_or_equal

import pathfinding_ext as _pathfinding_ext
# _pathfinding_ext = None



class CellType(Enum):
    EMPTY = 0
    PLANNED = 1 # A unit intends to move here but is in transit
    UNIT = 2
    OBSTACLE = 3


@dataclass
class BoardCell:
    """Represents a single cell on the game board."""
    position: Tuple[int, int]
    # DO NOT USE THIS TO CHECK IF CELL IS OCCUPIED
    # WHEN CHECK FOR AOE DAMAGE, CHECK IF A UNIT IS BEING HURT TWICE
    unit: Optional[Unit] = None     
    cell_type: CellType = CellType.EMPTY
    
    def is_empty(self) -> bool:
        return self.unit is None and self.cell_type == CellType.EMPTY
    
    def is_planned(self) -> bool:
        return self.cell_type == CellType.PLANNED
    
    def is_occupied(self) -> bool:
        return self.cell_type == CellType.UNIT
    
    
    def place_unit(self, unit: Unit):
        """Place a unit on this cell."""
        if not (self.is_empty() or self.is_planned()):
            raise ValueError(f"Cell {self.position} is not empty or planned")
        self.unit = unit
        self.cell_type = CellType.UNIT
        unit.position = self.position
    
    def remove_unit(self) -> Unit:
        """Remove unit from this cell."""
        unit = self.unit
        self.unit = None
        self.cell_type = CellType.EMPTY
        if unit:
        #     unit.position = None
            return unit
        else:
            raise ValueError(f"Tried to remove unit from cell {self.position}")
    
    def set_planned(self):
        """Set cell as planned for unit movement."""
        if not self.is_empty():
            raise ValueError(f"Cell {self.position} is not empty")
        self.cell_type = CellType.PLANNED


class Board:
    """
    Game board for auto chess combat.
    Manages unit positions and combat mechanics.
    """
    
    def __init__(self, size: Tuple[int, int] = (7, 8)):
        self.size = size
        self.bench_size = 9
        self.width, self.height = size
        self.cells: Dict[Tuple[int, int], BoardCell] = {}
        #This is an adhoc variable to make it more faithful to the actual game. Why is it like this? Range is definitely not plain l1 or l2 distance, but some weird hybrid.
        #It might be edge to edge unit model distance, when melee units are big enough they sometimes reach further than their center to center l2 range would suggest,
        #they keep moving to melee range though.
        self.range_offset = 0.0 

        self.player1: Player = Player(player_id=1)
        self.player2: Player = Player(player_id=2)
        # Cache for C++ pathfinding occupied grid
        self._occupied_cache = None
        self._occupied_cache_dirty = True
        # Initialize board cells
        for x in range(self.width):
            for y in range(self.height):
                self.cells[(x, y)] = BoardCell((x, y))

    def is_valid_position(self, position: Tuple[int, int]) -> bool:
        """Check if position is within board bounds."""
        x, y = position
        return 0 <= x < self.width and 0 <= y < self.height
    
    def is_valid_initial_position(self, position: Tuple[int, int], team: int) -> bool:
        """Check if position is valid for initial unit placement based on team."""
        x, y = position
        if not self.is_valid_position(position):
            return False
        if team == 1:
            return y < self.height // 2  # Team 1 on top half
        elif team == 2:
            return y >= self.height // 2  # Team 2 on bottom half
        return False
    
    def _invalidate_occupied_cache(self):
        """Mark the occupied grid cache as dirty."""
        self._occupied_cache_dirty = True
    
    def _rebuild_occupied_cache(self):
        """Rebuild the occupied grid cache for C++ pathfinding."""
        self._occupied_cache = [0] * (self.width * self.height)
        for (x, y), cell in self.cells.items():
            if cell.unit is not None:
                self._occupied_cache[y * self.width + x] = 1
        self._occupied_cache_dirty = False

    def place_board_unit(self, unit: Unit, position: Tuple[int, int], ) -> bool:
        """Place unit at position in board."""
        
        cell = self.cells[position]
        if not cell or not cell.is_empty():
            return False
        
        cell.place_unit(unit)
        # maintain player's units_on_board mapping if unit has team
        team = getattr(unit, 'team', None)
        if team == 1:
            self.player1.units_on_board[position] = unit
        elif team == 2:
            self.player2.units_on_board[position] = unit
        self._invalidate_occupied_cache()
        return True
    
    def place_unit(self, unit: Unit, position: Tuple[int, int]) -> bool:
        """Place unit at position in board or bench."""
        if position[0] < 0:
            return self.add_bench_unit(team=-position[0], unit=unit, bench_index=position[1])
        
        cell = self.cells[position]
        if not cell or not cell.is_empty():
            return False
        cell.place_unit(unit)
        self._invalidate_occupied_cache()
        return True
    
    def move_unit(self, from_pos: Tuple[int, int], to_pos: Tuple[int, int]) -> bool:
        """Move unit from one position to another."""

        from_cell = self.cells[from_pos]
        to_cell = self.cells[to_pos]
        
        if not from_cell or not to_cell:
            return False
        
        if from_cell.unit is None:
            return False
        
        from_cell.unit.planned_position = None
        if (to_cell.is_planned() and to_cell.unit != from_cell.unit) or to_cell.is_occupied():
            return False
        
        unit = from_cell.remove_unit()
        if unit is None:
            return False
        to_cell.place_unit(unit)
        self._invalidate_occupied_cache()
        # update player's units_on_board mapping keys. We don't need to sync this unless we want in combat item slams
        # team = getattr(unit, 'team', None)
        # if team == 1:
        #     # remove old key if present and set new key
        #     try:
        #         if from_pos in self.player1.units_on_board:
        #             del self.player1.units_on_board[from_pos]
        #     except Exception:
        #         pass
        #     self.player1.units_on_board[to_pos] = unit
        # elif team == 2:
        #     try:
        #         if from_pos in self.player2.units_on_board:
        #             del self.player2.units_on_board[from_pos]
        #     except Exception:
        #         pass
        #     self.player2.units_on_board[to_pos] = unit
        return True
    
    def get_units(self) -> List[Unit]:
        """Get all units on the board."""
        units = []
        for cell in self.cells.values():
            if cell.unit:
                units.append(cell.unit)
        return units
    
    def get_units_by_team(self, team: int) -> List[Unit]:
        """Get all units belonging to a specific team."""
        return [unit for unit in self.get_units() if getattr(unit, 'team', 0) == team]
    
    def get_bench_unit(self, team: int, bench_index: int) -> Optional[Unit]:
        """Get a unit from a specific bench position for a team."""
        if team == 1:
            return self.player1.bench.get(bench_index) if bench_index in self.player1.bench else None
        elif team == 2:
            return self.player2.bench.get(bench_index) if bench_index in self.player2.bench else None
        return None
    
    def remove_bench_unit(self, team: int, bench_index: int) -> Optional[Unit]:
        """Remove a unit from a specific bench position for a team."""
        if team == 1:
            unit = self.player1.bench.get(bench_index)
            if bench_index in self.player1.bench:
                self.player1.bench[bench_index] = None
            if unit and unit.position:
                self.player1.units_on_board[unit.position] = None
                unit.position = None
            return unit
        elif team == 2:
            unit = self.player2.bench.get(bench_index)
            if bench_index in self.player2.bench:
                self.player2.bench[bench_index] = None
            if unit and unit.position:
                self.player2.units_on_board[unit.position] = None
                unit.position = None
            return unit
        return None

    def add_bench_unit(self, team: int, unit: Unit, bench_index: int = -1) -> bool:
        """Add a unit to the specified bench index. If bench_index is -1, add to the first available bench position for a team."""
        if unit is None:
            return False
        if team == 1:
            bench = self.player1.bench
            size = len(bench)
            if bench_index == -1:
                for i in range(size):
                    if bench.get(i) is None:
                        unit.position = (-1, i)
                        bench[i] = unit
                        return True
            else:
                if 0 <= bench_index < size and bench.get(bench_index) is None:
                    unit.position = (-1, bench_index)
                    bench[bench_index] = unit
                    return True
        elif team == 2:
            bench = self.player2.bench
            size = len(bench)
            if bench_index == -1:
                for i in range(size):
                    if bench.get(i) is None:
                        unit.position = (-2, i)
                        bench[i] = unit
                        return True
            else:
                if 0 <= bench_index < size and bench.get(bench_index) is None:
                    unit.position = (-2, bench_index)
                    bench[bench_index] = unit
                    return True
        return False
        
    
    def player_move_unit(self, from_pos: Tuple[int, int], to_pos: Tuple[int, int], team: int) -> bool:
        """Player move unit function that can handle both board and bench movements.
        Bench to board moves, board to bench moves, and board to board moves are all handled in this function.
        This includes swaps."""
        #Sanity checks, -1 is team one and -2 is team two for bench positions. 
        if (from_pos[0] == -1 or to_pos[0] == -1) and team != 1:
            return False
        if (from_pos[0] == -2 or to_pos[0] == -2) and team != 2:
            return False

        valid_board_positions = self.get_initial_positions(team)

        if (from_pos[0] >= 0 and from_pos not in valid_board_positions) or (to_pos[0] >= 0 and to_pos not in valid_board_positions):    
            return False
        
        
        # Handle bench to board move
        if from_pos[0] < 0 and to_pos[0] >= 0:
            bench_unit = self.remove_bench_unit(team, from_pos[1])
            if bench_unit is None:
                return False
            to_unit= self.cells[to_pos].unit
            if to_unit is not None and to_unit.team == team:
                self.remove_unit(to_pos)
            if not self.place_board_unit(bench_unit, to_pos):
                return False
            
            if to_unit is not None and to_unit.team == team:
                self.add_bench_unit(team, to_unit, bench_index=from_pos[1])
            return True

        # Handle board to bench move
        if from_pos[0] >= 0 and to_pos[0] < 0:
            from_unit = self.cells[from_pos].unit
            if from_unit is None:
                return False
            self.remove_unit(from_pos)
            to_unit = self.get_bench_unit(team, to_pos[1])
            if to_unit is not None: 
                self.remove_bench_unit(team, to_pos[1])
                self.place_board_unit(to_unit, from_pos)
            return self.add_bench_unit(team, from_unit, bench_index=to_pos[1])

        # Handle board to board move
        if from_pos[0] >= 0 and to_pos[0] >= 0:
            to_pos_cell = self.cells[to_pos]
            from_pos_cell = self.cells[from_pos]
            to_pos_unit = to_pos_cell.unit
            from_pos_unit = from_pos_cell.unit
            if from_pos_cell.unit is None:
                return False
            if to_pos_unit is not None and to_pos_unit.team != team:
                return False
            if to_pos_unit is not None and to_pos_unit.team == team:
                to_pos_unit = to_pos_cell.remove_unit()
                from_pos_unit = from_pos_cell.remove_unit()
                to_pos_cell.place_unit(from_pos_unit)
                from_pos_cell.place_unit(to_pos_unit)
                self._invalidate_occupied_cache()
                return True
            else:
                return self.move_unit(from_pos, to_pos)
            
        #Handle bench to bench move
        if from_pos[0] < 0 and to_pos[0] < 0:
            from_bench_unit = self.get_bench_unit(team, from_pos[1])
            to_bench_unit = self.get_bench_unit(team, to_pos[1])
            if from_bench_unit is None:
                return False
            if to_bench_unit is not None and to_bench_unit.team != team:
                return False
            if to_bench_unit is not None and to_bench_unit.team == team:
                self.remove_bench_unit(team, from_pos[1])
                self.remove_bench_unit(team, to_pos[1])
                self.add_bench_unit(team, from_bench_unit, bench_index=to_pos[1])
                self.add_bench_unit(team, to_bench_unit, bench_index=from_pos[1])
                return True
            else:
                self.remove_bench_unit(team, from_pos[1])
                return self.add_bench_unit(team, from_bench_unit, bench_index=to_pos[1])

    def apply_player_units(self, player: Player):
        """Place all of a player's units on the board according to their position attribute."""
        # Set this board's player reference so bench accessors use the player's bench
        if player.player_id == 1:
            self.player1 = player
        elif player.player_id == 2:
            self.player2 = player

        # Clear any existing units on the board for this team
        for unit in list(self.get_units_by_team(player.player_id)):
            if unit:
                self.remove_unit(unit.position)

        # Place units according to their position attribute on the board. Bench slots live on the Player object.
        for unit in player.units_on_board.values():
            if unit:
                self.place_board_unit(unit, unit.position)
        # ensure board bench size matches player's bench
        self.bench_size = len(player.bench)


            

    @staticmethod
    def l1_distance(pos1: Tuple[int, int], pos2: Tuple[int, int]) -> float:
        """Calculate Manhattan distance between two positions."""
        raise NotImplementedError("This method should be implemented in subclasses based on board type")

    @staticmethod
    def l2_distance(pos1: Tuple[int, int], pos2: Tuple[int, int]) -> float:
        """Calculate Euclidean distance between two positions."""
        raise NotImplementedError("This method should be implemented in subclasses based on board type")
    
    def pathfind_distance(self, start: Tuple[int, int], target: Tuple[int, int]) -> float:
        """Calculate pathfinding distance between two positions using A*."""
        if _pathfinding_ext is not None:
            board_type = "hex"
            if isinstance(self, DiagonalSquareBoard):
                board_type = "diagonal"
            elif isinstance(self, SquareBoard):
                board_type = "square"
            # Use cached occupied grid if available
            if self._occupied_cache_dirty or self._occupied_cache is None:
                self._rebuild_occupied_cache()
            try:
                dist = _pathfinding_ext.pathfind_distance(
                    self.width,
                    self.height,
                    board_type,
                    self._occupied_cache,
                    start,
                    target,
                )
                if dist >= 0:
                    return float(dist)
                return float('inf')
            except Exception:
                pass
        path = self.find_path(start, target)
        if not path:
            return float('inf')
        return len(path) - 1  # Number of steps is path length minus 1
    
    def pathfind_distance_to_range(self, start: Tuple[int, int], target: Tuple[int, int], attack_range: float) -> float:
        """Calculate pathfinding distance to get within attack range of target."""
        if self.l2_distance(start, target) <= attack_range + attack_range*self.range_offset:
            return 0.0
        target_positions = self.get_positions_at_l2_distance(target, attack_range)

        target_positions = [pos for pos in target_positions if self.cells[pos].is_empty()]
        min_distance = float('inf')
        for pos in target_positions:
            distance = self.pathfind_distance(start, pos)
            if distance < min_distance:
                min_distance = distance
        return min_distance
    
    def get_adjacent_positions(self, position: Tuple[int, int]) -> List[Tuple[int, int]]:
        """Get valid adjacent positions."""
        raise NotImplementedError("This method should be implemented in subclasses based on board type")
    
    def get_adjacent_cells(self, position: Tuple[int, int]) -> List[BoardCell]:
        """Get adjacent cells for a given position."""
        """Returns a list of adjacent cell positions."""
        if not self.is_valid_position(position):
            raise ValueError(f"Position {position} is out of bounds")
        
        adjacent = self.get_adjacent_positions(position)
        return [self.cells[pos] for pos in adjacent if self.cells[pos] is not None]
    
    def get_cells_in_l1_range(self, position: Tuple[int, int], l1_range: int) -> List[BoardCell]:
        """Get all cells within a certain amount of steps from a position."""
        return [self.cells[pos] for pos in self.get_positions_in_l1_range(position, l1_range)]
    
    def get_positions_in_l1_range(self, position: Tuple[int, int], l1_range: int) -> List[Tuple[int, int]]:
        """Get all positions within a certain amount of steps from a position."""
        raise NotImplementedError("This method should be implemented in subclasses based on board type")
    
    def get_positions_in_l2_range(self, position: Tuple[int, int], l2_range: float) -> List[Tuple[int, int]]:
        """Get all positions within a certain Euclidean distance from a position."""
        raise NotImplementedError("This method should be implemented in subclasses based on board type")
    
    def get_positions_at_l2_distance(self, position: Tuple[int, int], l2_distance: float) -> List[Tuple[int, int]]:
        """Get all positions exactly at a certain Euclidean distance from a position."""
        raise NotImplementedError("This method should be implemented in subclasses based on board type")

    def get_initial_positions(self, team: int) -> List[Tuple[int, int]]:
        """Return a list of board positions considered valid initial placement for a team.

        By convention: split the board horizontally. Team 1 uses the upper half (smaller y),
        Team 2 uses the lower half (larger y).
        """
        mid = self.height // 2
        positions = []
        if team == 1:
            for x in range(self.width):
                for y in range(0, mid):
                    positions.append((x, y))
        else:
            for x in range(self.width):
                for y in range(mid, self.height):
                    positions.append((x, y))
        return positions
    
    def find_path(self, start: Tuple[int, int], target: Tuple[int, int]) -> List[Tuple[int, int]]:
        """Simple pathfinding using A* algorithm."""

        open_set = PriorityQueue()
        open_set.put((0, start))
        came_from = {start: None}
        g_score = {start: 0.0}
        f_score = {start: self.l1_distance(start, target)}

        while not open_set.empty():
            current = open_set.get()[1]

            if current == target:
                total_path = [current]
                while current in came_from and came_from[current] is not None:
                    current = came_from[current]
                    total_path.append(current)
                total_path.reverse()
                return total_path

            for neighbor in self.get_adjacent_positions(current):

                if not self.cells[neighbor].is_empty() and neighbor != target:
                    continue
                tentative_g_score = g_score[current] + 1  # Assume cost is 1

                if neighbor not in g_score or tentative_g_score < g_score[neighbor]:
                    came_from[neighbor] = current
                    g_score[neighbor] = tentative_g_score
                    f_score[neighbor] = tentative_g_score + self.l1_distance(neighbor, target)
                    if neighbor not in [i for i in open_set.queue]:
                        open_set.put((f_score[neighbor], neighbor))

        return []


    def find_path_guided(self, start: Tuple[int, int], target: Tuple[int, int]) -> List[Tuple[int, int]]:
        """Simple pathfinding using A* algorithm. Prefer horizontal movement when distances are equal and
          prefer moves that get closer to target according to l2 distance."""
        
        open_set = PriorityQueue()
        open_set.put((0, start))
        came_from = {start: None}
        g_score = {start: 0.0}
        f_score = {start: self.l1_distance(start, target)}

        while not open_set.empty():
            current = open_set.get()[1]

            if current == target:
                total_path = [current]
                while current in came_from and came_from[current] is not None:
                    current = came_from[current]
                    total_path.append(current)
                total_path.reverse()
                return total_path

            for neighbor in self.get_adjacent_positions(current):

                if not self.cells[neighbor].is_empty() and neighbor != target:
                    continue
                tentative_g_score = g_score[current] + 1  # Assume cost is 1
                #Prefer move that is closer to target according to l2
                dl2 = max(self.l2_distance(current, target) - self.l2_distance(neighbor, target), 0)
                tentative_g_score -= dl2/100

                if abs(neighbor[0] - current[0]) > abs(neighbor[1] - current[1]):
                    tentative_g_score -= 0.02  # Horizontal movement is preferred


                if neighbor not in g_score or tentative_g_score < g_score[neighbor]:
                    came_from[neighbor] = current
                    g_score[neighbor] = tentative_g_score
                    f_score[neighbor] = tentative_g_score + self.l1_distance(neighbor, target)
                    if neighbor not in [i for i in open_set.queue]:
                        open_set.put((f_score[neighbor], neighbor))

        return []

    def find_path_to_range_guided(self, start: Tuple[int, int], target: Tuple[int, int], attack_range: float) -> List[Tuple[int, int]]:
        """Find path to get within attack range of target using A* algorithm with guided movement."""
        target_positions = self.get_positions_in_l2_range(target, attack_range)
        target_positions = [pos for pos in target_positions if self.cells[pos].is_empty() or pos == start]
        #Sort target positions by vertical distance to start to prefer horizontal movement. Ties are broken by l2 distance to target to prefer moves that get closer to target.
        target_positions.sort(key=lambda pos: abs(pos[1]-start[1])+self.l2_distance(pos, target)/100)
        shortest_path = []
        min_length = float('inf')
        for pos in target_positions:
            path = self.find_path_guided(start, pos)
            if path and len(path) < min_length:
                min_length = len(path)
                shortest_path = path
        return shortest_path
    
    
    def to_array(self) -> np.ndarray:
        """Convert board to numerical array."""
        # Create 3D array: width x height x features
        array = np.zeros((self.width, self.height, 10), dtype=np.float32)
        
        for (x, y), cell in self.cells.items():
            if cell.unit:
                unit_array = cell.unit.to_array()
                array[x, y, :len(unit_array)] = unit_array[:10]  # Limit to 10 features
        
        return array

    def is_empty(self, position: Tuple[int, int]) -> bool:
        """Check if a cell is empty."""
        cell = self.cells[position]
        return cell.is_empty() if cell else False

    def remove_unit(self, position: Tuple[int, int]) -> Unit:
        """Remove unit from a specific position."""
        cell = self.cells[position]
        if cell and not cell.is_empty():
            unit = cell.remove_unit()
            self._invalidate_occupied_cache()
            return unit
        raise ValueError(f"Tried to remove unit from empty cell {position}")
    
    def set_planned(self, position: Tuple[int, int], unit: Unit):
        """Set a cell as planned for unit movement."""
        cell = self.cells[position]
        if cell and cell.is_empty():
            cell.set_planned()
            cell.unit = unit
            self._invalidate_occupied_cache()
        else:
            raise ValueError(f"Cell {position} is not empty or already planned")

    def reset_board(self):
        """Reset the board to empty state."""
        for cell in self.cells.values():
            cell.unit = None
            cell.cell_type = CellType.EMPTY
        self._invalidate_occupied_cache()

    def clone(self) -> 'Board':
        """Create a deep copy of the board."""
        new_board = Board(self.size)
        for position, cell in self.cells.items():
            if cell.unit:
                cloned_unit = cell.unit.clone()
                new_board.place_board_unit(cloned_unit, position)
        return new_board
    
    def print_board(self, title: str = ""):
        """Print current board state."""
        raise NotImplementedError("This method should be implemented in subclasses based on board type")
    
    def coord_to_pixel(self, position: Tuple[int, int], cell_radius: int = 50) -> Tuple[int, int]:
        """Convert board coordinates to pixel coordinates of cell center for visualization."""
        raise NotImplementedError("This method should be implemented in subclasses based on board type")
    
    def get_cell_corners(self, position: Tuple[int, int], cell_radius: int = 50) -> List[Tuple[int, int]]:
        """Get pixel coordinates of the corners of a cell for visualization. Input is cell center pixel position and cell radius in pixels."""
        raise NotImplementedError("This method should be implemented in subclasses based on board type")


class SquareBoard(Board):
    """Square board implementation."""

    def __init__(self, size: Tuple[int, int] = (7, 8)):
        super().__init__(size)
        self.range_offset = 0.25  
    
    @staticmethod
    def l1_distance(pos1: Tuple[int, int], pos2: Tuple[int, int]) -> float:
        """Calculate Manhattan distance between two positions."""
        return abs(pos1[0] - pos2[0]) + abs(pos1[1] - pos2[1])

    @staticmethod
    def l2_distance(pos1: Tuple[int, int], pos2: Tuple[int, int]) -> float:
        """Calculate Euclidean distance between two positions."""
        return np.sqrt((pos1[0] - pos2[0]) ** 2 + (pos1[1] - pos2[1]) ** 2)
    
    def get_adjacent_positions(self, position: Tuple[int, int]) -> List[Tuple[int, int]]:
        """Get valid adjacent positions."""
        x, y = position
        adjacent = [
            (x+1, y), (x-1, y), (x, y+1), (x, y-1),
            #(x+1, y+1), (x+1, y-1), (x-1, y+1), (x-1, y-1) # Uncomment for diagonal movement
        ]
        return [pos for pos in adjacent if self.is_valid_position(pos)]
    
    def get_adjacent_cells(self, position: Tuple[int, int]) -> List[BoardCell]:
        """Get adjacent cells for a given position."""
        """Returns a list of adjacent cell positions."""
        if not self.is_valid_position(position):
            raise ValueError(f"Position {position} is out of bounds")
        
        adjacent = self.get_adjacent_positions(position)
        return [self.cells[pos] for pos in adjacent if pos in self.cells]

    
    def get_positions_in_l1_range(self, position: Tuple[int, int], l1_range: int) -> List[Tuple[int, int]]:
        """Get all positions within a certain amount of steps from a position."""
        positions_in_range = []
        for dx in range(-l1_range, l1_range + 1):
            for dy in range(-l1_range + abs(dx), l1_range - abs(dx) + 1):
                new_pos = (position[0] + dx, position[1] + dy)
                if self.is_valid_position(new_pos):
                    positions_in_range.append(new_pos)
        return positions_in_range
    
    def get_positions_in_l2_range(self, position: Tuple[int, int], l2_range: float) -> List[Tuple[int, int]]:
        """Get all positions within a certain Euclidean distance from a position."""
        positions_in_range = []
        x0, y0 = position
        min_x = max(0, int(x0 - l2_range))
        max_x = min(self.width - 1, int(x0 + l2_range))
        min_y = max(0, int(y0 - l2_range))
        max_y = min(self.height - 1, int(y0 + l2_range))
        
        for x in range(min_x, max_x + 1):
            for y in range(min_y, max_y + 1):
                if float_less_than_or_equal(self.l2_distance(position, (x, y)), l2_range):
                    positions_in_range.append((x, y))
        
        return positions_in_range
    
    def get_positions_at_l2_distance(self, position: Tuple[int, int], l2_distance: float) -> List[Tuple[int, int]]:
        """Get all positions exactly at a certain Euclidean distance from a position."""
        positions_at_distance = []
        x0, y0 = position
        min_x = max(0, int(x0 - l2_distance))
        max_x = min(self.width - 1, int(x0 + l2_distance))
        min_y = max(0, int(y0 - l2_distance))
        max_y = min(self.height - 1, int(y0 + l2_distance))
        
        for x in range(min_x, max_x + 1):
            for y in range(min_y, max_y + 1):
                distance = self.l2_distance(position, (x, y))
                if l2_distance-distance < 1+1e-9 and distance - l2_distance < 1e-9:
                    positions_at_distance.append((x, y))
        
        return positions_at_distance

    
    def print_board(self, title: str = ""):
        """Print current board state."""
        if title:
            print(f"\n=== {title} ===")
        print("  ", end="")
        for x in range(self.width):
            print(f"{x:2}", end="")
        print()
        
        for y in range(self.height):
            print(f"{y} ", end="")
            for x in range(self.width):
                cell = self.cells[(x, y)]
                if cell and cell.unit:
                    # Display unit with team indicator
                    unit = cell.unit
                    symbol = unit._get_unit_symbol()
                    if hasattr(unit, 'team') and unit.team == 1:
                        symbol = symbol.upper()  # Team 1 uppercase
                    else:
                        symbol = symbol.lower()  # Team 2 lowercase
                    print(f"{symbol:>2}", end="")
                else:
                    print(" .", end="")
            print()
        print()

    def coord_to_pixel(self, position: Tuple[int, int], cell_radius: int = 50) -> Tuple[int, int]:
        """Convert board coordinates to pixel coordinates of cell center for visualization."""
        x, y = position
        pixel_x = x * cell_radius * math.sqrt(2) + cell_radius / math.sqrt(2)
        pixel_y = y * cell_radius * math.sqrt(2) + cell_radius / math.sqrt(2)
        return (int(pixel_x), int(pixel_y))
    
    def get_cell_corners(self, position: Tuple[int, int], cell_radius: int = 50) -> List[Tuple[int, int]]:
        """Get pixel coordinates of the corners of a cell for visualization. Input is cell center pixel position and cell radius in pixels."""
        x, y = position
        top_left = (int(x + cell_radius / math.sqrt(2)), int(y + cell_radius / math.sqrt(2)))
        top_right = (int(x - cell_radius / math.sqrt(2)), int(y + cell_radius / math.sqrt(2)))
        bottom_right = (int(x - cell_radius / math.sqrt(2)), int(y - cell_radius / math.sqrt(2)))
        bottom_left = (int(x + cell_radius / math.sqrt(2)), int(y - cell_radius / math.sqrt(2)))
        
        return [top_left, top_right, bottom_right, bottom_left]
    

    
class HexBoard(Board):
    """
    Hexagonal board implementation using offset coordinates.
    Uses odd-r offset coordinates where odd rows are shifted right.
    """
    
    def __init__(self, size: Tuple[int, int] = (7, 8)):
        super().__init__(size)
        self.range_offset = 1/6
        # Precalculate and cache pixel coordinates for all positions
        self._pixel_coords = {}
        for x in range(self.width):
            for y in range(self.height):
                q, r = oddr_to_axial((x, y))
                px = q + r / 2
                py = r * 3**0.5 / 2
                self._pixel_coords[(x, y)] = (px, py)
        
        # Precalculate and cache l2 distances for all position pairs
        self._l2_distance_cache = {}
        for x1 in range(self.width):
            for y1 in range(self.height):
                for x2 in range(self.width):
                    for y2 in range(self.height):
                        px1, py1 = self._pixel_coords[(x1, y1)]
                        px2, py2 = self._pixel_coords[(x2, y2)]
                        dist = np.sqrt((px1 - px2) ** 2 + (py1 - py2) ** 2)
                        # Cache with both orderings for lookup efficiency
                        self._l2_distance_cache[((x1, y1), (x2, y2))] = dist
    
    
    def get_adjacent_positions(self, position: Tuple[int, int]) -> List[Tuple[int, int]]:
        """Get valid adjacent positions for hexagonal grid using odd-r offset coordinates."""
        x, y = position
        
        # For odd-r offset coordinates with point-up orientation
        if y % 2 == 1:  # Odd row
            adjacent = [
            (x, y-1),     # Northwest
            (x+1, y-1),   # Northeast
            (x+1, y),     # East
            (x+1, y+1),   # Southeast
            (x, y+1),     # Southwest
            (x-1, y)      # West
            ]
        else:  # Even row
            adjacent = [
            (x-1, y-1),   # Northwest
            (x, y-1),     # Northeast
            (x+1, y),     # East
            (x, y+1),     # Southeast
            (x-1, y+1),   # Southwest
            (x-1, y)      # West
            ]
        
        return [pos for pos in adjacent if self.is_valid_position(pos)]
    
    @staticmethod
    def l1_distance(pos1: Tuple[int, int], pos2: Tuple[int, int]) -> float:
        """Calculate Manhattan distance for hexagonal grid using odd-r offset coordinates."""
        x1, y1 = pos1
        x2, y2 = pos2

        # Convert to axial coordinates for easier distance calculation
        q1 = x1 - (y1 - (y1 & 1)) // 2
        r1 = y1
        q2 = x2 - (y2 - (y2 & 1)) // 2
        r2 = y2

        # Calculate hex distance in axial coordinates
        return (abs(q1 - q2) + abs(q1 + r1 - q2 - r2) + abs(r1 - r2)) // 2

    def l2_distance(self, pos1: Tuple[int, int], pos2: Tuple[int, int]) -> float:
        """Calculate Euclidean distance for hexagonal grid using cached distances."""
        return self._l2_distance_cache[(pos1, pos2)]
    
    def get_cells_in_l1_range(self, position: Tuple[int, int], l1_range: int) -> List[BoardCell]:
        """Get all cells within a certain amount of steps from a position in hexagonal grid."""
        results = []
        (q0, r0) = oddr_to_axial(position)
        for q in range(-l1_range, l1_range + 1):
            for r in range(max(-l1_range, -q - l1_range), min(l1_range, -q + l1_range) + 1):
                x,y = axial_to_oddr((q0 + q, r0 + r))
                if self.is_valid_position((x, y)):
                    cell = self.cells[(x, y)]
                    if cell:
                        results.append(cell)
        return results
    
    def get_positions_in_l1_range(self, position: Tuple[int, int], l1_range: int) -> List[Tuple[int, int]]:
        """Get all positions within a certain amount of steps from a position in hexagonal grid."""
        results = []
        (q0, r0) = oddr_to_axial(position)
        for q in range(-l1_range, l1_range + 1):
            for r in range(max(-l1_range, -q - l1_range), min(l1_range, -q + l1_range) + 1):
                x,y = axial_to_oddr((q0 + q, r0 + r))
                if self.is_valid_position((x, y)):
                    results.append((x, y))
        return results
    
    def get_positions_in_l2_range(self, position, l2_range):
        """Get all positions within a certain Euclidean distance from a position in hexagonal grid."""
        results = []
        (q0, r0) = oddr_to_axial(position)
        max_range = int(l2_range * 2)  # Approximate max range in hex steps, this is a safe overestimate
        for q in range(-max_range, max_range + 1):
            for r in range(max(-max_range, -q - max_range), min(max_range, -q + max_range) + 1):
                x,y = axial_to_oddr((q0 + q, r0 + r))
                if self.is_valid_position((x, y)):
                    if float_less_than_or_equal(self.l2_distance(position, (x, y)), l2_range):
                        results.append((x, y))
        return results
    
    def get_positions_at_l2_distance(self, position, l2_distance):
        """Get all positions exactly at a certain Euclidean distance from a position in hexagonal grid."""
        results = []
        (q0, r0) = oddr_to_axial(position)
        max_range = int(l2_distance * 2)  # Approximate max range in hex steps, this is a safe overestimate
        for q in range(-max_range, max_range + 1):
            for r in range(max(-max_range, -q - max_range), min(max_range, -q + max_range) + 1):
                x,y = axial_to_oddr((q0 + q, r0 + r))
                if self.is_valid_position((x, y)):
                    distance = self.l2_distance(position, (x, y))
                    if l2_distance-distance < 1+1e-9 and distance - l2_distance < 1e-9:
                        results.append((x, y))
        return results
    

    def create_hex_cell(self, content=""):
        """
        Returns a list of 5 strings representing the ASCII art for a single hex cell.
        The cell is 6 characters wide and 5 lines high.
        The middle (third) line shows the content, centered in a field of width 4.
        """
        # Ensure the content fits in 6 characters.
        content_str = str(content)[:6]
        # Each cell's 5 lines:
        cell = [
            "  _ /  \\ _  ",  # line 0: top point
            " /        \\ ",  # line 1: upper sides
            f"|{content_str:^10}|",  # line 2: content line
            f"|{' ':^10}|",  # line 3: empty line for symmetry
            " \\ _    _ / ",  # line 4: lower sides
            "    \\  /    "   # line 5: bottom point
        ]
        return cell

    def print_board(self,title: str = ""):
        """
        Generates an ASCII drawing of an odd-r hex grid with point-up hexes.
        """
        if title:
            print(f"\n=== {title} ===")
        # Each hex cell's dimensions
        cell_width = 12   # each cell drawn is 8 characters wide
        cell_height = 6  # each cell drawn is 5 lines tall
        
        # In a point-up hex grid the vertical stacking overlaps.
        # We use a vertical offset of 3 lines per row.
        vert_offset = 4
        canvas_height = (self.height - 1) * vert_offset + cell_height
        # For horizontal extent, odd rows get shifted by half cell width (3 spaces).
        canvas_width = cell_width * self.width + 10  # extra for shifted rows

        # Create a blank canvas (list of lists of characters)
        canvas = [[" " for _ in range(canvas_width)] for _ in range(canvas_height)]
        


        # For each hex cell, compute its top-left position and overlay its ASCII art onto the canvas.
        for r in range(self.height):
            for c in range(self.width):
                # For odd rows (1-indexed odd, i.e. r % 2 == 1 in 0-indexing),
                # we indent by half the cell width (3 spaces)
                x_offset = (cell_width//2 if (r % 2 == 1) else 0) + c * cell_width
                y_offset = r * vert_offset

                cell = self.cells[(c, r)]
                if not cell or cell.is_empty() or cell.is_planned():
                    cell_content = f" "
                else:
                    cell_content = cell.unit._get_unit_symbol().upper() if cell.unit.team == 1 else cell.unit._get_unit_symbol().lower()
                cell_art = self.create_hex_cell(cell_content)

                # Overlay the cell art onto the canvas:
                for i in range(cell_height):
                    # Compute the canvas row index
                    canvas_y = y_offset + i
                    # Skip if outside the canvas (should not happen)
                    if canvas_y >= canvas_height:
                        continue
                    line = cell_art[i]
                    for j, char in enumerate(line):
                        canvas_x = x_offset + j
                        if canvas_x < canvas_width:
                            # Only non-space characters overwrite what's there.
                            if char != " ":
                                canvas[canvas_y][canvas_x] = char
        
        # Convert canvas to string lines
        ascii_art = "\n".join("".join(row) for row in canvas)
        print(ascii_art)
    
    def coord_to_pixel(self, position: Tuple[int, int], cell_radius: int = 50) -> Tuple[int, int]:
        """Convert board coordinates to pixel coordinates of cell center for visualization."""
        q,r = oddr_to_axial(position)
        x = int(cell_radius * math.sqrt(3) * (q + r / 2))
        y = int(cell_radius * 1.5 * r)
        return (x, y)
    
    def get_cell_corners(self, position: Tuple[int, int], cell_radius: int = 50) -> List[Tuple[int, int]]:
        """Get pixel coordinates of the corners of a cell for visualization. Input is cell pixel position and cell size in pixels."""
        corners = []
        center_x, center_y = position
        for i in range(6):
            angle_deg = 60 * i - 30  # Start at -30 degrees to have a point-up hex
            angle_rad = math.radians(angle_deg)
            corner_x = int(center_x + cell_radius * math.cos(angle_rad))
            corner_y = int(center_y + cell_radius * math.sin(angle_rad))
            corners.append((corner_x, corner_y))
        return corners


# @njit
def oddr_to_axial(position: Tuple[int, int]) -> Tuple[int, int]:
    """Convert odd-r offset coordinates to axial coordinates."""
    x, y = position
    q = x - (y - (y & 1)) // 2
    r = y
    return (q, r)
# @njit
def axial_to_oddr(position: Tuple[int, int]) -> Tuple[int, int]:
    """Convert axial coordinates to odd-r offset coordinates."""
    q, r = position
    x = q + (r - (r & 1)) // 2
    y = r
    return (x, y)

class DiagonalSquareBoard(SquareBoard):
    """Square board implementation with diagonal movement allowed."""

    def __init__(self, size: Tuple[int, int] = (7, 8)):
        super().__init__(size)

    @staticmethod
    # @njit
    def l2_distance(pos1: Tuple[int, int], pos2: Tuple[int, int]) -> float:
        """We set l2 distance to be the same as l1 distance when diagonal movement is allowed, since it effectively becomes a hybrid of l1 and l2."""
        return max(abs(pos1[0] - pos2[0]), abs(pos1[1] - pos2[1]))
    
    def get_adjacent_positions(self, position: Tuple[int, int]) -> List[Tuple[int, int]]:
        """Get valid adjacent positions with diagonals."""
        x, y = position
        adjacent = [
            (x+1, y), (x-1, y), (x, y+1), (x, y-1),  # Cardinal directions
            (x+1, y+1), (x+1, y-1), (x-1, y+1), (x-1, y-1)  # Diagonal directions
        ]
        return [pos for pos in adjacent if self.is_valid_position(pos)]
    
    def get_positions_in_l1_range(self, position: Tuple[int, int], l1_range: int) -> List[Tuple[int, int]]:
        """Get all positions within a certain amount of steps from a position."""
        positions_in_range = []
        for dx in range(-l1_range, l1_range + 1):
            for dy in range(-l1_range, l1_range + 1):
                new_pos = (position[0] + dx, position[1] + dy)
                if self.is_valid_position(new_pos):
                    positions_in_range.append(new_pos)
        return positions_in_range

#TODO: Triangular board?


            
        