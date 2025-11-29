"""
Image preprocessing utilities for receipt OCR.

Includes denoising, deskewing, normalization, and other image enhancement operations.
"""

import logging
from typing import Tuple, Optional, Any, TYPE_CHECKING
import numpy as np

if TYPE_CHECKING:
    from ..cli.debug_output import DebugOutputManager

logger = logging.getLogger(__name__)


class ImagePreprocessor:
    """
    Preprocesses receipt images to improve OCR accuracy.
    
    Operations include:
    - Grayscale conversion
    - Denoising
    - Deskewing
    - DPI normalization
    - Contrast enhancement
    - Adaptive thresholding
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
            target_dpi: Target DPI for normalization
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
        Preprocess image for OCR.
        
        Args:
            image_path: Path to image file
            page_num: Page number for debug output
            
        Returns:
            Preprocessed image as numpy array
        """
        logger.info(f"Preprocessing image: {image_path}")
        
        try:
            from PIL import Image
            import cv2
            
            # 1. Load image
            img = Image.open(image_path)
            img_array = np.array(img)
            
            return self.preprocess_array(img_array, page_num=page_num)
            
        except ImportError as e:
            logger.error(f"Required dependencies not available: {e}")
            raise ImportError(
                "OpenCV and Pillow are required. Install with: pip install opencv-python Pillow"
            )
    
    def preprocess_array(self, img_array: np.ndarray, page_num: int = 1) -> np.ndarray:
        """
        Preprocess a numpy array image.
        
        Args:
            img_array: Image as numpy array
            page_num: Page number for debug output
            
        Returns:
            Preprocessed image as numpy array (RGB)
        """
        try:
            import cv2
        except ImportError:
            logger.warning("OpenCV not available, returning original image")
            return img_array
        
        # 2. Convert to grayscale if needed
        if len(img_array.shape) == 3:
            if img_array.shape[2] == 4:  # RGBA
                img_array = cv2.cvtColor(img_array, cv2.COLOR_RGBA2RGB)
            gray = cv2.cvtColor(img_array, cv2.COLOR_RGB2GRAY)
        else:
            gray = img_array.copy()
        
        # Save grayscale debug image
        if self.debug_manager:
            self.debug_manager.save_grayscale_image(gray, page_num)
        
        # 3. Denoise
        if self.denoise:
            gray = self._denoise(gray)
            # Save denoised debug image
            if self.debug_manager:
                self.debug_manager.save_denoised_image(gray, page_num)
        
        # 4. Deskew
        if self.deskew:
            gray = self._deskew(gray)
            # Save deskewed debug image
            if self.debug_manager:
                self.debug_manager.save_deskewed_image(gray, page_num)
        
        # 5. Normalize DPI (resize to target height)
        gray = self._normalize_dpi(gray)
        
        # 6. Enhance contrast
        if self.enhance_contrast:
            gray = self._enhance_contrast(gray)
            # Save contrast enhanced debug image
            if self.debug_manager:
                self.debug_manager.save_contrast_enhanced_image(gray, page_num)
        
        # Convert back to RGB for OCR engines
        result = cv2.cvtColor(gray, cv2.COLOR_GRAY2RGB)
        
        # Save final preprocessed debug image
        if self.debug_manager:
            self.debug_manager.save_preprocessed_image(result, page_num)
        
        return result
    
    def _denoise(self, image: np.ndarray) -> np.ndarray:
        """
        Apply denoising filter.
        
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
        
        Uses Hough transform to detect dominant text orientation.
        
        Args:
            image: Grayscale image
            
        Returns:
            Deskewed image
        """
        try:
            import cv2
            
            # Find coordinates of non-white pixels
            # For receipts, text is typically darker than background
            # np.where returns (rows, cols) = (y, x), but cv2.minAreaRect expects (x, y)
            # So we reverse the order with [::-1] to get (cols, rows) = (x, y)
            coords = np.column_stack(np.where(image < 200)[::-1])
            
            if len(coords) < 100:
                logger.debug("Not enough text pixels for deskew detection")
                return image
            
            # Find minimum area rectangle
            angle = cv2.minAreaRect(coords)[-1]
            
            # Normalize angle to [-45, 45] range
            if angle < -45:
                angle = -(90 + angle)
            else:
                angle = -angle
            
            # Only correct if angle is significant (> 0.5 degrees)
            if abs(angle) < 0.5:
                logger.debug(f"Skew angle ({angle:.2f}°) too small, skipping correction")
                return image
            
            # Get image dimensions
            (h, w) = image.shape[:2]
            center = (w // 2, h // 2)
            
            # Create rotation matrix
            M = cv2.getRotationMatrix2D(center, angle, 1.0)
            
            # Apply rotation
            rotated = cv2.warpAffine(
                image, M, (w, h),
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
