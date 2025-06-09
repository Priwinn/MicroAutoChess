from numba import njit
import time

@njit
def inner_loop(total, i):
    x = i % 10
    if x > 5:
        total += (x * 1.1) ** 0.5
    else:
        total -= (x * 0.9) ** 0.5
    return total



def inner_loop_py(total, i):
    x = i % 10
    if x > 5:
        total += (x * 1.1) ** 0.5
    else:
        total -= (x * 0.9) ** 0.5
    return total

start = time.time()
for i in range(10000000):
    inner_loop(0, i)
print("JIT Time:", time.time() - start)
start = time.time()
for i in range(10000000):
    inner_loop_py(0, i)
print("Python Time:", time.time() - start)
