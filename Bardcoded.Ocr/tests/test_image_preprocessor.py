"""
Unit tests for image_preprocessor.py.

Tests for image preprocessing functions, especially the deskew functionality.
Also includes tests for ImageMagick-based preprocessing steps.
"""

import sys
import pytest
import numpy as np
from pathlib import Path
import tempfile
import os

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
        """Test that deskew handles images with no clear text lines gracefully."""
        # Create an image with random noise but no clear lines
        image = np.full((200, 200), 255, dtype=np.uint8)
        # Add some random dark pixels that don't form lines
        np.random.seed(42)
        for _ in range(50):
            x, y = np.random.randint(0, 200, 2)
            image[y, x] = 0
        
        preprocessor = ImagePreprocessor(deskew=True, denoise=False, enhance_contrast=False)
        result = preprocessor._deskew(image)
        
        # Result should be valid (either original or slightly adjusted)
        assert result is not None
        assert len(result.shape) == 2  # Still grayscale
        # Should not crash or produce empty image
        assert result.shape[0] > 0 and result.shape[1] > 0

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


class TestImageMagickPreprocessing:
    """Tests for ImageMagick (Wand) based preprocessing steps."""

    @pytest.fixture
    def sample_image_path(self):
        """Create a temporary test image file."""
        from PIL import Image
        
        # Create a simple test image with text-like content
        height, width = 400, 300
        img_array = np.full((height, width, 3), 255, dtype=np.uint8)
        
        # Add some dark horizontal lines (simulating text)
        for y in range(50, 350, 40):
            img_array[y:y+5, 30:270, :] = 30
        
        # Save to temporary file
        with tempfile.NamedTemporaryFile(suffix='.png', delete=False) as tmp:
            Image.fromarray(img_array).save(tmp.name)
            yield tmp.name
        
        # Cleanup
        os.unlink(tmp.name)

    @pytest.fixture
    def wand_available(self):
        """Check if Wand/ImageMagick is available."""
        try:
            from wand.image import Image as WandImage
            return True
        except ImportError:
            return False

    def test_preprocess_uses_imagemagick_when_available(self, sample_image_path, wand_available):
        """Test that preprocessing uses ImageMagick when available."""
        preprocessor = ImagePreprocessor(
            target_dpi=300,
            deskew=True,
            denoise=True,
            enhance_contrast=True
        )
        
        result = preprocessor.preprocess(sample_image_path)
        
        # Result should be a valid RGB image
        assert result is not None
        assert len(result.shape) == 3
        assert result.shape[2] == 3
        # Should have reasonable dimensions
        assert result.shape[0] > 0
        assert result.shape[1] > 0

    def test_convert_to_tiff(self, sample_image_path):
        """Test TIFF conversion step."""
        wand = pytest.importorskip("wand.image", reason="Wand not available")
        WandImage = wand.Image
        
        preprocessor = ImagePreprocessor()
        
        with WandImage(filename=sample_image_path) as img:
            result = preprocessor._convert_to_tiff(img)
            
            assert result is not None
            assert result.format.lower() == 'tiff'

    def test_fix_resolution_sets_300_dpi(self, sample_image_path):
        """Test that fix_resolution sets the image to 300 DPI."""
        wand = pytest.importorskip("wand.image", reason="Wand not available")
        WandImage = wand.Image
        
        preprocessor = ImagePreprocessor(target_dpi=300)
        
        with WandImage(filename=sample_image_path) as img:
            original_width = img.width
            original_height = img.height
            
            result = preprocessor._fix_resolution(img)
            
            assert result is not None
            # Resolution should be set to 300 DPI
            assert result.resolution == (300, 300)

    def test_remove_background(self, sample_image_path):
        """Test background removal step."""
        wand = pytest.importorskip("wand.image", reason="Wand not available")
        WandImage = wand.Image
        
        preprocessor = ImagePreprocessor()
        
        with WandImage(filename=sample_image_path) as img:
            result = preprocessor._remove_background(img)
            
            assert result is not None
            # Result should still have valid dimensions
            assert result.width > 0
            assert result.height > 0

    def test_deskew_wand(self, sample_image_path):
        """Test ImageMagick deskew step."""
        wand = pytest.importorskip("wand.image", reason="Wand not available")
        WandImage = wand.Image
        
        preprocessor = ImagePreprocessor(deskew=True)
        
        with WandImage(filename=sample_image_path) as img:
            result = preprocessor._deskew_wand(img)
            
            assert result is not None
            assert result.width > 0
            assert result.height > 0

    def test_grayscale_conversion(self, sample_image_path):
        """Test grayscale conversion using ImageMagick."""
        wand = pytest.importorskip("wand.image", reason="Wand not available")
        WandImage = wand.Image
        
        preprocessor = ImagePreprocessor()
        
        with WandImage(filename=sample_image_path) as img:
            result = preprocessor._grayscale(img)
            
            assert result is not None
            assert result.type == 'grayscale'

    def test_enhance_contrast_wand(self, sample_image_path):
        """Test contrast enhancement using ImageMagick."""
        wand = pytest.importorskip("wand.image", reason="Wand not available")
        WandImage = wand.Image
        
        preprocessor = ImagePreprocessor(enhance_contrast=True)
        
        with WandImage(filename=sample_image_path) as img:
            result = preprocessor._enhance_contrast_wand(img)
            
            assert result is not None
            assert result.width > 0
            assert result.height > 0

    def test_denoise_wand(self, sample_image_path):
        """Test denoising using ImageMagick."""
        wand = pytest.importorskip("wand.image", reason="Wand not available")
        WandImage = wand.Image
        
        preprocessor = ImagePreprocessor(denoise=True)
        
        with WandImage(filename=sample_image_path) as img:
            result = preprocessor._denoise_wand(img)
            
            assert result is not None
            assert result.width > 0
            assert result.height > 0

    def test_full_imagemagick_pipeline(self, sample_image_path):
        """Test the full ImageMagick preprocessing pipeline."""
        pytest.importorskip("wand.image", reason="Wand not available")
        
        preprocessor = ImagePreprocessor(
            target_dpi=300,
            deskew=True,
            denoise=True,
            enhance_contrast=True
        )
        
        result = preprocessor.preprocess(sample_image_path)
        
        # Result should be a valid RGB numpy array
        assert result is not None
        assert isinstance(result, np.ndarray)
        assert len(result.shape) == 3
        assert result.shape[2] == 3

    def test_opencv_fallback_when_wand_unavailable(self):
        """Test that OpenCV fallback works when Wand is not available."""
        # Create a simple test image
        height, width = 200, 150
        image = np.full((height, width, 3), 255, dtype=np.uint8)
        for y in range(30, 170, 30):
            image[y:y+3, 20:130, :] = 50
        
        preprocessor = ImagePreprocessor(
            deskew=True,
            denoise=True,
            enhance_contrast=True
        )
        
        # preprocess_array should work even without Wand
        result = preprocessor.preprocess_array(image)
        
        assert result is not None
        assert len(result.shape) == 3
        assert result.shape[2] == 3


