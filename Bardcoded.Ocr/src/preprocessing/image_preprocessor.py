"""
Image preprocessing utilities for receipt OCR.

Includes denoising, deskewing, normalization, and other image enhancement operations.
Uses ImageMagick (via Wand) for optimal image processing and Tesseract compatibility.

Preprocessing pipeline order for best OCR accuracy:
1. Convert to TIFF
2. Fix resolution (300 DPI)
3. Remove background
4. Deskew
5. Grayscale
6. Contrast enhancement
7. Denoise
"""

import logging
import tempfile
import os
import io
from typing import Tuple, Optional, Any, TYPE_CHECKING
import numpy as np

if TYPE_CHECKING:
    from ..cli.debug_output import DebugOutputManager

logger = logging.getLogger(__name__)


class ImagePreprocessor:
    """
    Preprocesses receipt images to improve OCR accuracy.
    
    Uses ImageMagick (via Wand) for image processing operations.
    
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
    
    def preprocess(self, image_path: str, page_num: int = 1) -> np.ndarray:
        """
        Preprocess image for OCR using ImageMagick pipeline.
        
        Pipeline order: TIFF -> Resolution -> Background removal -> Deskew -> 
                       Grayscale -> Contrast -> Denoise
        
        Args:
            image_path: Path to image file
            page_num: Page number for debug output
            
        Returns:
            Preprocessed image as numpy array (RGB)
        """
        try:
            from wand.image import Image as WandImage
            from PIL import Image
            
            with WandImage(filename=image_path) as img:
                logger.info(f"Image size {img.width}x{img.height}")
                # 1. Convert to TIFF format (internal processing)
                
                logger.info("Converting image to TIFF format")
                img = self._convert_to_tiff(img)
                if self.debug_manager:
                    self._save_debug_wand_image(img, "tiff_converted", page_num)
                logger.info("Converted image to TIFF format")
                
                # 2. Fix resolution to 300 DPI
                img = self._fix_resolution(img)
                if self.debug_manager:
                    self._save_debug_wand_image(img, "resolution_fixed", page_num)
                logger.info("Fixed image resolution to target DPI")

                # 3. Remove background
                img = self._remove_background(img)
                if self.debug_manager:
                    self._save_debug_wand_image(img, "background_removed", page_num)
                logger.info("Removed image background")

                # 4. Deskew
                if self.deskew:
                    img = self._deskew_wand(img)
                    if self.debug_manager:
                        self._save_debug_wand_image(img, "deskewed", page_num)
                    logger.info("Applied deskew correction")
                
                # 5. Convert to grayscale
                img = self._grayscale(img)
                if self.debug_manager:
                    self._save_debug_wand_image(img, "grayscale", page_num)
                logger.info("Converted image to grayscale")
                
                # 6. Enhance contrast
                if self.enhance_contrast:
                    img = self._enhance_contrast_wand(img)
                    if self.debug_manager:
                        self._save_debug_wand_image(img, "contrast_enhanced", page_num)
                logger.info("Enhanced image contrast")
                # 7. Denoise
                if self.denoise:
                    img = self._denoise_wand(img)
                    if self.debug_manager:
                        self._save_debug_wand_image(img, "denoised", page_num)
                logger.info("Applied denoising to image")

                # Convert to numpy array for OCR processing
                img.format = 'png'
                blob = img.make_blob()
            
            # Convert blob to numpy array via PIL
           
            pil_img = Image.open(io.BytesIO(blob))
            if pil_img.mode != 'RGB':
                pil_img = pil_img.convert('RGB')
            result = np.array(pil_img)
            
            # Save final preprocessed debug image
            if self.debug_manager:
                self.debug_manager.save_preprocessed_image(result, page_num)
            logger.info("Completed image preprocessing")
            return result
            
        except ImportError as e:
            logger.warning(f"Wand (ImageMagick) not available: {e}. Falling back to OpenCV.")
            return self._preprocess_opencv_fallback(image_path, page_num)
    
    def _save_debug_wand_image(self, img, step_name: str, page_num: int):
        """Save a Wand image for debugging purposes."""
        if not self.debug_manager:
            return
        try:
            from PIL import Image
            
            # Clone to avoid modifying original
            with img.clone() as debug_img:
                debug_img.format = 'png'
                blob = debug_img.make_blob()
            
            pil_img = Image.open(io.BytesIO(blob))
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
    
    def _preprocess_opencv_fallback(self, image_path: str, page_num: int = 1) -> np.ndarray:
        """Fallback preprocessing using OpenCV when ImageMagick is not available."""
        try:
            from PIL import Image
            import cv2
            
            img = Image.open(image_path)
            img_array = np.array(img)
            
            return self.preprocess_array(img_array, page_num=page_num)
            
        except ImportError as e:
            logger.error(f"Required dependencies not available: {e}")
            raise ImportError(
                "ImageMagick (Wand) or OpenCV and Pillow are required. "
                "Install with: pip install Wand opencv-python Pillow"
            )
    
    # ==================== ImageMagick (Wand) Methods ====================
    
    def _convert_to_tiff(self, img):
        """
        Convert image to TIFF format for optimal Tesseract OCR processing.
        
        TIFF is the preferred format for Tesseract as it provides lossless
        compression and supports high bit depths.
        
        Args:
            img: Wand Image object
            
        Returns:
            Wand Image object in TIFF format
        """
        try:
            # Set format to TIFF internally
            img.format = 'tiff'
            # Use LZW compression for smaller file size while maintaining quality
            img.compression = 'lzw'
            logger.debug("Converted image to TIFF format")
            return img
        except Exception as e:
            logger.warning(f"TIFF conversion failed: {e}")
            return img
    
    def _fix_resolution(self, img):
        """
        Ensure image is at 300 DPI for optimal OCR results.
        
        300 DPI is the recommended resolution for Tesseract OCR.
        If the image resolution is lower, it will be upscaled.
        
        Args:
            img: Wand Image object
            
        Returns:
            Wand Image object at 300 DPI
        """
        try:
            current_dpi = img.resolution
            target = self.target_dpi
            
            # Get current resolution (x, y)
            if current_dpi and current_dpi[0] > 0:
                current_x_dpi = current_dpi[0]
                # this is one reason I don't like python -- ternary operators are clunky and obfuscate meaning.
                current_y_dpi = current_dpi[1] if len(current_dpi) > 1 else current_dpi[0]
            else:
                # Default to 72 DPI if current DPI is not specified
                current_x_dpi = 72
                current_y_dpi = 72
            
            # Calculate scale factors
            scale_x = target / current_x_dpi
            scale_y = target / current_y_dpi
            
            # Only resize if needed (allow 5% tolerance)
            if abs(scale_x - 1.0) > 0.05 or abs(scale_y - 1.0) > 0.05:
                new_width = int(img.width * scale_x)
                new_height = int(img.height * scale_y)
                logger.debug(f"Resizing image from {img.width}x{img.height} to {new_width}x{new_height} and {current_x_dpi}x{current_y_dpi}DPI to {self.target_dpi}x{self.target_dpi} DPI")
                # Use Lanczos filter for high-quality resampling
                img.resize(new_width, new_height, filter='lanczos')
                logger.debug(f"Resized image from {current_x_dpi}x{current_y_dpi} DPI to {target} DPI")
            
            # Set the resolution metadata
            img.resolution = (target, target)
            img.units = 'pixelsperinch'
            
            logger.debug(f"Set image resolution to {target} DPI")
            return img
            
        except Exception as e:
            logger.warning(f"Resolution fix failed: {e}")
            return img
    
    def _remove_background(self, img):
        """
        Remove background from image to isolate text.
        
        Uses ImageMagick's background removal capabilities to
        clean up the image and improve text detection.
        
        Args:
            img: Wand Image object
            
        Returns:
            Wand Image object with background removed
        """
        try:
            from wand.color import Color
            
            # Use fuzz factor to handle slight color variations
            # The fuzz factor (as percentage) determines how similar colors
            # must be to be considered the "same" for background removal
            fuzz_percent = 10.0
            
            # Try to detect and remove the dominant background color
            # This works well for receipts which typically have white/light backgrounds
            
            # First, try to make the background transparent
            try:
                # Set fuzz for background removal
                img.fuzz = img.quantum_range * (fuzz_percent / 100.0)
                
                # Remove white/light background (common for receipts)
                img.transparent_color(Color('white'), alpha=0.0, fuzz=img.fuzz)
                
                # Flatten to white background to ensure consistent processing
                img.background_color = Color('white')
                img.alpha_channel = 'remove'
                
            except Exception as inner_e:
                logger.debug(f"Transparent background removal failed: {inner_e}")
                # Fall back to simple normalization
                img.normalize()
            
            # Apply auto-level to improve contrast after background removal
            img.auto_level()
            
            logger.debug("Applied background removal")
            return img
            
        except Exception as e:
            logger.warning(f"Background removal failed: {e}")
            return img
    
    def _deskew_wand(self, img):
        """
        Detect and correct image skew using ImageMagick.
        
        Uses ImageMagick's deskew function which analyzes the image
        and rotates it to straighten text lines.
        
        Args:
            img: Wand Image object
            
        Returns:
            Deskewed Wand Image object
        """
        try:
            from wand.color import Color
            
            # ImageMagick deskew threshold (0.0 to 1.0)
            # Lower values are more aggressive in detecting skew
            # 0.4 is a good balance for receipt images
            threshold = 0.4
            
            # Apply deskew
            img.deskew(threshold * img.quantum_range)
            
            # Set background to white for any new pixels from rotation
            img.background_color = Color('white')
            
            logger.debug("Applied deskew correction using ImageMagick")
            return img
            
        except Exception as e:
            logger.warning(f"ImageMagick deskew failed: {e}")
            return img
    
    def _grayscale(self, img):
        """
        Convert image to grayscale using ImageMagick.
        
        Args:
            img: Wand Image object
            
        Returns:
            Grayscale Wand Image object
        """
        try:
            # Convert to grayscale colorspace
            img.type = 'grayscale'
            logger.debug("Converted to grayscale")
            return img
        except Exception as e:
            logger.warning(f"Grayscale conversion failed: {e}")
            return img
    
    def _enhance_contrast_wand(self, img):
        """
        Enhance image contrast using ImageMagick.
        
        Uses a combination of techniques for optimal text visibility:
        - Auto-level for dynamic range optimization
        - Normalize for consistent contrast
        - Sigmoidal contrast for non-linear enhancement
        
        Args:
            img: Wand Image object
            
        Returns:
            Contrast-enhanced Wand Image object
        """
        try:
            # Auto-level to stretch the histogram
            img.auto_level()
            
            # Apply sigmoidal contrast for better text visibility
            # sharpen=True increases contrast, contrast=3 is moderate enhancement
            # midpoint=50% targets the middle tones
            img.sigmoidal_contrast(sharpen=True, strength=3, midpoint=0.5 * img.quantum_range)
            
            logger.debug("Applied contrast enhancement using ImageMagick")
            return img
            
        except Exception as e:
            logger.warning(f"ImageMagick contrast enhancement failed: {e}")
            return img
    
    def _denoise_wand(self, img):
        """
        Apply denoising using ImageMagick.
        
        Uses ImageMagick's enhance filter which reduces noise
        while attempting to preserve edges.
        
        Args:
            img: Wand Image object
            
        Returns:
            Denoised Wand Image object
        """
        try:
            # Use enhance filter for noise reduction
            # This is similar to a median filter but preserves edges better
            img.enhance()
            
            logger.debug("Applied denoising using ImageMagick enhance filter")
            return img
            
        except Exception as e:
            logger.warning(f"ImageMagick denoising failed: {e}")
            return img
    
    # ==================== OpenCV Fallback Methods ====================
    
    def _remove_background_opencv(self, image: np.ndarray) -> np.ndarray:
        """
        Remove background using OpenCV (fallback method).
        
        Uses adaptive thresholding and morphological operations
        to separate text from background.
        
        Args:
            image: Input image (RGB or grayscale)
            
        Returns:
            Image with background removed/normalized
        """
        try:
            import cv2
            
            # Convert to grayscale if needed
            if len(image.shape) == 3:
                gray = cv2.cvtColor(image, cv2.COLOR_RGB2GRAY)
            else:
                gray = image.copy()
            
            # Apply Gaussian blur to reduce noise
            blurred = cv2.GaussianBlur(gray, (5, 5), 0)
            
            # Use Otsu's thresholding to separate foreground from background
            _, binary = cv2.threshold(blurred, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
            
            # Apply morphological operations to clean up
            kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (3, 3))
            cleaned = cv2.morphologyEx(binary, cv2.MORPH_CLOSE, kernel)
            
            # Invert if text is white on dark background
            if np.mean(cleaned) < 127:
                cleaned = cv2.bitwise_not(cleaned)
            
            # Convert back to 3-channel for consistency
            result = cv2.cvtColor(cleaned, cv2.COLOR_GRAY2RGB)
            
            logger.debug("Applied background removal using OpenCV")
            return result
            
        except Exception as e:
            logger.warning(f"OpenCV background removal failed: {e}")
            return image
    
    def _denoise(self, image: np.ndarray) -> np.ndarray:
        """
        Apply denoising filter using OpenCV.
        
        Uses bilateral filter to preserve edges while removing noise.
        
        Args:
            image: Grayscale image
            
        Returns:
            Denoised image
        """
        try:
            import cv2
            
            # Bilateral filter parameters:
            # d=9: Diameter of each pixel neighborhood
            # sigmaColor=75: Filter sigma in the color space
            # sigmaSpace=75: Filter sigma in the coordinate space
            denoised = cv2.bilateralFilter(image, 9, 75, 75)
            logger.debug("Applied bilateral denoising filter")
            return denoised
            
        except Exception as e:
            logger.warning(f"Denoising failed: {e}")
            return image
    
    def _deskew(self, image: np.ndarray) -> np.ndarray:
        """
        Detect and correct image skew.
        
        Uses adaptive thresholding and morphological operations to detect text lines,
        then applies Hough line transform to calculate the skew angle.
        
        Args:
            image: Grayscale image
            
        Returns:
            Deskewed image
        """
        try:
            import cv2
            
            # Apply bilateral filter to reduce noise while preserving edges
            filtered = cv2.bilateralFilter(image, 9, 75, 75)
            
            # Apply adaptive threshold to create binary image with text as foreground
            binary = cv2.adaptiveThreshold(
                filtered, 255, 
                cv2.ADAPTIVE_THRESH_GAUSSIAN_C, 
                cv2.THRESH_BINARY_INV, 
                11, 2
            )
            
            # Use Canny edge detection on binary image
            edges = cv2.Canny(binary, 50, 150, apertureSize=3)
            
            # Morphological dilation to connect text characters into horizontal lines
            # The kernel width of 30 pixels is tuned to bridge typical character spacing
            # while the height of 1 preserves horizontal line detection sensitivity
            kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (30, 1))
            dilated = cv2.dilate(edges, kernel, iterations=1)
            
            # Use Hough line transform with relaxed parameters
            lines = cv2.HoughLinesP(
                dilated, 
                rho=1, 
                theta=np.pi / 180, 
                threshold=50,       # Lower threshold to detect more lines
                minLineLength=50,   # Shorter minimum line length
                maxLineGap=20       # Allow larger gaps between line segments
            )
            
            if lines is None or len(lines) == 0:
                logger.debug("No lines detected for deskew")
                return image
            
            # Calculate angles of detected lines
            # Only consider lines longer than 30 pixels to filter out noise
            # from short edge fragments that don't represent actual text lines
            min_line_length = 30
            angles = []
            for line in lines:
                x1, y1, x2, y2 = line[0]
                length = np.sqrt((x2 - x1) ** 2 + (y2 - y1) ** 2)
                if x2 - x1 != 0 and length > min_line_length:
                    angle = np.degrees(np.arctan2(y2 - y1, x2 - x1))
                    # Only consider near-horizontal lines (within 30 degrees)
                    # These represent text lines on a receipt
                    if abs(angle) < 30:
                        angles.append(angle)
            
            if not angles:
                logger.debug("No near-horizontal lines found for deskew")
                return image
            
            # Use median angle to be robust against outliers
            angle = np.median(angles)
            
            # Only correct if angle is significant (> 0.5 degrees)
            if abs(angle) < 0.5:
                logger.debug(f"Skew angle ({angle:.2f}°) too small, skipping correction")
                return image
            
            logger.debug(f"Detected {len(angles)} text lines, median angle: {angle:.2f}°")
            
            # Get image dimensions
            (h, w) = image.shape[:2]
            center = (w // 2, h // 2)
            
            # Create rotation matrix to straighten text
            # cv2.getRotationMatrix2D(center, angle, scale) rotates the image
            # by 'angle' degrees around the center point
            M = cv2.getRotationMatrix2D(center, angle, 1.0)
            
            # Calculate new bounding box size to avoid cutting off content
            cos_val = np.abs(np.cos(np.radians(angle)))
            sin_val = np.abs(np.sin(np.radians(angle)))
            new_w = int(h * sin_val + w * cos_val)
            new_h = int(h * cos_val + w * sin_val)
            
            # Adjust the rotation matrix to account for the new size
            M[0, 2] += (new_w - w) / 2
            M[1, 2] += (new_h - h) / 2
            
            # Apply rotation with expanded canvas to prevent content cutoff
            rotated = cv2.warpAffine(
                image, M, (new_w, new_h),
                flags=cv2.INTER_CUBIC,
                borderMode=cv2.BORDER_REPLICATE
            )
            
            logger.info(f"Applied deskew correction: {angle:.2f} degrees")
            return rotated
            
        except Exception as e:
            logger.warning(f"Deskewing failed: {e}")
            return image
    
    def _normalize_dpi(self, image: np.ndarray) -> np.ndarray:
        """
        Resize image to target DPI equivalent.
        
        For 300 DPI, target height is typically around 1600-2000 pixels.
        
        Args:
            image: Grayscale image
            
        Returns:
            Resized image
        """
        try:
            import cv2
            
            # Calculate target height based on DPI
            # Assuming original is ~72 DPI (screen resolution)
            # scale = target_dpi / 72
            # For 300 DPI, we want approximately 1600px height
            target_height = 1600
            
            current_height = image.shape[0]
            
            # Only resize if current height is significantly different
            if abs(current_height - target_height) < 100:
                logger.debug("Image height already near target, skipping resize")
                return image
            
            # Calculate scale
            scale = target_height / current_height
            new_width = int(image.shape[1] * scale)
            
            # Choose interpolation method based on scale direction
            if scale > 1:
                interpolation = cv2.INTER_CUBIC  # Upscaling
            else:
                interpolation = cv2.INTER_AREA  # Downscaling
            
            resized = cv2.resize(image, (new_width, target_height), interpolation=interpolation)
            logger.debug(f"Resized image from {current_height}px to {target_height}px height")
            return resized
            
        except Exception as e:
            logger.warning(f"DPI normalization failed: {e}")
            return image
    
    def _enhance_contrast(self, image: np.ndarray) -> np.ndarray:
        """
        Enhance image contrast using CLAHE.
        
        CLAHE (Contrast Limited Adaptive Histogram Equalization) provides
        better results for text than simple histogram equalization.
        
        Args:
            image: Grayscale image
            
        Returns:
            Contrast-enhanced image
        """
        try:
            import cv2
            
            # Create CLAHE object
            # clipLimit: Threshold for contrast limiting
            # tileGridSize: Size of grid for histogram equalization
            clahe = cv2.createCLAHE(clipLimit=2.0, tileGridSize=(8, 8))
            enhanced = clahe.apply(image)
            
            logger.debug("Applied CLAHE contrast enhancement")
            return enhanced
            
        except Exception as e:
            logger.warning(f"Contrast enhancement failed: {e}")
            return image
    
    def detect_and_crop(self, image: np.ndarray) -> Tuple[np.ndarray, Optional[np.ndarray]]:
        """
        Detect receipt boundaries and crop.
        
        Args:
            image: Input image
            
        Returns:
            Tuple of (cropped_image, crop_mask)
        """
        try:
            import cv2
            
            # Convert to grayscale if needed
            if len(image.shape) == 3:
                gray = cv2.cvtColor(image, cv2.COLOR_RGB2GRAY)
            else:
                gray = image.copy()
            
            # Apply Gaussian blur
            blurred = cv2.GaussianBlur(gray, (5, 5), 0)
            
            # Edge detection
            edges = cv2.Canny(blurred, 75, 200)
            
            # Find contours
            contours, _ = cv2.findContours(
                edges, cv2.RETR_LIST, cv2.CHAIN_APPROX_SIMPLE
            )
            
            if not contours:
                return image, None
            
            # Sort contours by area (largest first)
            contours = sorted(contours, key=cv2.contourArea, reverse=True)
            
            # Find the largest quadrilateral contour
            for contour in contours[:5]:
                # Approximate contour to polygon
                peri = cv2.arcLength(contour, True)
                approx = cv2.approxPolyDP(contour, 0.02 * peri, True)
                
                # If polygon has 4 vertices, it's likely the receipt
                if len(approx) == 4:
                    # Get bounding rectangle
                    x, y, w, h = cv2.boundingRect(approx)
                    
                    # Check if area is significant (at least 10% of image)
                    if w * h > 0.1 * image.shape[0] * image.shape[1]:
                        cropped = image[y:y+h, x:x+w]
                        mask = np.zeros_like(gray)
                        cv2.drawContours(mask, [approx], -1, 255, -1)
                        logger.info(f"Detected receipt boundaries: {w}x{h} at ({x}, {y})")
                        return cropped, mask
            
            # No significant quadrilateral found
            logger.debug("No clear receipt boundaries detected")
            return image, None
            
        except Exception as e:
            logger.warning(f"Receipt detection failed: {e}")
            return image, None
    
    def adaptive_threshold(self, image: np.ndarray) -> np.ndarray:
        """
        Apply adaptive thresholding for binarization.
        
        Args:
            image: Grayscale image
            
        Returns:
            Binary image
        """
        try:
            import cv2
            
            # Ensure grayscale
            if len(image.shape) == 3:
                gray = cv2.cvtColor(image, cv2.COLOR_RGB2GRAY)
            else:
                gray = image
            
            # Apply adaptive threshold
            # ADAPTIVE_THRESH_GAUSSIAN_C: Weighted sum of neighborhood values
            # THRESH_BINARY: Output is 0 or maxValue
            # 11: Size of neighborhood area
            # 2: Constant subtracted from mean
            binary = cv2.adaptiveThreshold(
                gray,
                255,
                cv2.ADAPTIVE_THRESH_GAUSSIAN_C,
                cv2.THRESH_BINARY,
                11,
                2
            )
            
            logger.debug("Applied adaptive thresholding")
            return binary
            
        except Exception as e:
            logger.warning(f"Adaptive thresholding failed: {e}")
            return image
