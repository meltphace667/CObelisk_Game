#!/usr/bin/env python3
# -*- coding: utf-8 -*-

"""
Obelisk Old Digital Look Batch Processor
----------------------------------------

Transforme un dossier d'images pour les rapprocher du look de ta room Ob_02 :
- vieux numérique / compact camera
- contraste dur
- noirs profonds
- verts saturés un peu jaunis
- ciel plus bleu
- grain / bruit léger
- légère netteté dure
- compression JPEG visible
- crop 4:3 optionnel pour Unity

Installation :
    py -m pip install pillow

Exemple simple :
    py obelisk_photo_grain_batch.py --input "input" --output "output"

Exemple plus fort :
    py obelisk_photo_grain_batch.py --input "input" --output "output" --strength 1.25 --jpeg-quality 62

Exemple sans recadrer :
    py obelisk_photo_grain_batch.py --input "input" --output "output" --no-crop
"""

import argparse
import io
import math
import random
from pathlib import Path
from typing import Iterable, Tuple

from PIL import Image, ImageEnhance, ImageFilter, ImageOps


SUPPORTED_EXTENSIONS = {
    ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".tif", ".tiff"
}


def clamp(value: float, minimum: int = 0, maximum: int = 255) -> int:
    return max(minimum, min(maximum, int(round(value))))


def parse_size(value: str) -> Tuple[int, int]:
    try:
        width_text, height_text = value.lower().replace(" ", "").split("x")
        width = int(width_text)
        height = int(height_text)
    except Exception as exc:
        raise argparse.ArgumentTypeError(
            "Le format de taille doit être du type 1600x1200 ou 1024x768."
        ) from exc

    if width <= 0 or height <= 0:
        raise argparse.ArgumentTypeError("La taille doit être positive.")

    return width, height


def list_images(input_dir: Path, recursive: bool) -> Iterable[Path]:
    pattern = "**/*" if recursive else "*"

    for path in sorted(input_dir.glob(pattern)):
        if not path.is_file():
            continue

        if path.suffix.lower() in SUPPORTED_EXTENSIONS:
            yield path


def center_crop_to_aspect(image: Image.Image, aspect_ratio: float) -> Image.Image:
    width, height = image.size
    current_ratio = width / height

    if abs(current_ratio - aspect_ratio) < 0.001:
        return image

    if current_ratio > aspect_ratio:
        # Image trop large : on coupe les côtés.
        new_width = int(height * aspect_ratio)
        left = (width - new_width) // 2
        return image.crop((left, 0, left + new_width, height))

    # Image trop haute : on coupe haut/bas.
    new_height = int(width / aspect_ratio)
    top = (height - new_height) // 2
    return image.crop((0, top, width, top + new_height))


def resize_image(image: Image.Image, target_size: Tuple[int, int]) -> Image.Image:
    return image.resize(target_size, Image.Resampling.LANCZOS)


def lift_and_crush_tones(image: Image.Image, strength: float) -> Image.Image:
    """
    Courbe simple :
    - noirs plus noirs
    - hautes lumières un peu chaudes
    - contraste compact camera
    """
    image = image.convert("RGB")
    pixels = image.load()
    width, height = image.size

    black_crush = 16 * strength
    highlight_lift = 6 * strength
    gamma = 0.92 / max(0.65, strength)

    for y in range(height):
        for x in range(width):
            r, g, b = pixels[x, y]

            # Gamma.
            rn = (r / 255.0) ** gamma
            gn = (g / 255.0) ** gamma
            bn = (b / 255.0) ** gamma

            r = rn * 255
            g = gn * 255
            b = bn * 255

            # Crush des noirs.
            r = max(0, (r - black_crush) * (255 / max(1, 255 - black_crush)))
            g = max(0, (g - black_crush) * (255 / max(1, 255 - black_crush)))
            b = max(0, (b - black_crush) * (255 / max(1, 255 - black_crush)))

            # Hautes lumières légèrement poussées.
            luminance = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255.0
            if luminance > 0.55:
                r += highlight_lift * (luminance - 0.55)
                g += highlight_lift * (luminance - 0.55)
                b += highlight_lift * (luminance - 0.55)

            pixels[x, y] = (clamp(r), clamp(g), clamp(b))

    return image


