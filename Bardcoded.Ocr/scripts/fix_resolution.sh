#!/bin/bash
# Ensure image is at 300 DPI for optimal OCR results.
# 300 DPI is the recommended resolution for Tesseract OCR.
#
# Usage: ./fix_resolution.sh <input_image> <output_image> [target_dpi]
#
# Example:
#   ./fix_resolution.sh receipt.tiff receipt_300dpi.tiff 300
#
# ImageMagick commands:
#   # Identify current DPI
#   identify -format "%x %y" <input>
#   
#   # Resample to 300 DPI (adjusts both pixels and density)
#   convert <input> -resample 300 -units PixelsPerInch <output>

set -e

if [ $# -lt 2 ]; then
    echo "Usage: $0 <input_image> <output_image> [target_dpi]"
    echo "Example: $0 receipt.tiff receipt_300dpi.tiff 300"
    exit 1
fi

INPUT="$1"
OUTPUT="$2"
TARGET_DPI="${3:-300}"

if [ ! -f "$INPUT" ]; then
    echo "Error: Input file '$INPUT' does not exist"
    exit 1
fi

# Get current DPI (returns "x_dpi y_dpi")
CURRENT_DPI=$(identify -format "%x %y" "$INPUT" 2>/dev/null || echo "72 72")

echo "Current DPI: $CURRENT_DPI"
echo "Target DPI: $TARGET_DPI"

# Resample to target DPI - this adjusts both the pixel dimensions and the density metadata
convert "$INPUT" -resample "$TARGET_DPI" -units PixelsPerInch "$OUTPUT"

echo "Fixed resolution to ${TARGET_DPI} DPI: $OUTPUT"
