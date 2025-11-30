#!/bin/bash
# Run all preprocessing steps in the correct order for optimal OCR results.
#
# Pipeline order:
# 1. Convert to TIFF
# 2. Fix resolution (300 DPI)
# 3. Remove background
# 4. Deskew
# 5. Grayscale
# 6. Contrast enhancement
# 7. Denoise
#
# Usage: ./preprocess_all.sh <input_image> <output_image> [options]
#
# Options:
#   --dpi <value>        Target DPI (default: 300)
#   --no-deskew          Skip deskew step
#   --no-denoise         Skip denoise step
#   --no-contrast        Skip contrast enhancement step
#   --keep-intermediates Keep intermediate files (for debugging)
#
# Example:
#   ./preprocess_all.sh receipt.jpg preprocessed.png
#   ./preprocess_all.sh receipt.jpg preprocessed.png --dpi 300 --keep-intermediates

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Default options
TARGET_DPI=300
DO_DESKEW=true
DO_DENOISE=true
DO_CONTRAST=true
KEEP_INTERMEDIATES=false

# Parse arguments
INPUT=""
OUTPUT=""

while [[ $# -gt 0 ]]; do
    case $1 in
        --dpi)
            TARGET_DPI="$2"
            shift 2
            ;;
        --no-deskew)
            DO_DESKEW=false
            shift
            ;;
        --no-denoise)
            DO_DENOISE=false
            shift
            ;;
        --no-contrast)
            DO_CONTRAST=false
            shift
            ;;
        --keep-intermediates)
            KEEP_INTERMEDIATES=true
            shift
            ;;
        *)
            if [ -z "$INPUT" ]; then
                INPUT="$1"
            elif [ -z "$OUTPUT" ]; then
                OUTPUT="$1"
            fi
            shift
            ;;
    esac
done

if [ -z "$INPUT" ] || [ -z "$OUTPUT" ]; then
    echo "Usage: $0 <input_image> <output_image> [options]"
    echo ""
    echo "Options:"
    echo "  --dpi <value>        Target DPI (default: 300)"
    echo "  --no-deskew          Skip deskew step"
    echo "  --no-denoise         Skip denoise step"
    echo "  --no-contrast        Skip contrast enhancement step"
    echo "  --keep-intermediates Keep intermediate files (for debugging)"
    exit 1
fi

if [ ! -f "$INPUT" ]; then
    echo "Error: Input file '$INPUT' does not exist"
    exit 1
fi

# Create temp directory for intermediate files
TEMP_DIR=$(mktemp -d)
trap "if [ '$KEEP_INTERMEDIATES' = false ]; then rm -rf '$TEMP_DIR'; fi" EXIT

echo "Starting preprocessing pipeline..."
echo "Input: $INPUT"
echo "Output: $OUTPUT"
echo "Temp dir: $TEMP_DIR"

CURRENT="$INPUT"
STEP=1

# Step 1: Convert to TIFF
echo "Step $STEP: Converting to TIFF..."
NEXT="$TEMP_DIR/step${STEP}_tiff.tiff"
"$SCRIPT_DIR/convert_to_tiff.sh" "$CURRENT" "$NEXT"
CURRENT="$NEXT"
((STEP++))

# Step 2: Fix resolution
echo "Step $STEP: Fixing resolution to ${TARGET_DPI} DPI..."
NEXT="$TEMP_DIR/step${STEP}_resolution.tiff"
"$SCRIPT_DIR/fix_resolution.sh" "$CURRENT" "$NEXT" "$TARGET_DPI"
CURRENT="$NEXT"
((STEP++))

# Step 3: Remove background
echo "Step $STEP: Removing background..."
NEXT="$TEMP_DIR/step${STEP}_nobg.tiff"
"$SCRIPT_DIR/remove_background.sh" "$CURRENT" "$NEXT"
CURRENT="$NEXT"
((STEP++))

# Step 4: Deskew (optional)
if [ "$DO_DESKEW" = true ]; then
    echo "Step $STEP: Deskewing..."
    NEXT="$TEMP_DIR/step${STEP}_deskew.tiff"
    "$SCRIPT_DIR/deskew.sh" "$CURRENT" "$NEXT"
    CURRENT="$NEXT"
    ((STEP++))
fi

# Step 5: Grayscale
echo "Step $STEP: Converting to grayscale..."
NEXT="$TEMP_DIR/step${STEP}_gray.tiff"
"$SCRIPT_DIR/grayscale.sh" "$CURRENT" "$NEXT"
CURRENT="$NEXT"
((STEP++))

# Step 6: Contrast enhancement (optional)
if [ "$DO_CONTRAST" = true ]; then
    echo "Step $STEP: Enhancing contrast..."
    NEXT="$TEMP_DIR/step${STEP}_contrast.tiff"
    "$SCRIPT_DIR/enhance_contrast.sh" "$CURRENT" "$NEXT"
    CURRENT="$NEXT"
    ((STEP++))
fi

# Step 7: Denoise (optional)
if [ "$DO_DENOISE" = true ]; then
    echo "Step $STEP: Denoising..."
    NEXT="$TEMP_DIR/step${STEP}_denoise.tiff"
    "$SCRIPT_DIR/denoise.sh" "$CURRENT" "$NEXT"
    CURRENT="$NEXT"
    ((STEP++))
fi

# Copy final result to output
cp "$CURRENT" "$OUTPUT"

if [ "$KEEP_INTERMEDIATES" = true ]; then
    echo ""
    echo "Intermediate files kept in: $TEMP_DIR"
    ls -la "$TEMP_DIR"
fi

echo ""
echo "Preprocessing complete: $OUTPUT"
