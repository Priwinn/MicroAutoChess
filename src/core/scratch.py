def create_hex_cell(content=""):
    """
    Returns a list of 5 strings representing the ASCII art for a single hex cell.
    The cell is 6 characters wide and 5 lines high.
    The middle (third) line shows the content, centered in a field of width 4.
    """
    # Ensure the content fits in 4 characters.
    content_str = str(content)[:4]
    # Each cell's 5 lines:
    cell = [
        "   /\\  ",  # line 0: top point
        " /    \\ ",  # line 1: upper sides
        f"|{content_str:^6}|",  # line 2: content line (centered in 4 spaces)
        " \\    / ",  # line 3: lower sides
        "   \\/  "   # line 4: bottom point
    ]
    return cell

def generate_hex_grid(rows, cols, contents=None):
    """
    Generates an ASCII drawing of an odd-r hex grid with point-up hexes.
    
    Parameters:
      rows (int): Number of hex rows.
      cols (int): Number of hex columns.
      contents: An optional 2D list (rows x cols) of content strings for each cell.
                If omitted, the cell's coordinate (r,c) is used.
    
    Returns:
      A string containing the complete ASCII art grid.
    """
    # Each hex cell's dimensions
    cell_width = 6   # each cell drawn is 6 characters wide
    cell_height = 5  # each cell drawn is 5 lines tall
    
    # In a point-up hex grid the vertical stacking overlaps.
    # We use a vertical offset of 3 lines per row.
    vert_offset = 3
    canvas_height = (rows - 1) * vert_offset + cell_height
    # For horizontal extent, odd rows get shifted by half cell width (3 spaces).
    canvas_width = cell_width * cols + 10  # extra for shifted rows
    
    # Create a blank canvas (list of lists of characters)
    canvas = [[" " for _ in range(canvas_width)] for _ in range(canvas_height)]
    
    # If no contents provided, fill with default coordinates.
    if contents is None:
        contents = [[f"{r},{c}" for c in range(cols)] for r in range(rows)]
    
    # For each hex cell, compute its top-left position and overlay its ASCII art onto the canvas.
    for r in range(rows):
        for c in range(cols):
            # For odd rows (1-indexed odd, i.e. r % 2 == 1 in 0-indexing),
            # we indent by half the cell width (3 spaces)
            x_offset = (3 if (r % 2 == 1) else 0) + c * cell_width
            y_offset = r * vert_offset
            # Get content for this cell (each cell's content is trimmed/padded in create_hex_cell)
            cell_content = contents[r][c] if r < len(contents) and c < len(contents[r]) else ""
            cell_art = create_hex_cell(cell_content)
            
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
    return ascii_art

# Example usage:
if __name__ == "__main__":
    # Define grid dimensions
    grid_rows = 8
    grid_cols = 8

    # Optionally, you can define a custom grid of cell contents.
    # Here, we fill each cell with its (row, col) coordinates (abbreviated).
    contents = [[f"{r}{c}" for c in range(grid_cols)] for r in range(grid_rows)]
    
    art = generate_hex_grid(grid_rows, grid_cols, contents)
    print(art)
