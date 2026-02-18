#include <queue>
#include <string>
#include <utility>
#include <vector>

#include <pybind11/pybind11.h>
#include <pybind11/stl.h>

namespace py = pybind11;

namespace {
struct Pos {
    int x;
    int y;
};

static Pos to_pos(const py::tuple &t) {
    if (t.size() != 2) {
        throw py::value_error("Position must be a 2-tuple");
    }
    return {t[0].cast<int>(), t[1].cast<int>()};
}

static bool in_bounds(int x, int y, int width, int height) {
    return x >= 0 && x < width && y >= 0 && y < height;
}

static int idx(int x, int y, int width) {
    return y * width + x;
}

static void add_neighbors_square(int x, int y, int width, int height, std::vector<Pos> &out) {
    const int dx[4] = {1, -1, 0, 0};
    const int dy[4] = {0, 0, 1, -1};
    for (int i = 0; i < 4; ++i) {
        int nx = x + dx[i];
        int ny = y + dy[i];
        if (in_bounds(nx, ny, width, height)) {
            out.push_back({nx, ny});
        }
    }
}

static void add_neighbors_diagonal(int x, int y, int width, int height, std::vector<Pos> &out) {
    for (int dx = -1; dx <= 1; ++dx) {
        for (int dy = -1; dy <= 1; ++dy) {
            if (dx == 0 && dy == 0) {
                continue;
            }
            int nx = x + dx;
            int ny = y + dy;
            if (in_bounds(nx, ny, width, height)) {
                out.push_back({nx, ny});
            }
        }
    }
}

static void add_neighbors_hex(int x, int y, int width, int height, std::vector<Pos> &out) {
    if (y % 2 == 1) {
        const int dx[6] = {0, 1, 1, 1, 0, -1};
        const int dy[6] = {-1, -1, 0, 1, 1, 0};
        for (int i = 0; i < 6; ++i) {
            int nx = x + dx[i];
            int ny = y + dy[i];
            if (in_bounds(nx, ny, width, height)) {
                out.push_back({nx, ny});
            }
        }
    } else {
        const int dx[6] = {-1, 0, 1, 0, -1, -1};
        const int dy[6] = {-1, -1, 0, 1, 1, 0};
        for (int i = 0; i < 6; ++i) {
            int nx = x + dx[i];
            int ny = y + dy[i];
            if (in_bounds(nx, ny, width, height)) {
                out.push_back({nx, ny});
            }
        }
    }
}

static int pathfind_distance_impl(
    int width,
    int height,
    const std::string &board_type,
    const std::vector<unsigned char> &occupied,
    const py::tuple &start_t,
    const py::tuple &target_t) {
    if (width <= 0 || height <= 0) {
        throw py::value_error("Invalid board size");
    }
    if (static_cast<int>(occupied.size()) != width * height) {
        throw py::value_error("Occupied grid size does not match board dimensions");
    }

    Pos start = to_pos(start_t);
    Pos target = to_pos(target_t);
    if (!in_bounds(start.x, start.y, width, height) || !in_bounds(target.x, target.y, width, height)) {
        throw py::value_error("Start or target position is out of bounds");
    }

    if (start.x == target.x && start.y == target.y) {
        return 0;
    }

    std::vector<int> dist(width * height, -1);
    std::queue<Pos> q;
    dist[idx(start.x, start.y, width)] = 0;
    q.push(start);

    std::vector<Pos> neighbors;
    neighbors.reserve(8);

    while (!q.empty()) {
        Pos cur = q.front();
        q.pop();
        neighbors.clear();

        if (board_type == "square") {
            add_neighbors_square(cur.x, cur.y, width, height, neighbors);
        } else if (board_type == "diagonal") {
            add_neighbors_diagonal(cur.x, cur.y, width, height, neighbors);
        } else {
            add_neighbors_hex(cur.x, cur.y, width, height, neighbors);
        }

        for (const auto &n : neighbors) {
            int nidx = idx(n.x, n.y, width);
            if (dist[nidx] != -1) {
                continue;
            }
            if (occupied[nidx] && !(n.x == target.x && n.y == target.y)) {
                continue;
            }
            dist[nidx] = dist[idx(cur.x, cur.y, width)] + 1;
            if (n.x == target.x && n.y == target.y) {
                return dist[nidx];
            }
            q.push(n);
        }
    }

    return -1;
}
}  // namespace

PYBIND11_MODULE(pathfinding_ext, m) {
    m.doc() = "Native pathfinding distance for MicroAutoChess";
    m.def(
        "pathfind_distance",
        &pathfind_distance_impl,
        py::arg("width"),
        py::arg("height"),
        py::arg("board_type"),
        py::arg("occupied"),
        py::arg("start"),
        py::arg("target"),
        "Compute shortest path distance; returns -1 if no path."
    );
}
