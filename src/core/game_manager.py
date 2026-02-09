from typing import TYPE_CHECKING
if TYPE_CHECKING:
    from player import Player
    from typing import *

class GameManager:
    def __init__(self, players: List[Player]):
        self.players = players
        self.current_round = 0
        self.max_rounds = 10 
        self.round_time = 60
        self.board = None
        
    