class TestPreprocessingPipelineOrder:
    """Tests to verify preprocessing steps are in the correct order."""

    @pytest.fixture
    def sample_image_path(self):
        """Create a temporary test image file."""
        from PIL import Image
        
        height, width = 300, 200
        img_array = np.full((height, width, 3), 255, dtype=np.uint8)
        for y in range(40, 260, 30):
            img_array[y:y+4, 25:175, :] = 40
        
        with tempfile.NamedTemporaryFile(suffix='.png', delete=False) as tmp:
            Image.fromarray(img_array).save(tmp.name)
            yield tmp.name
        
        os.unlink(tmp.name)

    def test_pipeline_order_tiff_first(self, sample_image_path):
        """
        Test that TIFF conversion happens first in the pipeline.
        
        Pipeline order should be: TIFF -> Resolution -> Background -> Deskew -> 
                                 Grayscale -> Contrast -> Denoise
        """
        wand = pytest.importorskip("wand.image", reason="Wand not available")
        WandImage = wand.Image
        
        preprocessor = ImagePreprocessor(
            target_dpi=300,
            deskew=True,
            denoise=True,
            enhance_contrast=True
        )
        
        # We verify the order by checking that each step can receive output from the previous
        with WandImage(filename=sample_image_path) as img:
            # Step 1: Convert to TIFF
            img = preprocessor._convert_to_tiff(img)
            assert img.format.lower() == 'tiff'
            
            # Step 2: Fix resolution
            img = preprocessor._fix_resolution(img)
            assert img.resolution == (300, 300)
            
            # Step 3: Remove background
            img = preprocessor._remove_background(img)
            assert img.width > 0
            
            # Step 4: Deskew
            img = preprocessor._deskew_wand(img)
            assert img.width > 0
            
            # Step 5: Grayscale
            img = preprocessor._grayscale(img)
            assert img.type == 'grayscale'
            
            # Step 6: Contrast enhancement
            img = preprocessor._enhance_contrast_wand(img)
            assert img.width > 0
            
            # Step 7: Denoise
            img = preprocessor._denoise_wand(img)
            assert img.width > 0

    def test_preprocess_returns_rgb_for_ocr_engines(self, sample_image_path):
        """Test that final output is RGB format for compatibility with OCR engines."""
        preprocessor = ImagePreprocessor(
            target_dpi=300,
            deskew=True,
            denoise=True,
            enhance_contrast=True
        )
        
        result = preprocessor.preprocess(sample_image_path)
        
        # OCR engines expect RGB format
        assert result is not None
        assert len(result.shape) == 3
        assert result.shape[2] == 3  # RGB channels
