def less_than_or_equal(a: float, b: float, tol: float = 1e-9) -> bool:
    return a < b or abs(a - b) < tol