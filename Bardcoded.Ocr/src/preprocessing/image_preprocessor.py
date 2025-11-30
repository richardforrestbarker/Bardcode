"""
Image preprocessing utilities for receipt OCR.

Uses ImageMagick CLI (via shell scripts) for optimal image processing and Tesseract compatibility.

Preprocessing pipeline order for best OCR accuracy:
1. Convert to TIFF
2. Fix resolution (300 DPI)
3. Remove background
4. Deskew
5. Grayscale
6. Contrast enhancement
7. Denoise

Shell scripts are located in the scripts/ directory and can be run manually for debugging.
"""

import logging
import subprocess
import tempfile
import os
import shutil
from pathlib import Path
from typing import Optional, Any, TYPE_CHECKING
import numpy as np

if TYPE_CHECKING:
    from ..cli.debug_output import DebugOutputManager

logger = logging.getLogger(__name__)

# Get the scripts directory path
SCRIPTS_DIR = Path(__file__).parent.parent.parent / "scripts"


class ImagePreprocessor:
    """
    Preprocesses receipt images to improve OCR accuracy.
    
    Uses ImageMagick CLI (via shell scripts) for image processing operations.
    
    Preprocessing pipeline order:
    1. Convert to TIFF - optimal format for Tesseract OCR
    2. Fix resolution - ensure 300 DPI for best OCR results
    3. Remove background - isolate text from background noise
    4. Deskew - correct rotation/skew
    5. Grayscale - convert to grayscale
    6. Contrast enhancement - improve text visibility
    7. Denoise - reduce noise while preserving edges
    """
    
    def __init__(
        self,
        target_dpi: int = 300,
        denoise: bool = True,
        deskew: bool = True,
        enhance_contrast: bool = True,
        debug_manager: Optional['DebugOutputManager'] = None
    ):
        """
        Initialize preprocessor.
        
        Args:
            target_dpi: Target DPI for resolution normalization (default 300)
            denoise: Whether to apply denoising
            deskew: Whether to correct skew
            enhance_contrast: Whether to enhance contrast
            debug_manager: Optional DebugOutputManager for saving intermediate steps
        """
        self.target_dpi = target_dpi
        self.denoise = denoise
        self.deskew = deskew
        self.enhance_contrast = enhance_contrast
        self.debug_manager = debug_manager
        
        # Verify ImageMagick is installed
        self._check_imagemagick()
    
    def _check_imagemagick(self):
        """Check if ImageMagick is installed and available."""
        try:
            result = subprocess.run(
                ["convert", "--version"],
                capture_output=True,
                text=True,
                timeout=10
            )
            if result.returncode != 0:
                raise RuntimeError("ImageMagick 'convert' command failed")
            logger.debug(f"ImageMagick version: {result.stdout.split('\n')[0]}")
        except FileNotFoundError:
            raise RuntimeError(
                "ImageMagick is not installed. Please install it:\n"
                "  Ubuntu/Debian: sudo apt-get install imagemagick\n"
                "  macOS: brew install imagemagick\n"
                "  Windows: Download from https://imagemagick.org/script/download.php"
            )
        except subprocess.TimeoutExpired:
            raise RuntimeError("ImageMagick version check timed out")
    
    def _run_script(self, script_name: str, *args) -> bool:
        """
        Run a preprocessing shell script.
        
        Args:
            script_name: Name of the script (without path)
            *args: Arguments to pass to the script
            
        Returns:
            True if successful, False otherwise
        """
        script_path = SCRIPTS_DIR / script_name
        
        if not script_path.exists():
            logger.error(f"Script not found: {script_path}")
            return False
        
        try:
            cmd = [str(script_path)] + [str(arg) for arg in args]
            logger.debug(f"Running: {' '.join(cmd)}")
            
            result = subprocess.run(
                cmd,
                capture_output=True,
                text=True,
                timeout=60
            )
            
            if result.returncode != 0:
                logger.error(f"Script {script_name} failed: {result.stderr}")
                return False
            
            logger.debug(f"Script output: {result.stdout.strip()}")
            return True
            
        except subprocess.TimeoutExpired:
            logger.error(f"Script {script_name} timed out")
            return False
        except Exception as e:
            logger.error(f"Error running script {script_name}: {e}")
            return False
    
    def _run_imagemagick_cmd(self, args: list) -> bool:
        """
        Run an ImageMagick convert command directly.
        
        Args:
            args: Arguments to pass to convert command
            
        Returns:
            True if successful, False otherwise
        """
        try:
            cmd = ["convert"] + args
            logger.debug(f"Running: {' '.join(cmd)}")
            
            result = subprocess.run(
                cmd,
                capture_output=True,
                text=True,
                timeout=60
            )
            
            if result.returncode != 0:
                logger.error(f"ImageMagick command failed: {result.stderr}")
                return False
            
            return True
            
        except subprocess.TimeoutExpired:
            logger.error("ImageMagick command timed out")
            return False
        except Exception as e:
            logger.error(f"Error running ImageMagick: {e}")
            return False
    
    def preprocess(self, image_path: str, page_num: int = 1) -> np.ndarray:
        """
        Preprocess image for OCR using ImageMagick CLI.
        
        Pipeline order: TIFF -> Resolution -> Background removal -> Deskew -> 
                       Grayscale -> Contrast -> Denoise
        
        Args:
            image_path: Path to image file
            page_num: Page number for debug output
            
        Returns:
            Preprocessed image as numpy array (RGB)
        """
        from PIL import Image
        
        logger.info(f"Preprocessing image: {image_path}")
        
        # Create temp directory for intermediate files
        temp_dir = tempfile.mkdtemp(prefix="ocr_preprocess_")
        
        try:
            current_file = image_path
            step = 1
            
            # Step 1: Convert to TIFF
            logger.info(f"Step {step}: Converting to TIFF...")
            next_file = os.path.join(temp_dir, f"step{step}_tiff.tiff")
            if not self._run_imagemagick_cmd([current_file, "-compress", "lzw", next_file]):
                raise RuntimeError(f"Failed to convert '{image_path}' to TIFF")
            current_file = next_file
            if self.debug_manager:
                self._save_debug_image(current_file, "tiff_converted", page_num)
            step += 1
            
            # Step 2: Fix resolution to 300 DPI
            logger.info(f"Step {step}: Fixing resolution to {self.target_dpi} DPI...")
            next_file = os.path.join(temp_dir, f"step{step}_resolution.tiff")
            if not self._run_imagemagick_cmd([
                current_file, "-resample", str(self.target_dpi), 
                "-units", "PixelsPerInch", next_file
            ]):
                raise RuntimeError(f"Failed to fix resolution to {self.target_dpi} DPI")
            current_file = next_file
            if self.debug_manager:
                self._save_debug_image(current_file, "resolution_fixed", page_num)
            step += 1
            
            # Step 3: Remove background
            logger.info(f"Step {step}: Removing background...")
            next_file = os.path.join(temp_dir, f"step{step}_nobg.tiff")
            if not self._run_imagemagick_cmd([
                current_file, "-fuzz", "10%", "-transparent", "white",
                "-background", "white", "-alpha", "remove", "-auto-level", next_file
            ]):
                raise RuntimeError("Failed to remove background")
            current_file = next_file
            if self.debug_manager:
                self._save_debug_image(current_file, "background_removed", page_num)
            step += 1
            
            # Step 4: Deskew (optional)
            if self.deskew:
                logger.info(f"Step {step}: Deskewing...")
                next_file = os.path.join(temp_dir, f"step{step}_deskew.tiff")
                if not self._run_imagemagick_cmd([
                    current_file, "-deskew", "40%", "-background", "white", "+repage", next_file
                ]):
                    raise RuntimeError("Failed to deskew")
                current_file = next_file
                if self.debug_manager:
                    self._save_debug_image(current_file, "deskewed", page_num)
                step += 1
            
            # Step 5: Grayscale
            logger.info(f"Step {step}: Converting to grayscale...")
            next_file = os.path.join(temp_dir, f"step{step}_gray.tiff")
            if not self._run_imagemagick_cmd([
                current_file, "-colorspace", "Gray", next_file
            ]):
                raise RuntimeError("Failed to convert to grayscale")
            current_file = next_file
            if self.debug_manager:
                self._save_debug_image(current_file, "grayscale", page_num)
            step += 1
            
            # Step 6: Contrast enhancement (optional)
            if self.enhance_contrast:
                logger.info(f"Step {step}: Enhancing contrast...")
                next_file = os.path.join(temp_dir, f"step{step}_contrast.tiff")
                if not self._run_imagemagick_cmd([
                    current_file, "-auto-level", "-sigmoidal-contrast", "3x50%", next_file
                ]):
                    raise RuntimeError("Failed to enhance contrast")
                current_file = next_file
                if self.debug_manager:
                    self._save_debug_image(current_file, "contrast_enhanced", page_num)
                step += 1
            
            # Step 7: Denoise (optional)
            if self.denoise:
                logger.info(f"Step {step}: Denoising...")
                next_file = os.path.join(temp_dir, f"step{step}_denoise.tiff")
                if not self._run_imagemagick_cmd([
                    current_file, "-enhance", next_file
                ]):
                    raise RuntimeError("Failed to denoise")
                current_file = next_file
                if self.debug_manager:
                    self._save_debug_image(current_file, "denoised", page_num)
                step += 1
            
            # Load the final preprocessed image
            logger.info("Loading preprocessed image...")
            pil_img = Image.open(current_file)
            if pil_img.mode != 'RGB':
                pil_img = pil_img.convert('RGB')
            result = np.array(pil_img)
            
            # Save final preprocessed debug image
            if self.debug_manager:
                self.debug_manager.save_preprocessed_image(result, page_num)
            
            logger.info("Preprocessing complete")
            return result
            
        finally:
            # Clean up temp directory
            shutil.rmtree(temp_dir, ignore_errors=True)
    
    def _save_debug_image(self, image_path: str, step_name: str, page_num: int):
        """Save a debug image for the current step."""
        if not self.debug_manager:
            return
        try:
            from PIL import Image
            
            pil_img = Image.open(image_path)
            if pil_img.mode != 'RGB':
                pil_img = pil_img.convert('RGB')
            img_array = np.array(pil_img)
            
            # Use debug manager's save method if available
            debug_path = os.path.join(
                self.debug_manager.output_dir if hasattr(self.debug_manager, 'output_dir') else tempfile.gettempdir(),
                f"page_{page_num}_{step_name}.png"
            )
            pil_img.save(debug_path)
            logger.debug(f"Saved debug image: {debug_path}")
        except Exception as e:
            logger.warning(f"Failed to save debug image for {step_name}: {e}")
