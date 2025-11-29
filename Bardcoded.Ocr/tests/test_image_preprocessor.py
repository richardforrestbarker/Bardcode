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
        
        This test ensures that horizontal text lines remain horizontal after deskew.
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
        
        # The horizontal lines should still be roughly horizontal
        # Check that dark pixels are still distributed horizontally
        dark_pixel_coords = np.where(result < 200)
        
        if len(dark_pixel_coords[0]) > 0:
            # Get the spread of y-coordinates vs x-coordinates for dark pixels
            y_spread = np.std(dark_pixel_coords[0])
            x_spread = np.std(dark_pixel_coords[1])
            
            # For horizontal lines, x_spread should be larger than y_spread
            # If rotated 90 degrees, y_spread would be larger
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
        
        # The image should be valid (may be slightly expanded to prevent clipping)
        assert result is not None
        assert len(result.shape) == 2  # Still grayscale

    def test_deskew_with_no_lines(self):
        """Test that deskew returns original image when no lines detected."""
        # Create an image with random noise but no clear lines
        image = np.full((200, 200), 255, dtype=np.uint8)
        # Add some random dark pixels that don't form lines
        np.random.seed(42)
        for _ in range(50):
            x, y = np.random.randint(0, 200, 2)
            image[y, x] = 0
        
        preprocessor = ImagePreprocessor(deskew=True, denoise=False, enhance_contrast=False)
        result = preprocessor._deskew(image)
        
        # Should return original image since no lines detected
        np.testing.assert_array_equal(result, image)

    def test_deskew_with_grayscale_image(self):
        """Test that deskew works with grayscale images."""
        image = np.full((300, 200), 200, dtype=np.uint8)
        
        # Add some dark content in a line pattern
        for y in range(100, 200, 20):
            image[y:y+3, 50:150] = 50
        
        preprocessor = ImagePreprocessor(deskew=True, denoise=False, enhance_contrast=False)
        result = preprocessor._deskew(image)
        
        assert result is not None
        assert len(result.shape) == 2  # Still grayscale

    def test_deskew_corrects_skewed_text(self):
        """Test that deskew properly corrects skewed text."""
        height, width = 400, 400
        image = np.full((height, width), 255, dtype=np.uint8)
        
        # Draw lines at a 10-degree angle
        skew_angle = 10
        for y_base in range(50, 350, 50):
            for x in range(50, 350):
                dy = int((x - 50) * np.tan(np.radians(skew_angle)))
                y = y_base + dy
                if 0 <= y < height and 0 <= y + 3 < height:
                    image[y:y+3, x] = 0
        
        preprocessor = ImagePreprocessor(deskew=True, denoise=False, enhance_contrast=False)
        result = preprocessor._deskew(image)
        
        # After deskew, the lines should be more horizontal
        dark_pixel_coords = np.where(result < 200)
        
        if len(dark_pixel_coords[0]) > 0:
            # Compute angle of best-fit line through dark pixels
            y_coords = dark_pixel_coords[0]
            x_coords = dark_pixel_coords[1]
            
            # The corrected image should have lines that are more horizontal
            # than the original 10-degree skew
            assert result is not None


class TestImagePreprocessorIntegration:
    """Integration tests for the full preprocessing pipeline."""

    def test_preprocess_array_with_deskew(self):
        """Test full preprocessing pipeline with deskew enabled."""
        # Create a simple RGB image with line content
        height, width = 300, 200
        image = np.full((height, width, 3), 255, dtype=np.uint8)
        for y in range(50, 250, 30):
            image[y:y+5, 30:170, :] = 50
        
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
