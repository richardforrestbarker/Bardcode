# Receipt OCR Service

Python-based receipt OCR service using PaddleOCR and LayoutLMv3 for structured data extraction.

## Python Requirements

- **Python version must be 3.12** for all environments.
- **Windows users:** The Ninja build system must **not** be on your PATH, or else pip will use Ninja for building wheels instead of the default backend (setuptools). This can cause build failures for some dependencies. If you encounter build errors, ensure Ninja is not present in your PATH.

## Overview

This service provides OCR and structured field extraction from receipt images using:
- **PaddleOCR (PP-StructureV3)**: Text detection and recognition
- **LayoutLMv3**: Layout-aware field extraction (vendor, date, total, line items, etc.)

## Features

- Multi-page receipt processing
- Token-to-bounding-box mapping
- Configurable model selection (LayoutLMv3, LayoutLMv2, Donut)
- GPU acceleration with CPU fallback
- CLI interface for integration with .NET API

## Setup

### Prerequisites

- Python 3.9 or higher
- CUDA-capable GPU (optional, but recommended for performance)
- 8GB+ RAM (16GB+ recommended with GPU)

### Installation

```bash
# Create virtual environment
python -m venv venv
source venv/bin/activate  # On Windows: venv\Scripts\activate

# Install dependencies
pip install -r requirements.txt
```

### Installing OCR Dependencies

#### PaddleOCR (Recommended - Primary OCR Engine)

**On Linux/macOS:**
```bash
# Install PaddlePaddle (CPU)
pip install paddlepaddle

# Or with GPU support (CUDA 11.8)
pip install paddlepaddle-gpu

# Install PaddleOCR
pip install paddleocr
```

**On Windows:**
```bash
# Install PaddlePaddle (CPU)
pip install paddlepaddle

# Install PaddleOCR
pip install paddleocr

# Note: GPU support on Windows requires specific CUDA version
# See: https://www.paddlepaddle.org.cn/install/quick
```

PaddleOCR will automatically download required models on first use.

#### ImageMagick (Required - Image Preprocessing)

ImageMagick is required for the image preprocessing pipeline. It provides optimal image processing for best OCR accuracy.

**On Ubuntu/Debian:**
```bash
sudo apt-get update
sudo apt-get install imagemagick
```

**On macOS:**
```bash
brew install imagemagick
```

**On Windows:**
1. Download the installer from: https://imagemagick.org/script/download.php
2. Run the installer and select "Install development headers and libraries for C and C++"
3. Add ImageMagick to your PATH environment variable (the installer can do this automatically)

**Verify installation:**
```bash
magick --version
```

#### Tesseract (Fallback OCR Engine)

**On Ubuntu/Debian:**
```bash
sudo apt-get update
sudo apt-get install tesseract-ocr tesseract-ocr-eng
pip install pytesseract
```

**On macOS:**
```bash
brew install tesseract
pip install pytesseract
```

**On Windows:**
1. Download the installer from: https://github.com/UB-Mannheim/tesseract/wiki
2. Run the installer and note the installation path (e.g., `C:\Program Files\Tesseract-OCR`)
3. Add Tesseract to your PATH environment variable
4. Install the Python wrapper:
   ```bash
   pip install pytesseract
   ```

### Downloading LayoutLMv3 Models

The LayoutLMv3 model is automatically downloaded from HuggingFace on first use. However, you can pre-download it:

**Option 1: Using HuggingFace CLI (Recommended)**
```bash
# Install HuggingFace Hub CLI
pip install huggingface_hub

# Download the base model
huggingface-cli download microsoft/layoutlmv3-base --local-dir ./models/layoutlmv3-base

# Or download a fine-tuned receipt model (if available)
huggingface-cli download your-username/layoutlmv3-receipts --local-dir ./models/layoutlmv3-receipts
```

**Option 2: Using Python**
```python
from transformers import AutoProcessor, AutoModelForTokenClassification

# Download and cache the model
model_name = "microsoft/layoutlmv3-base"
processor = AutoProcessor.from_pretrained(model_name)
model = AutoModelForTokenClassification.from_pretrained(model_name)

# Optionally save to local directory
processor.save_pretrained("./models/layoutlmv3-base")
model.save_pretrained("./models/layoutlmv3-base")
```

**Option 3: Manual Download**
1. Visit https://huggingface.co/microsoft/layoutlmv3-base
2. Download all files to `./models/layoutlmv3-base`
3. Use the local path when running the CLI:
   ```bash
   python cli.py process --image receipt.jpg --model ./models/layoutlmv3-base
   ```

### Verifying Installation

```bash
# Check all dependencies
python -c "
import torch
import transformers
import paddleocr
print(f'PyTorch: {torch.__version__}')
print(f'CUDA available: {torch.cuda.is_available()}')
print(f'Transformers: {transformers.__version__}')
print('PaddleOCR: OK')
"

# Test with version command
python cli.py version
```

## Usage

### Command-line Interface

Process a single receipt:

```bash
python cli.py process --image path/to/receipt.jpg --output result.json
```

Process multiple pages:

