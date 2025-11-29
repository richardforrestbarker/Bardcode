"""
CLI argument parsing module.

Handles command-line argument parsing for the Receipt OCR CLI.
"""

import argparse
from typing import Tuple


def create_argument_parser() -> argparse.ArgumentParser:
    """
    Create and configure the argument parser for the CLI.
    
    Returns:
        Configured ArgumentParser instance
    """
    parser = argparse.ArgumentParser(
        description="Process receipt images with OCR and structured extraction",
        prog="receipt-ocr"
    )
    
    subparsers = parser.add_subparsers(dest="command", help="Command to execute")
    
    # Process command
    process_parser = subparsers.add_parser("process", help="Process receipt images")
    process_parser.add_argument(
        "--image",
        "-i",
        action="append",
        required=True,
        dest="images",
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
    process_parser.add_argument(
        "--skip-model",
        action="store_true",
        help="Skip LayoutLM model inference (use only heuristic extraction)"
    )
    process_parser.add_argument(
        "--verbose",
        "-v",
        action="store_true",
        help="Enable verbose logging"
    )
    
    # Version command
    subparsers.add_parser("version", help="Show version information")
    
    return parser


def parse_args(args=None) -> Tuple[argparse.Namespace, argparse.ArgumentParser]:
    """
    Parse command-line arguments.
    
    Args:
        args: Optional list of arguments (uses sys.argv if None)
        
    Returns:
        Tuple of (parsed arguments namespace, parser instance)
    """
    parser = create_argument_parser()
    parsed_args = parser.parse_args(args)
    return parsed_args, parser
