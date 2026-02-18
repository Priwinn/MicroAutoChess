from setuptools import setup

try:
    from pybind11.setup_helpers import Pybind11Extension, build_ext
    ext_modules = [
        Pybind11Extension(
            "pathfinding_ext",
            ["src/native/pathfinding_ext.cpp"],
            cxx_std=17,
            extra_compile_args=["/O2"],
            define_macros=[("Py_GIL_DISABLED", "0")],
        )
    ]
except ImportError:
    ext_modules = []
    build_ext = None

setup(
    name="microautochess-pathfinding",
    version="0.1.0",
    ext_modules=ext_modules,
    cmdclass={"build_ext": build_ext} if build_ext else {},
    zip_safe=False,
    setup_requires=["pybind11>=2.13.0"],
)