```bash
python cli.py process --image page1.jpg --image page2.jpg --output result.json
```

Configure OCR engine and model:

```bash
python cli.py process --image receipt.jpg --output result.json --ocr-engine paddle --model microsoft/layoutlmv3-base --device cuda
```

### Debug Mode

Debug mode saves intermediary images for each processing step, allowing you to validate that each stage of the pipeline is functioning correctly:

```bash
python cli.py process \
  --image receipt.jpg \
  --output result.json \
  --debug \
  --debug-output-dir ./my_debug_output
```

When debug mode is enabled, the following files are created in the debug output directory:

| Step | File | Description |
|------|------|-------------|
| 1 | `step_01_source_page01.png` | Original source image |
| 2 | `step_02_grayscale_page01.png` | Grayscale converted image |
| 3 | `step_03_denoised_page01.png` | Denoised image (if --denoise enabled) |
| 4 | `step_04_deskewed_page01.png` | Deskewed image (if --deskew enabled) |
| 5 | `step_05_contrast_enhanced_page01.png` | Contrast enhanced image |
| 6 | `step_06_preprocessed_final_page01.png` | Final preprocessed image sent to OCR |
| 7 | `step_07_paddle_bboxes_page01.png` | Image with OCR bounding boxes drawn (color-coded by confidence) |
| 8 | `step_08_result_bboxes_page01.png` | Image with extracted field bounding boxes drawn |
| - | `debug_summary.json` | Summary JSON with list of files and final result |

The debug output helps diagnose issues in the processing pipeline:
- **Low OCR accuracy?** Check grayscale and preprocessing steps
- **Missing text?** Examine OCR bounding boxes visualization
- **Incorrect field extraction?** Review result bounding boxes to see what fields were identified

### Python API

```python
from src.receipt_processor import ReceiptProcessor

processor = ReceiptProcessor(
    model_name="microsoft/layoutlmv3-base",
    ocr_engine="paddle",
    device="cuda"
)

result = processor.process_receipt(["page1.jpg", "page2.jpg"])
print(result.to_json())
```

## Configuration

Configuration file: `config/config.yaml`

```yaml
model:
  name_or_path: "microsoft/layoutlmv3-base"
  device: "auto"  # auto, cuda, cpu
  
ocr:
  engine: "paddle"  # paddle, tesseract
  detection_mode: "word"  # word, line
  
preprocessing:
  target_dpi: 300
  denoise: true
  deskew: true
  
postprocessing:
  min_confidence: 0.5
  verify_totals: true
```

## Output Format

```json
{
  "job_id": "unique-job-id",
  "status": "done",
  "pages": [
    {
      "page_number": 1,
      "raw_ocr_text": "Full text from OCR...",
      "words": [
        {
          "text": "TOTAL",
          "box": {"x0": 100, "y0": 200, "x1": 200, "y1": 250},
          "confidence": 0.98
        }
      ]
    }
  ],
  "vendor_name": {
    "value": "Sample Store",
    "confidence": 0.95,
    "box": {"x0": 50, "y0": 20, "x1": 300, "y1": 80}
  },
  "date": {
    "value": "2024-01-15",
    "confidence": 0.92,
    "box": {"x0": 400, "y0": 30, "x1": 550, "y1": 70}
  },
  "total_amount": {
    "value": "45.99",
    "confidence": 0.96,
    "box": {"x0": 420, "y0": 600, "x1": 520, "y1": 650}
  },
  "line_items": [
    {
      "description": "Product 1",
      "quantity": 2,
      "unit_price": 10.50,
      "line_total": 21.00,
      "box": {"x0": 50, "y0": 300, "x1": 550, "y1": 340},
      "confidence": 0.89
    }
  ]
}
```

## Architecture

### Pipeline Stages

1. **Image Preprocessing** (via ImageMagick):
   - Convert to TIFF
   - Fix resolution (300 DPI)
   - Remove background
   - Deskew
   - Grayscale
   - Contrast enhancement
   - Denoise
2. **Text Detection**: PaddleOCR detector finds text regions
3. **OCR**: PaddleOCR recognizer extracts text with bounding boxes
4. **Tokenization**: Split text into model tokens, map to boxes
5. **Model Inference**: LayoutLMv3 identifies field types and entities
6. **Postprocessing**: Parse values, verify totals, merge multi-page results

### Manual Image Preprocessing

You can run the preprocessing steps manually using ImageMagick before calling the CLI. This is useful for debugging or customizing the preprocessing pipeline.

Shell scripts are provided in the `scripts/` directory for each preprocessing step:

```bash
# Run all preprocessing steps at once
./scripts/preprocess_all.sh input.jpg output.tiff

# Or run steps individually:

# Step 1: Convert to TIFF (optimal format for Tesseract)
./scripts/convert_to_tiff.sh input.jpg step1.tiff

# Step 2: Fix resolution to 300 DPI
./scripts/fix_resolution.sh step1.tiff step2.tiff 300

# Step 3: Remove background
./scripts/remove_background.sh step2.tiff step3.tiff

# Step 4: Deskew (straighten the image)
./scripts/deskew.sh step3.tiff step4.tiff

# Step 5: Convert to grayscale
./scripts/grayscale.sh step4.tiff step5.tiff

# Step 6: Enhance contrast
./scripts/enhance_contrast.sh step5.tiff step6.tiff

# Step 7: Denoise
./scripts/denoise.sh step6.tiff final.tiff
```

