#!/bin/bash
# Convert image to grayscale using ImageMagick.
#
# Usage: ./grayscale.sh <input_image> <output_image>
#
# Example:
#   ./grayscale.sh receipt.tiff receipt_gray.tiff
#
# ImageMagick command:
#   magick <input> -colorspace Gray <output>

set -e

# Use 'magick' if available (ImageMagick 7+), otherwise fall back to 'convert' (ImageMagick 6)
if command -v magick &> /dev/null; then
    MAGICK_CMD="magick"
else
    MAGICK_CMD="convert"
fi

if [ $# -ne 2 ]; then
    echo "Usage: $0 <input_image> <output_image>"
    echo "Example: $0 receipt.tiff receipt_gray.tiff"
    exit 1
fi

INPUT="$1"
OUTPUT="$2"

if [ ! -f "$INPUT" ]; then
    echo "Error: Input file '$INPUT' does not exist"
    exit 1
fi

# Convert to grayscale using the Gray colorspace
$MAGICK_CMD "$INPUT" -colorspace Gray "$OUTPUT"

echo "Converted to grayscale: $OUTPUT"
