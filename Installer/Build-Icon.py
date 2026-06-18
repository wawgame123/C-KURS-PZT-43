from collections import deque
from pathlib import Path
import sys

from PIL import Image


def is_background(pixel):
    red, green, blue, alpha = pixel
    return alpha == 0 or (red >= 245 and green >= 245 and blue >= 245)


def remove_outer_background(image):
    image = image.convert("RGBA")
    pixels = image.load()
    width, height = image.size
    visited = bytearray(width * height)
    queue = deque()

    def add(x, y):
        index = y * width + x
        if visited[index] or not is_background(pixels[x, y]):
            return
        visited[index] = 1
        queue.append((x, y))

    for x in range(width):
        add(x, 0)
        add(x, height - 1)

    for y in range(height):
        add(0, y)
        add(width - 1, y)

    while queue:
        x, y = queue.popleft()
        red, green, blue, _ = pixels[x, y]
        pixels[x, y] = (red, green, blue, 0)

        if x > 0:
            add(x - 1, y)
        if x + 1 < width:
            add(x + 1, y)
        if y > 0:
            add(x, y - 1)
        if y + 1 < height:
            add(x, y + 1)

    return image


def build_icon(source_path, png_path, ico_path):
    source = Image.open(source_path)
    source = remove_outer_background(source)

    bounds = source.getbbox()
    if bounds:
        source = source.crop(bounds)

    canvas_size = 1024
    content_size = 940
    source.thumbnail((content_size, content_size), Image.Resampling.LANCZOS)

    canvas = Image.new("RGBA", (canvas_size, canvas_size), (0, 0, 0, 0))
    position = (
        (canvas_size - source.width) // 2,
        (canvas_size - source.height) // 2,
    )
    canvas.alpha_composite(source, position)

    png_path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(png_path, "PNG")
    canvas.save(
        ico_path,
        "ICO",
        sizes=[(16, 16), (24, 24), (32, 32), (48, 48),
               (64, 64), (128, 128), (256, 256)],
    )


if __name__ == "__main__":
    if len(sys.argv) != 4:
        raise SystemExit("Usage: Build-Icon.py source.png output.png output.ico")

    build_icon(
        Path(sys.argv[1]),
        Path(sys.argv[2]),
        Path(sys.argv[3]),
    )