def shift_colors_ob02(image: Image.Image, strength: float) -> Image.Image:
    """
    Rend les verts plus denses/jaunes et le ciel plus bleu.
    Pas du vrai étalonnage pro, mais utile pour homogénéiser vite.
    """
    image = image.convert("RGB")
    pixels = image.load()
    width, height = image.size

    for y in range(height):
        for x in range(width):
            r, g, b = pixels[x, y]

            # Détection approximative verts.
            is_green = g > r * 1.05 and g > b * 1.03 and g > 45

            if is_green:
                # Vert plus dense, légèrement jaunâtre, moins cyan.
                r += 8 * strength
                g += 18 * strength
                b -= 10 * strength

            # Détection ciel / bleu clair.
            is_sky = b > r * 1.08 and b > g * 0.95 and r > 80 and g > 90

            if is_sky:
                r -= 3 * strength
                g += 2 * strength
                b += 18 * strength

            # Objets jaunes / pierre chaude.
            is_yellow = r > 120 and g > 95 and b < 95

            if is_yellow:
                r += 10 * strength
                g += 5 * strength
                b -= 6 * strength

            pixels[x, y] = (clamp(r), clamp(g), clamp(b))

    return image


def add_vignette(image: Image.Image, strength: float) -> Image.Image:
    if strength <= 0:
        return image

    image = image.convert("RGB")
    width, height = image.size
    pixels = image.load()

    center_x = width / 2.0
    center_y = height / 2.0
    max_distance = math.sqrt(center_x * center_x + center_y * center_y)

    vignette_amount = 0.22 * strength

    for y in range(height):
        for x in range(width):
            r, g, b = pixels[x, y]
            dx = x - center_x
            dy = y - center_y
            distance = math.sqrt(dx * dx + dy * dy) / max_distance
            factor = 1.0 - (distance ** 1.8) * vignette_amount

            pixels[x, y] = (
                clamp(r * factor),
                clamp(g * factor),
                clamp(b * factor),
            )

    return image


def add_noise(image: Image.Image, amount: float, seed: int) -> Image.Image:
    if amount <= 0:
        return image

    random.seed(seed)

    image = image.convert("RGB")
    pixels = image.load()
    width, height = image.size

    # Bruit un peu coloré, un peu JPEG/compact.
    noise_strength = 12.0 * amount

    for y in range(height):
        for x in range(width):
            r, g, b = pixels[x, y]

            n = random.gauss(0, noise_strength)
            chroma = random.gauss(0, noise_strength * 0.35)

            pixels[x, y] = (
                clamp(r + n + chroma),
                clamp(g + n),
                clamp(b + n - chroma),
            )

    return image


def apply_jpeg_artifacts(image: Image.Image, quality: int) -> Image.Image:
    buffer = io.BytesIO()
    image.convert("RGB").save(
        buffer,
        format="JPEG",
        quality=quality,
        optimize=False,
        progressive=False,
        subsampling=2,
    )
    buffer.seek(0)
    return Image.open(buffer).convert("RGB")


def sharpen_old_digital(image: Image.Image, strength: float) -> Image.Image:
    # UnsharpMask donne ce côté "trop net / vieux numérique".
    radius = 1.0
    percent = int(90 * strength)
    threshold = 3
    return image.filter(ImageFilter.UnsharpMask(radius=radius, percent=percent, threshold=threshold))


def process_image(
    image: Image.Image,
    target_size: Tuple[int, int],
    crop: bool,
    strength: float,
    jpeg_quality: int,
    noise_amount: float,
    vignette: float,
    seed: int,
) -> Image.Image:
    image = ImageOps.exif_transpose(image).convert("RGB")

    if crop:
        aspect = target_size[0] / target_size[1]
        image = center_crop_to_aspect(image, aspect)

    image = resize_image(image, target_size)

    # Base old digital.
    image = ImageOps.autocontrast(image, cutoff=1)

    image = ImageEnhance.Contrast(image).enhance(1.22 * strength)
    image = ImageEnhance.Color(image).enhance(1.18 * strength)
    image = ImageEnhance.Brightness(image).enhance(0.98)

    image = lift_and_crush_tones(image, strength)
    image = shift_colors_ob02(image, strength)

    image = add_vignette(image, vignette * strength)

    image = sharpen_old_digital(image, strength)

    # Premier passage JPEG avant bruit pour casser un peu les aplats.
    image = apply_jpeg_artifacts(image, jpeg_quality)

    image = add_noise(image, noise_amount * strength, seed)

    # Deuxième petit passage JPEG pour homogénéiser.
    image = apply_jpeg_artifacts(image, min(95, jpeg_quality + 8))

    return image


