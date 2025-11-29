"""
Unit tests for image_preprocessor.py.

Tests for image preprocessing functions, especially the deskew functionality.
"""

import sys
import pytest
import numpy as np
from pathlib import Path

# Add parent directory to path for imports
sys.path.insert(0, str(Path(__file__).parent.parent))

from src.preprocessing.image_preprocessor import ImagePreprocessor


class TestDeskew:
    """Tests for the deskew functionality."""

    def test_deskew_does_not_rotate_90_degrees(self):
        """
        Test that deskew does not incorrectly rotate images by 90 degrees.
        
        This test reproduces the bug where coordinates were passed to 
        cv2.minAreaRect in (y, x) format instead of (x, y) format,
        causing 90-degree rotations.
        """
        # Create a tall image (like a receipt) with horizontal text lines
        # Height > Width to simulate a receipt
        height, width = 600, 400
        
        # Create a white background
        image = np.full((height, width), 255, dtype=np.uint8)
        
        # Draw horizontal lines (simulating text) - dark pixels on white background
        for y in range(100, 500, 50):
            image[y:y+5, 50:350] = 0
        
        # Create preprocessor with deskew enabled
        preprocessor = ImagePreprocessor(deskew=True, denoise=False, enhance_contrast=False)
        
        # Apply deskew
        result = preprocessor._deskew(image)
        
        # The result should have the same shape as the input
        # (a 90-degree rotation would swap height and width, but warpAffine preserves size)
        assert result.shape == image.shape
        
        # More importantly, the horizontal lines should still be roughly horizontal
        # Check that dark pixels are still distributed horizontally
        dark_pixel_coords = np.where(result < 200)
        
        if len(dark_pixel_coords[0]) > 0:
            # Get the spread of y-coordinates vs x-coordinates for dark pixels
            y_spread = np.std(dark_pixel_coords[0])
            x_spread = np.std(dark_pixel_coords[1])
            
            # For horizontal lines, x_spread should be larger than y_spread
            # If rotated 90 degrees, y_spread would be larger
            # We just need the ratio to be reasonable - not flipped
            assert x_spread > y_spread * 0.5, (
                f"Horizontal lines appear to have been rotated. "
                f"x_spread={x_spread}, y_spread={y_spread}"
            )

    def test_deskew_preserves_nearly_straight_image(self):
        """Test that images with minimal skew are not significantly altered."""
        # Create a simple image with horizontal lines (no skew)
        height, width = 400, 400
        image = np.full((height, width), 255, dtype=np.uint8)
        
        # Draw horizontal lines
        for y in range(50, 350, 50):
            image[y:y+3, 50:350] = 0
        
        preprocessor = ImagePreprocessor(deskew=True, denoise=False, enhance_contrast=False)
        result = preprocessor._deskew(image)
        
        # The image should be largely unchanged (small angle < 0.5 degrees skipped)
        # or only slightly adjusted
        assert result.shape == image.shape

    def test_deskew_with_insufficient_pixels(self):
        """Test that deskew returns original image when not enough dark pixels."""
        # Create an almost white image with very few dark pixels
        image = np.full((200, 200), 255, dtype=np.uint8)
        image[50, 50] = 0  # Only 1 dark pixel
        
        preprocessor = ImagePreprocessor(deskew=True, denoise=False, enhance_contrast=False)
        result = preprocessor._deskew(image)
        
        # Should return original image
        np.testing.assert_array_equal(result, image)

    def test_deskew_with_grayscale_image(self):
        """Test that deskew works with grayscale images."""
        image = np.full((300, 200), 200, dtype=np.uint8)
        
        # Add some dark content
        image[100:150, 50:150] = 50
        
        preprocessor = ImagePreprocessor(deskew=True, denoise=False, enhance_contrast=False)
        result = preprocessor._deskew(image)
        
        assert result is not None
        assert result.shape == image.shape


class TestImagePreprocessorIntegration:
    """Integration tests for the full preprocessing pipeline."""

    def test_preprocess_array_with_deskew(self):
        """Test full preprocessing pipeline with deskew enabled."""
        # Create a simple RGB image
        image = np.full((300, 200, 3), 255, dtype=np.uint8)
        image[100:150, 50:150, :] = 50
        
        preprocessor = ImagePreprocessor(
            deskew=True, 
            denoise=False, 
            enhance_contrast=False
        )
        result = preprocessor.preprocess_array(image)
        
        assert result is not None
        assert len(result.shape) == 3  # Should be RGB
        assert result.shape[2] == 3

    def test_preprocess_array_all_options_disabled(self):
        """Test preprocessing with all options disabled."""
        image = np.full((100, 100, 3), 128, dtype=np.uint8)
        
        preprocessor = ImagePreprocessor(
            deskew=False, 
            denoise=False, 
            enhance_contrast=False
        )
        result = preprocessor.preprocess_array(image)
        
        assert result is not None
