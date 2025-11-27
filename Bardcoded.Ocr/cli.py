#!/usr/bin/env python3
"""
Receipt OCR CLI

Command-line interface for processing receipt images with OCR and structured extraction.
"""

import argparse
import json
import sys
import logging
from pathlib import Path
from typing import List, Optional, Dict, Any

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

# Try to import optional dependencies
try:
    import numpy as np
    HAS_NUMPY = True
except ImportError:
    HAS_NUMPY = False
    logger.warning("NumPy not available")

try:
    from PIL import Image
    HAS_PIL = True
except ImportError:
    HAS_PIL = False
    logger.warning("Pillow not available")


def check_dependencies():
    """Check if required dependencies are available."""
    if not HAS_NUMPY:
        raise ImportError("NumPy is required. Install with: pip install numpy")
    if not HAS_PIL:
        raise ImportError("Pillow is required. Install with: pip install Pillow")


def get_device(device_str: str) -> str:
    """Resolve device string to actual device."""
    if device_str == "auto":
        try:
            import torch
            return "cuda" if torch.cuda.is_available() else "cpu"
        except ImportError:
            return "cpu"
    return device_str


def load_image(image_path: str):
    """Load an image file and return as numpy array."""
    check_dependencies()
    img = Image.open(image_path)
    if img.mode != 'RGB':
        img = img.convert('RGB')
    return np.array(img)


