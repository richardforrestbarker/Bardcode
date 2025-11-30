#!/bin/bash
# Convert image to grayscale using ImageMagick.
#
# Usage: ./grayscale.sh <input_image> <output_image>
#
# Example:
#   ./grayscale.sh receipt.tiff receipt_gray.tiff
#
# ImageMagick command:
#   convert <input> -colorspace Gray <output>

set -e

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
convert "$INPUT" -colorspace Gray "$OUTPUT"

echo "Converted to grayscale: $OUTPUT"
