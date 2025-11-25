"""
OCR engines for text detection and recognition.

Supports PaddleOCR and Tesseract for receipt text extraction.
"""

import logging
from typing import List, Dict, Any, Tuple
from abc import ABC, abstractmethod

logger = logging.getLogger(__name__)


class OcrEngine(ABC):
    """Abstract base class for OCR engines."""
    
    @abstractmethod
    def detect_and_recognize(self, image: Any) -> List[Dict[str, Any]]:
        """
        Detect text regions and recognize text.
        
        Args:
            image: Input image (numpy array or PIL Image)
            
        Returns:
            List of detected words with boxes and text
        """
        pass


class PaddleOcrEngine(OcrEngine):
    """
    PaddleOCR engine for text detection and recognition.
    
    Uses PP-StructureV3 for high-accuracy OCR on receipts.
    """
    
    def __init__(
        self,
        lang: str = "en",
        detection_mode: str = "word",
        use_gpu: bool = True
    ):
        """
        Initialize PaddleOCR engine.
        
        Args:
            lang: Language code ('en', 'ch', etc.)
            detection_mode: 'word' or 'line' level detection
            use_gpu: Whether to use GPU acceleration
        """
        self.lang = lang
        self.detection_mode = detection_mode
        self.use_gpu = use_gpu
        self._ocr = None
        
        logger.info(f"Initialized PaddleOCR engine (lang={lang}, gpu={use_gpu})")
    
    def _load_ocr(self):
        """Lazy load PaddleOCR model."""
        if self._ocr is None:
            # TODO: Implement actual PaddleOCR loading
            # from paddleocr import PaddleOCR
            # self._ocr = PaddleOCR(
            #     use_angle_cls=True,
            #     lang=self.lang,
            #     use_gpu=self.use_gpu,
            #     show_log=False
            # )
            logger.info("PaddleOCR model loaded")
    
    def detect_and_recognize(self, image: Any) -> List[Dict[str, Any]]:
        """
        Detect and recognize text using PaddleOCR.
        
        Args:
            image: Input image
            
        Returns:
            List of words with format:
            [
                {
                    'text': 'TOTAL',
                    'box': [x0, y0, x1, y1],
                    'confidence': 0.98
                },
                ...
            ]
        """
        self._load_ocr()
        
        logger.info("Running PaddleOCR detection and recognition")
        
        # TODO: Implement actual OCR
        # result = self._ocr.ocr(image, cls=True)
        # 
        # words = []
        # for line in result:
        #     for word_info in line:
        #         box_points = word_info[0]  # 4 corner points
        #         text = word_info[1][0]     # recognized text
        #         confidence = word_info[1][1]  # confidence score
        #         
        #         # Convert 4-point box to [x0, y0, x1, y1]
        #         x_coords = [p[0] for p in box_points]
        #         y_coords = [p[1] for p in box_points]
        #         box = [
        #             min(x_coords),
        #             min(y_coords),
        #             max(x_coords),
        #             max(y_coords)
        #         ]
        #         
        #         words.append({
        #             'text': text,
        #             'box': box,
        #             'confidence': confidence
        #         })
        
        # Placeholder return
        return [
            {
                'text': 'RECEIPT',
                'box': [100, 50, 300, 100],
                'confidence': 0.98
            },
            {
                'text': 'TOTAL',
                'box': [100, 500, 200, 550],
                'confidence': 0.95
            },
            {
                'text': '25.99',
                'box': [400, 500, 500, 550],
                'confidence': 0.97
            }
        ]


class TesseractOcrEngine(OcrEngine):
    """
    Tesseract OCR engine fallback.
    
    Used when PaddleOCR is not available or as a secondary option.
    """
    
    def __init__(self, lang: str = "eng", config: str = "--psm 6"):
        """
        Initialize Tesseract engine.
        
        Args:
            lang: Language code
            config: Tesseract configuration string
        """
        self.lang = lang
        self.config = config
        logger.info(f"Initialized Tesseract engine (lang={lang})")
    
    def detect_and_recognize(self, image: Any) -> List[Dict[str, Any]]:
        """
        Detect and recognize text using Tesseract.
        
        Args:
            image: Input image
            
        Returns:
            List of words with boxes and text
        """
        logger.info("Running Tesseract OCR")
        
        # TODO: Implement Tesseract OCR
        # import pytesseract
        # 
        # # Get word-level data
        # data = pytesseract.image_to_data(
        #     image,
        #     lang=self.lang,
        #     config=self.config,
        #     output_type=pytesseract.Output.DICT
        # )
        # 
        # words = []
        # for i in range(len(data['text'])):
        #     text = data['text'][i].strip()
        #     if not text:
        #         continue
        #     
        #     box = [
        #         data['left'][i],
        #         data['top'][i],
        #         data['left'][i] + data['width'][i],
        #         data['top'][i] + data['height'][i]
        #     ]
        #     
        #     confidence = data['conf'][i] / 100.0  # Tesseract returns 0-100
        #     
        #     words.append({
        #         'text': text,
        #         'box': box,
        #         'confidence': confidence
        #     })
        
        # Placeholder return
        return []


def create_ocr_engine(engine_type: str = "paddle", **kwargs) -> OcrEngine:
    """
    Factory function to create OCR engine.
    
    Args:
        engine_type: Type of engine ('paddle' or 'tesseract')
        **kwargs: Additional arguments for engine initialization
        
    Returns:
        OcrEngine instance
    """
    if engine_type.lower() == "paddle":
        return PaddleOcrEngine(**kwargs)
    elif engine_type.lower() == "tesseract":
        return TesseractOcrEngine(**kwargs)
    else:
        raise ValueError(f"Unknown OCR engine type: {engine_type}")