#### Direct ImageMagick Commands

If you prefer to run ImageMagick commands directly without the scripts:

```bash
# Step 1: Convert to TIFF
magick input.jpg -compress lzw step1.tiff

# Step 2: Fix resolution to 300 DPI
magick step1.tiff -resample 300 -units PixelsPerInch step2.tiff

# Step 3: Remove background
magick step2.tiff -fuzz 10% -transparent white -background white -alpha remove -auto-level step3.tiff

# Step 4: Deskew
magick step3.tiff -deskew 40% -background white step4.tiff

# Step 5: Grayscale
magick step4.tiff -colorspace Gray step5.tiff

# Step 6: Enhance contrast
magick step5.tiff -auto-level -sigmoidal-contrast 3x50% step6.tiff

# Step 7: Denoise
magick step6.tiff -enhance final.tiff
```

#### All-in-One Command

Run all preprocessing steps in a single ImageMagick command:

```bash
magick input.jpg \
    -compress lzw \
    -resample 300 -units PixelsPerInch \
    -fuzz 10% -transparent white -background white -alpha remove -auto-level \
    -deskew 40% -background white \
    -colorspace Gray \
    -auto-level -sigmoidal-contrast 3x50% \
    -enhance \
    output.tiff
```

### Token-to-Box Mapping

Each word from OCR is tokenized using the model's tokenizer. Sub-tokens inherit the parent word's bounding box:

```
Word: "TOTAL"  Box: [100, 200, 200, 250]
Tokens: ["TO", "##TAL"]
Mapping: 
  - "TO" → [100, 200, 200, 250]
  - "##TAL" → [100, 200, 200, 250]
```

## Testing

The test suite includes both unit tests and integration tests.

### Test Categories

1. **Unit Tests** (`tests/test_cli_unit.py`) - 52 tests
   - CLI argument parsing and validation
   - Device selection logic  
   - Bounding box normalization
   - Heuristic field extraction
   - Output formatting and JSON structure
   - Error handling
   - Preprocessing functions
   - These tests mock OCR/model calls and don't require full dependencies

2. **Integration Tests** (`tests/test_cli_integration.py`) - 21 tests
   - PaddleOCR text detection and recognition
   - Tesseract OCR fallback
   - LayoutLMv3 model loading and inference
   - Full pipeline end-to-end processing
   - Multi-page receipt handling
   - These tests run the actual models and require full dependencies

### Running Tests

```bash
# Run all tests (unit tests will pass, integration tests skip if deps missing)
python -m pytest tests/

# Run only unit tests (no dependencies required beyond numpy, Pillow)
python -m pytest tests/test_cli_unit.py -v

# Run integration tests (requires paddleocr, pytesseract, transformers)
python -m pytest tests/test_cli_integration.py -v

# Run with coverage report
python -m pytest tests/ --cov=. --cov-report=html

# Run excluding slow tests (model loading)
python -m pytest tests/ -m "not slow"

# Run specific test class
python -m pytest tests/test_cli_unit.py::TestNormalizeBoxes -v

# Run specific test
python -m pytest tests/test_cli_unit.py::TestCLIArguments::test_version_command -v
```

### Test Dependencies

**Minimal (unit tests only):**
```bash
pip install pytest pytest-cov numpy Pillow
```

**Full (all tests including integration):**
```bash
pip install -r requirements.txt
```

### Test Coverage

Run with coverage to see which code is tested:

```bash
python -m pytest tests/ --cov=. --cov-report=term-missing
```

## Development

### Fine-tuning LayoutLMv3

See `docs/fine_tuning.md` for instructions on:
- Preparing training data
- Labeling receipts
- Training the model
- Evaluating performance

### Adding New Models

1. Implement model interface in `src/models/base.py`
2. Add model-specific code in `src/models/your_model.py`
3. Register in `src/models/__init__.py`
4. Update configuration options

## Performance

Typical performance on a receipt (1-2 pages, 300 DPI):

| Hardware | OCR Time | Model Inference | Total |
|----------|----------|----------------|-------|
| CPU only | 2-4s | 8-15s | 10-20s |
| GPU (CUDA) | 1-2s | 1-3s | 2-5s |

## Troubleshooting

### CUDA Out of Memory

Reduce batch size or use CPU:
```bash
python cli.py process --image receipt.jpg --device cpu
```

### Low Accuracy

1. Check image quality (300+ DPI recommended)
2. Ensure good lighting and minimal skew
3. Try preprocessing options:
   ```bash
   python cli.py process --image receipt.jpg --denoise --deskew
   ```
4. Consider fine-tuning on your specific receipt formats

## License

[Specify license]

## Contributing

See `CONTRIBUTING.md` for guidelines on contributing to this project.