def preprocess_image(
    image,
    denoise: bool = False,
    deskew: bool = False
):
    """
    Preprocess image for OCR.
    
    Args:
        image: Input image as numpy array
        denoise: Whether to apply denoising
        deskew: Whether to deskew image
        
    Returns:
        Preprocessed image
    """
    try:
        import cv2
        
        # Convert to grayscale for preprocessing
        if len(image.shape) == 3:
            gray = cv2.cvtColor(image, cv2.COLOR_RGB2GRAY)
        else:
            gray = image.copy()
        
        # Apply denoising
        if denoise:
            gray = cv2.bilateralFilter(gray, 9, 75, 75)
            logger.info("Applied bilateral denoising filter")
        
        # Apply deskewing
        if deskew:
            # Simple deskew using moments
            coords = np.column_stack(np.where(gray < 200))
            if len(coords) > 100:
                angle = cv2.minAreaRect(coords)[-1]
                if angle < -45:
                    angle = -(90 + angle)
                else:
                    angle = -angle
                
                if abs(angle) > 0.5:  # Only correct if angle is significant
                    (h, w) = gray.shape[:2]
                    center = (w // 2, h // 2)
                    M = cv2.getRotationMatrix2D(center, angle, 1.0)
                    gray = cv2.warpAffine(
                        gray, M, (w, h),
                        flags=cv2.INTER_CUBIC,
                        borderMode=cv2.BORDER_REPLICATE
                    )
                    logger.info(f"Applied deskew correction: {angle:.2f} degrees")
        
        # Enhance contrast using CLAHE
        clahe = cv2.createCLAHE(clipLimit=2.0, tileGridSize=(8, 8))
        enhanced = clahe.apply(gray)
        
        # Convert back to RGB for OCR
        return cv2.cvtColor(enhanced, cv2.COLOR_GRAY2RGB)
        
    except ImportError:
        logger.warning("OpenCV not available, skipping preprocessing")
        return image


def run_ocr(
    image: Any,
    ocr_engine: str = "paddle",
    device: str = "cpu"
) -> List[Dict[str, Any]]:
    """
    Run OCR on image using specified engine.
    
    Args:
        image: Input image as numpy array
        ocr_engine: OCR engine to use ('paddle' or 'tesseract')
        device: Device for inference
        
    Returns:
        List of detected words with boxes and confidences
    """
    words = []
    
    if ocr_engine == "paddle":
        try:
            from paddleocr import PaddleOCR
            
            use_gpu = device == "cuda"
            ocr = PaddleOCR(
                use_angle_cls=True,
                lang='en',
                use_gpu=use_gpu,
                show_log=False
            )
            
            result = ocr.ocr(image, cls=True)
            
            if result and result[0]:
                for line in result[0]:
                    box_points = line[0]  # 4 corner points
                    text = line[1][0]     # recognized text
                    confidence = float(line[1][1])  # confidence score
                    
                    # Convert 4-point box to [x0, y0, x1, y1]
                    x_coords = [p[0] for p in box_points]
                    y_coords = [p[1] for p in box_points]
                    box = [
                        int(min(x_coords)),
                        int(min(y_coords)),
                        int(max(x_coords)),
                        int(max(y_coords))
                    ]
                    
                    words.append({
                        'text': text,
                        'box': box,
                        'confidence': confidence
                    })
            
            logger.info(f"PaddleOCR detected {len(words)} text regions")
            
        except ImportError:
            logger.warning("PaddleOCR not available, falling back to Tesseract")
            return run_ocr(image, "tesseract", device)
        except Exception as e:
            logger.error(f"PaddleOCR error: {e}, falling back to Tesseract")
            return run_ocr(image, "tesseract", device)
    
    elif ocr_engine == "tesseract":
        try:
            import pytesseract
            
            # Get word-level data
            data = pytesseract.image_to_data(
                image,
                lang='eng',
                config='--psm 6',
                output_type=pytesseract.Output.DICT
            )
            
            for i in range(len(data['text'])):
                text = data['text'][i].strip()
                if not text:
                    continue
                
                conf = data['conf'][i]
                if conf < 0:  # Tesseract returns -1 for invalid entries
                    continue
                
                box = [
                    int(data['left'][i]),
                    int(data['top'][i]),
                    int(data['left'][i] + data['width'][i]),
                    int(data['top'][i] + data['height'][i])
                ]
                
                words.append({
                    'text': text,
                    'box': box,
                    'confidence': conf / 100.0  # Tesseract returns 0-100
                })
            
            logger.info(f"Tesseract detected {len(words)} words")
            
        except ImportError:
            logger.error("Tesseract not available")
            raise RuntimeError("No OCR engine available. Please install paddleocr or pytesseract.")
    
    return words


def normalize_boxes(
    words: List[Dict[str, Any]],
    image_width: int,
    image_height: int,
    scale: int = 1000
) -> List[Dict[str, Any]]:
    """
    Normalize bounding boxes to 0-1000 scale for LayoutLM.
    
    Args:
        words: List of words with boxes
        image_width: Image width
        image_height: Image height
        scale: Normalization scale (default 1000)
        
    Returns:
        Words with normalized boxes
    """
    normalized = []
    for word in words:
        box = word['box']
        normalized_box = [
            int(box[0] * scale / image_width),
            int(box[1] * scale / image_height),
            int(box[2] * scale / image_width),
            int(box[3] * scale / image_height)
        ]
        # Clamp values to valid range
        normalized_box = [max(0, min(scale, x)) for x in normalized_box]
        
        normalized.append({
            'text': word['text'],
            'box': normalized_box,
            'confidence': word['confidence']
        })
    
    return normalized


def run_layoutlm_inference(
    image: Any,
    words: List[Dict[str, Any]],
    model_name: str = "microsoft/layoutlmv3-base",
    device: str = "cpu"
) -> Dict[str, Any]:
    """
    Run LayoutLMv3 model inference for field extraction.
    
    Args:
        image: Input image
        words: OCR words with boxes
        model_name: HuggingFace model name
        device: Device for inference
        
    Returns:
        Model predictions with entity labels
    """
    try:
        import torch
        from transformers import AutoProcessor, AutoModelForTokenClassification
        from PIL import Image as PILImage
        
        logger.info(f"Loading LayoutLMv3 model: {model_name}")
        
        # Load processor and model
        processor = AutoProcessor.from_pretrained(model_name, apply_ocr=False)
        model = AutoModelForTokenClassification.from_pretrained(model_name)
        model.to(device)
        model.eval()
        
        # Prepare input
        pil_image = PILImage.fromarray(image)
        
        # Extract text and boxes from words
        texts = [w['text'] for w in words]
        boxes = [w['box'] for w in words]
        
        if not texts:
            logger.warning("No text detected for model inference")
            return {"predictions": [], "labels": []}
        
        # Process with LayoutLMv3 processor
        encoding = processor(
            pil_image,
            texts,
            boxes=boxes,
            return_tensors="pt",
            truncation=True,
            max_length=512,
            padding="max_length"
        )
        
        # Move to device
        for k, v in encoding.items():
            if isinstance(v, torch.Tensor):
                encoding[k] = v.to(device)
        
        # Run inference
        with torch.no_grad():
            outputs = model(**encoding)
        
        # Get predictions
        logits = outputs.logits
        predictions = torch.argmax(logits, dim=-1).squeeze().tolist()
        
        # Get confidence scores
        probs = torch.softmax(logits, dim=-1)
        confidences = probs.max(dim=-1).values.squeeze().tolist()
        
        # Handle single prediction case
        if not isinstance(predictions, list):
            predictions = [predictions]
            confidences = [confidences]
        
        logger.info(f"LayoutLMv3 inference completed with {len(predictions)} predictions")
        
        return {
            "predictions": predictions,
            "confidences": confidences,
            "label_names": model.config.id2label if hasattr(model.config, 'id2label') else {}
        }
        
    except ImportError as e:
        logger.warning(f"Transformers not available: {e}. Using heuristic extraction.")
        return {"predictions": [], "labels": []}
    except Exception as e:
        logger.warning(f"LayoutLMv3 inference failed: {e}. Using heuristic extraction.")
        return {"predictions": [], "labels": []}


def extract_fields_heuristic(
    words: List[Dict[str, Any]]
) -> Dict[str, Any]:
    """
    Extract receipt fields using heuristics when model is not available.
    
    Args:
        words: OCR words with boxes
        
    Returns:
        Extracted fields
    """
    import re
    from datetime import datetime
    
    # Sort words by position (top to bottom, left to right)
    sorted_words = sorted(words, key=lambda w: (w['box'][1], w['box'][0]))
    
    # Full text for searching
    full_text = ' '.join(w['text'] for w in sorted_words)
    
    result = {
        "vendor_name": None,
        "date": None,
        "total_amount": None,
        "subtotal": None,
        "tax_amount": None,
        "currency": None,
        "line_items": []
    }
    
    # Extract vendor name (usually first few words at top)
    if sorted_words:
        top_words = [w for w in sorted_words if w['box'][1] < sorted_words[0]['box'][1] + 100]
        if top_words:
            vendor_text = ' '.join(w['text'] for w in top_words[:3])
            all_boxes = [w['box'] for w in top_words[:3]]
            result["vendor_name"] = {
                "value": vendor_text,
                "confidence": sum(w['confidence'] for w in top_words[:3]) / len(top_words[:3]),
                "box": {
                    "x0": min(b[0] for b in all_boxes),
                    "y0": min(b[1] for b in all_boxes),
                    "x1": max(b[2] for b in all_boxes),
                    "y1": max(b[3] for b in all_boxes)
                }
            }
    
    # Extract date
    date_patterns = [
        r'(\d{1,2}[/-]\d{1,2}[/-]\d{2,4})',
        r'(\d{4}[/-]\d{1,2}[/-]\d{1,2})',
        r'((?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]* \d{1,2},? \d{4})'
    ]
    for pattern in date_patterns:
        match = re.search(pattern, full_text, re.IGNORECASE)
        if match:
            date_str = match.group(1)
            # Find the word containing the date
            for w in words:
                if date_str in w['text'] or w['text'] in date_str:
                    result["date"] = {
                        "value": date_str,
                        "confidence": w['confidence'],
                        "box": {
                            "x0": w['box'][0],
                            "y0": w['box'][1],
                            "x1": w['box'][2],
                            "y1": w['box'][3]
                        }
                    }
                    break
            break
    
    # Extract amounts
    amount_pattern = r'\$?\s*(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)'
    
    # Find total (look for TOTAL, GRAND TOTAL, etc.)
    total_keywords = ['total', 'grand total', 'amount due', 'balance']
    for i, w in enumerate(words):
        text_lower = w['text'].lower()
        if any(kw in text_lower for kw in total_keywords):
            # Look for amount nearby
            for j in range(max(0, i-2), min(len(words), i+5)):
                match = re.search(amount_pattern, words[j]['text'])
                if match:
                    result["total_amount"] = {
                        "value": match.group(1).replace(',', ''),
                        "confidence": words[j]['confidence'],
                        "box": {
                            "x0": words[j]['box'][0],
                            "y0": words[j]['box'][1],
                            "x1": words[j]['box'][2],
                            "y1": words[j]['box'][3]
                        }
                    }
                    break
            if result["total_amount"]:
                break
    
    # Find subtotal
    subtotal_keywords = ['subtotal', 'sub total', 'sub-total']
    for i, w in enumerate(words):
        text_lower = w['text'].lower()
        if any(kw in text_lower for kw in subtotal_keywords):
            for j in range(max(0, i-2), min(len(words), i+5)):
                match = re.search(amount_pattern, words[j]['text'])
                if match:
                    result["subtotal"] = {
                        "value": match.group(1).replace(',', ''),
                        "confidence": words[j]['confidence'],
                        "box": {
                            "x0": words[j]['box'][0],
                            "y0": words[j]['box'][1],
                            "x1": words[j]['box'][2],
                            "y1": words[j]['box'][3]
                        }
                    }
                    break
            if result["subtotal"]:
                break
    
    # Find tax
    tax_keywords = ['tax', 'vat', 'gst', 'hst']
    for i, w in enumerate(words):
        text_lower = w['text'].lower()
        if any(kw in text_lower for kw in tax_keywords):
            for j in range(max(0, i-2), min(len(words), i+5)):
                match = re.search(amount_pattern, words[j]['text'])
                if match:
                    result["tax_amount"] = {
                        "value": match.group(1).replace(',', ''),
                        "confidence": words[j]['confidence'],
                        "box": {
                            "x0": words[j]['box'][0],
                            "y0": words[j]['box'][1],
                            "x1": words[j]['box'][2],
                            "y1": words[j]['box'][3]
                        }
                    }
                    break
            if result["tax_amount"]:
                break
    
    # Detect currency
    if '$' in full_text or 'USD' in full_text:
        result["currency"] = {"value": "USD", "confidence": 0.9, "box": None}
    elif '€' in full_text or 'EUR' in full_text:
        result["currency"] = {"value": "EUR", "confidence": 0.9, "box": None}
    elif '£' in full_text or 'GBP' in full_text:
        result["currency"] = {"value": "GBP", "confidence": 0.9, "box": None}
    
    return result


def process_receipt(
    image_paths: List[str],
    output_path: Optional[str] = None,
    model_name: str = "microsoft/layoutlmv3-base",
    ocr_engine: str = "paddle",
    device: str = "auto",
    denoise: bool = False,
    deskew: bool = False,
    job_id: Optional[str] = None
) -> dict:
    """
    Process receipt images and extract structured data.
    
    Args:
        image_paths: List of paths to receipt image files
        output_path: Optional path to write JSON output
        model_name: Model name or path for LayoutLM
        ocr_engine: OCR engine to use ('paddle' or 'tesseract')
        device: Device for inference ('auto', 'cuda', or 'cpu')
        denoise: Apply denoising preprocessing
        deskew: Apply deskewing preprocessing
        job_id: Optional job identifier
        
    Returns:
        Dictionary containing extracted receipt data
    """
    logger.info(f"Processing {len(image_paths)} image(s)...")
    logger.info(f"Model: {model_name}")
    logger.info(f"OCR Engine: {ocr_engine}")
    
    # Resolve device
    actual_device = get_device(device)
    logger.info(f"Using device: {actual_device}")
    
    result = {
        "job_id": job_id or f"job-{hash(tuple(image_paths)) % 100000:05d}",
        "status": "done",
        "pages": [],
        "vendor_name": None,
        "date": None,
        "total_amount": None,
        "subtotal": None,
        "tax_amount": None,
        "currency": None,
        "line_items": []
    }
    
    all_words = []
    
    try:
        for page_num, image_path in enumerate(image_paths):
            logger.info(f"Processing page {page_num + 1}: {image_path}")
            
            # Load image
            image = load_image(image_path)
            img_height, img_width = image.shape[:2]
            logger.info(f"Image size: {img_width}x{img_height}")
            
            # Preprocess
            processed_image = preprocess_image(image, denoise=denoise, deskew=deskew)
            
            # Run OCR
            words = run_ocr(processed_image, ocr_engine, actual_device)
            
            # Normalize boxes
            normalized_words = normalize_boxes(words, img_width, img_height)
            
            # Build raw OCR text
            raw_text = ' '.join(w['text'] for w in words)
            
            # Add page result
            result["pages"].append({
                "page_number": page_num + 1,
                "raw_ocr_text": raw_text,
                "words": [
                    {
                        "text": w['text'],
                        "box": {
                            "x0": w['box'][0],
                            "y0": w['box'][1],
                            "x1": w['box'][2],
                            "y1": w['box'][3]
                        },
                        "confidence": w['confidence']
                    }
                    for w in normalized_words
                ]
            })
            
            all_words.extend(normalized_words)
        
        # Try LayoutLM inference if model available
        if all_words:
            first_image = load_image(image_paths[0])
            model_result = run_layoutlm_inference(
                first_image,
                all_words,
                model_name,
                actual_device
            )
            
            # If model inference failed or no predictions, use heuristics
            if not model_result.get("predictions"):
                logger.info("Using heuristic field extraction")
                fields = extract_fields_heuristic(all_words)
            else:
                # TODO: Parse model predictions to extract fields
                # For now, still use heuristics as fallback
                fields = extract_fields_heuristic(all_words)
            
            # Update result with extracted fields
            for field_name in ["vendor_name", "date", "total_amount", "subtotal", "tax_amount", "currency"]:
                if fields.get(field_name):
                    result[field_name] = fields[field_name]
            
            result["line_items"] = fields.get("line_items", [])
        
    except Exception as e:
        logger.error(f"Error processing receipt: {e}")
        result["status"] = "failed"
        result["error"] = str(e)
    
    # Write output if specified
    if output_path:
        output_file = Path(output_path)
        output_file.parent.mkdir(parents=True, exist_ok=True)
        with open(output_file, 'w') as f:
            json.dump(result, f, indent=2)
        logger.info(f"Results written to {output_path}")
    
    return result


def main():
    """Main entry point for CLI."""
    parser = argparse.ArgumentParser(
        description="Process receipt images with OCR and structured extraction"
    )
    
    subparsers = parser.add_subparsers(dest="command", help="Command to execute")
    
    # Process command
    process_parser = subparsers.add_parser("process", help="Process receipt images")
    process_parser.add_argument(
        "--image",
        "-i",
        action="append",
        required=True,
        help="Path to receipt image (can be specified multiple times for multi-page receipts)"
    )
    process_parser.add_argument(
        "--output",
        "-o",
        help="Path to write JSON output (prints to stdout if not specified)"
    )
    process_parser.add_argument(
        "--model",
        "-m",
        default="microsoft/layoutlmv3-base",
        help="Model name or path (default: microsoft/layoutlmv3-base)"
    )
    process_parser.add_argument(
        "--ocr-engine",
        choices=["paddle", "tesseract"],
        default="paddle",
        help="OCR engine to use (default: paddle)"
    )
    process_parser.add_argument(
        "--device",
        choices=["auto", "cuda", "cpu"],
        default="auto",
        help="Device for inference (default: auto)"
    )
    process_parser.add_argument(
        "--denoise",
        action="store_true",
        help="Apply denoising preprocessing"
    )
    process_parser.add_argument(
        "--deskew",
        action="store_true",
        help="Apply deskewing preprocessing"
    )
    process_parser.add_argument(
        "--job-id",
        help="Job identifier for tracking"
    )
    
    # Version command
    version_parser = subparsers.add_parser("version", help="Show version information")
    
    args = parser.parse_args()
    
    if args.command == "process":
        try:
            result = process_receipt(
                image_paths=args.image,
                output_path=args.output,
                model_name=args.model,
                ocr_engine=args.ocr_engine,
                device=args.device,
                denoise=args.denoise,
                deskew=args.deskew,
                job_id=args.job_id
            )
            
            if not args.output:
                print(json.dumps(result, indent=2))
            
            sys.exit(0)
        except Exception as e:
            print(f"Error processing receipt: {e}", file=sys.stderr)
            sys.exit(1)
    
    elif args.command == "version":
        print("Receipt OCR Service v0.0.0")
        print("PaddleOCR + LayoutLMv3")
        sys.exit(0)
    
    else:
        parser.print_help()
        sys.exit(1)


if __name__ == "__main__":
    main()
