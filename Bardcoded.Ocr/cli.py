#!/usr/bin/env python3
"""
Receipt OCR CLI

Command-line interface for processing receipt images with OCR and structured extraction.

This is the main entry point. The CLI logic is split into modular components:
- src/cli/args.py: Argument parsing
- src/cli/commands.py: Command implementations
- src/cli/utils.py: Utility functions
"""

import json
import sys
import logging

# Import required functions for tests
from src.receipt_processor import (
    get_device,
    load_image,
    preprocess_image,
    normalize_boxes,
    extract_fields_heuristic,
    process_receipt,
    run_ocr,
    run_layoutlm_inference,
)


# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger("Bardcoded Python OCR CLI")


def main():
    """Main entry point for CLI."""
    # Import from modular components
    from src.cli.args import parse_args
    from src.cli.commands import process_command, version_command
    
    args, parser = parse_args()
    
    if args.command == "process":
        try:
            result = process_command(
                image_paths=args.images,
                output_path=args.output,
                model_name=args.model,
                ocr_engine=args.ocr_engine,
                device=args.device,
                denoise=args.denoise,
                deskew=args.deskew,
                job_id=args.job_id,
                skip_model=args.skip_model,
                verbose=args.verbose,
                debug=args.debug,
                debug_output_dir=args.debug_output_dir,
                fuzz_percent=args.fuzz_percent,
                deskew_threshold=args.deskew_threshold,
                contrast_type=args.contrast_type,
                contrast_strength=args.contrast_strength,
                contrast_midpoint=args.contrast_midpoint
            )
            
            if not args.output:
                print(json.dumps(result, indent=2))
            
            sys.exit(0)
        except Exception as e:
            print(f"Error processing receipt: {e}", file=sys.stderr)
            sys.exit(1)
    
    elif args.command == "version":
        version_command()
        sys.exit(0)
    
    else:
        parser.print_help()
        sys.exit(1)


if __name__ == "__main__":
    main()
