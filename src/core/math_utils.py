import math
def float_less_than_or_equal(a: float, b: float, tol: float = 1e-9) -> bool:
    return a < b or math.isclose(a, b, abs_tol=tol)