def output_path_for(input_path: Path, input_dir: Path, output_dir: Path, output_format: str) -> Path:
    relative = input_path.relative_to(input_dir)
    extension = "." + output_format.lower()
    return output_dir / relative.with_suffix(extension)


def save_output(image: Image.Image, output_path: Path, output_format: str, png_compress_level: int, jpeg_quality: int) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)

    if output_format.lower() == "png":
        image.save(output_path, format="PNG", compress_level=png_compress_level)
        return

    if output_format.lower() in {"jpg", "jpeg"}:
        image.save(output_path, format="JPEG", quality=jpeg_quality, optimize=True, progressive=False)
        return

    raise ValueError("Format de sortie non supporté : " + output_format)


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Applique un look vieux numérique / Ob_02 à un dossier d'images."
    )

    parser.add_argument("--input", "-i", required=True, help="Dossier contenant les images originales.")
    parser.add_argument("--output", "-o", required=True, help="Dossier où écrire les images traitées.")
    parser.add_argument("--size", default="1600x1200", type=parse_size, help="Taille de sortie. Défaut : 1600x1200.")
    parser.add_argument("--no-crop", action="store_true", help="Ne pas recadrer en 4:3 avant resize.")
    parser.add_argument("--recursive", action="store_true", help="Traiter aussi les sous-dossiers.")

    parser.add_argument("--strength", type=float, default=1.0, help="Force globale du look. 0.7 subtil, 1.0 normal, 1.3 fort.")
    parser.add_argument("--jpeg-quality", type=int, default=68, help="Qualité JPEG interne pour les artefacts. Plus bas = plus sale.")
    parser.add_argument("--noise", type=float, default=0.55, help="Quantité de grain/bruit. 0 à 1.2 conseillé.")
    parser.add_argument("--vignette", type=float, default=0.55, help="Vignette sombre. 0 à 1 conseillé.")
    parser.add_argument("--seed", type=int, default=1234, help="Seed du bruit pour résultat reproductible.")

    parser.add_argument("--format", choices=["png", "jpg", "jpeg"], default="png", help="Format de sortie. Défaut : png.")
    parser.add_argument("--png-compress-level", type=int, default=6, help="Compression PNG. 0 à 9.")

    args = parser.parse_args()

    input_dir = Path(args.input).expanduser().resolve()
    output_dir = Path(args.output).expanduser().resolve()

    if not input_dir.exists() or not input_dir.is_dir():
        raise SystemExit("Dossier input introuvable : " + str(input_dir))

    if args.jpeg_quality < 25 or args.jpeg_quality > 95:
        raise SystemExit("--jpeg-quality doit être entre 25 et 95.")

    if args.strength <= 0:
        raise SystemExit("--strength doit être supérieur à 0.")

    image_paths = list(list_images(input_dir, args.recursive))

    if not image_paths:
        raise SystemExit("Aucune image trouvée dans : " + str(input_dir))

    print("Images trouvées :", len(image_paths))
    print("Input :", input_dir)
    print("Output:", output_dir)
    print("Taille:", f"{args.size[0]}x{args.size[1]}")
    print("Crop 4:3:", not args.no_crop)
    print("Strength:", args.strength)

    for index, image_path in enumerate(image_paths, start=1):
        try:
            with Image.open(image_path) as source:
                processed = process_image(
                    image=source,
                    target_size=args.size,
                    crop=not args.no_crop,
                    strength=args.strength,
                    jpeg_quality=args.jpeg_quality,
                    noise_amount=args.noise,
                    vignette=args.vignette,
                    seed=args.seed + index,
                )

            output_path = output_path_for(image_path, input_dir, output_dir, args.format)
            save_output(
                processed,
                output_path,
                output_format=args.format,
                png_compress_level=args.png_compress_level,
                jpeg_quality=args.jpeg_quality,
            )

            print(f"[OK] {image_path.name} -> {output_path.name}")

        except Exception as exc:
            print(f"[ERREUR] {image_path.name} : {exc}")

    print("")
    print("Terminé.")
    print("Conseil Unity : importe le dossier output, puis mets Texture Type = Sprite (2D and UI) si nécessaire.")


if __name__ == "__main__":
    main()
