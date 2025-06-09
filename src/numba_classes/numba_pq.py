#https://gist.github.com/gmarkall/62308d951d4c1553ccbe608f391de46d
import itertools

import numba as nb
from numba.experimental import jitclass

from typing import List, Tuple, Dict
from heapq import heappush, heappop
import time
from queue import PriorityQueue as PythonPriorityQueue



# @jitclass
class PurePythonPriorityQueue:
    def __init__(self):
        self.queue = []  # list of entries arranged in a heap
        self.entry_finder = {}  # mapping of indices to entries
        self.REMOVED = -1  # placeholder for a removed item
        self.counter = itertools.count()  # unique sequence count

    def put(self, inp: Tuple[float, Tuple[int, int]]):
        """Add a new item or update the priority of an existing item"""
        priority, item = inp
        if item in self.entry_finder:
            self.remove_item(item)
        count = next(self.counter)
        entry = [priority, count, item]
        self.entry_finder[item] = entry
        heappush(self.queue, entry)

    def remove_item(self, item: Tuple[int, int]):
        """Mark an existing item as REMOVED.  Raise KeyError if not found."""
        entry = self.entry_finder.pop(item)
        entry[-1] = self.REMOVED

    def get(self):
        """Remove and return the lowest priority item. Raise KeyError if empty."""
        while self.queue:
            priority, count, item = heappop(self.queue)
            if item is not self.REMOVED:
                del self.entry_finder[item]
                return priority, item
        raise KeyError("pop from an empty priority queue")
    def empty(self):
        """Check if the priority queue is empty."""
        return not self.entry_finder


# @jitclass
# class Item:
#     i: int
#     j: int
#
#     def __init__(self, i, j):
#         self.i = i
#         self.j = j
#
#     def __eq__(self, other):
#         return self.i == other.i and self.j == other.j


@jitclass
class Entry:
    priority: float
    count: int
    item: Tuple[int, int]
    removed: bool

    def __init__(self, p: float, c: int, i: Tuple[int, int]):
        self.priority = p
        self.count = c
        self.item = i
        self.removed = False

    def __lt__(self, other):
        return self.priority < other.priority


@jitclass
class NumbaPriorityQueue:
    queue: List[Entry]
    entry_finder: Dict[Tuple[int, int], Entry]
    counter: int

    def __init__(self):
        self.queue = nb.typed.List.empty_list(Entry(0.0, 0, (0, 0)))
        self.entry_finder = nb.typed.Dict.empty((0, 0), Entry(0, 0, (0, 0)))
        self.counter = 0

    def put(self, inp: Tuple[float, Tuple[int, int]]):
        """Add a new item or update the priority of an existing item"""
        priority, item = inp
        if item in self.entry_finder:
            self.remove_item(item)
        self.counter += 1
        entry = Entry(priority, self.counter, item)
        self.entry_finder[item] = entry
        heappush(self.queue, entry)

    def remove_item(self, item: Tuple[int, int]):
        """Mark an existing item as REMOVED.  Raise KeyError if not found."""
        entry = self.entry_finder.pop(item)
        entry.removed = True

    def get(self):
        """Remove and return the lowest priority item. Raise KeyError if empty."""
        while self.queue:
            entry = heappop(self.queue)
            if not entry.removed:
                del self.entry_finder[entry.item]
                return entry.priority, entry.item
        raise KeyError("pop from an empty priority queue")

    def empty(self):
        """Check if the priority queue is empty."""
        return len(self.entry_finder) == 0

@nb.njit
def mock_pq(queue: NumbaPriorityQueue):
    """Mock function to test the priority queue."""
    for i in range(100):
        queue.put((i, (i, i+1)))
    for i in range(100):
        queue.get()


if __name__ == "__main__":
    queue1 = PurePythonPriorityQueue()
    queue2 = NumbaPriorityQueue()
    mock_pq(queue2)  # Warm up the JIT compilation
    
    queue3 = PythonPriorityQueue()
    # Time Python's built-in PriorityQueue
    start_time = time.time()
    for _ in range(10000):
        for i in range(100):
            queue3.put((i, (i, i+1)))
        for i in range(100):
            queue3.get()
    python_builtin_time = time.time() - start_time
    # Time PurePythonPriorityQueue
    start_time = time.time()
    for _ in range(10000):
        for i in range(100):
            queue1.put((i, (i, i+1)))
        for i in range(100):
            queue1.get()
    # Time the pure Python implementation
    python_time = time.time() - start_time

    # Time JIT-compiled PriorityQueue
    start_time = time.time()
    for _ in range(10000):
        mock_pq(queue2)
    jit_time = time.time() - start_time



    print(f"Python PriorityQueue time: {python_builtin_time:.4f} seconds")
    print(f"Pure Python time: {python_time:.4f} seconds")
    print(f"JIT compiled time: {jit_time:.4f} seconds")
    print(f"Speedup: {python_time/jit_time:.2f}x")
    #No idea, but it seems like the JIT compiled version is about 5x slower than the pure Python implementation, and the Python built-in is about 2x slower than the pure Python implementation (this speedup does not translate to our use).

    # queue3 = OptimalQueue()
    # for i in range(5):
    #     for j in range(5):
    #         queue3.add_index(i, j)
    #         # [Index(i, j) for i in range(5) for j in range(5)]
    # print(queue3.indices)
    # queue3.put((4, 5), 5.4)
    # queue3.put((5, 6), 1.0)
    # print(queue3.pop())  # Yay this works!