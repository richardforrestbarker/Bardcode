#!/bin/bash
# Apply denoising using ImageMagick.
# Uses ImageMagick's enhance filter which reduces noise while preserving edges.
#
# Usage: ./denoise.sh <input_image> <output_image>
#
# Example:
#   ./denoise.sh receipt.tiff receipt_denoised.tiff
#
# ImageMagick command:
#   magick <input> -enhance <output>

set -e

# Use 'magick' if available (ImageMagick 7+), otherwise fall back to 'convert' (ImageMagick 6)
if command -v magick &> /dev/null; then
    MAGICK_CMD="magick"
else
    MAGICK_CMD="convert"
fi

if [ $# -ne 2 ]; then
    echo "Usage: $0 <input_image> <output_image>"
    echo "Example: $0 receipt.tiff receipt_denoised.tiff"
    exit 1
fi

INPUT="$1"
OUTPUT="$2"

if [ ! -f "$INPUT" ]; then
    echo "Error: Input file '$INPUT' does not exist"
    exit 1
fi

# Apply enhance filter for noise reduction
# -enhance: Apply a digital filter to reduce noise
$MAGICK_CMD "$INPUT" -enhance "$OUTPUT"

echo "Applied denoising: $OUTPUT"
