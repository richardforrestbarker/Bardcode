import { createWorker } from 'tesseract.js';

let worker = null;

// Initialize Tesseract worker
async function initWorker() {
    if (!worker) {
        console.log('Initializing Tesseract worker...');
        worker = await createWorker('eng', 1, {
            logger: m => console.log('Tesseract:', m)
        });
        console.log('Tesseract worker initialized');
    }
    return worker;
}

// Perform OCR on an image
export async function performOcr(imageDataUrl) {
    try {
        const tesseractWorker = await initWorker();
        
        console.log('Starting OCR...');
        const { data: { text } } = await tesseractWorker.recognize(imageDataUrl);
        console.log('OCR completed:', text);
        
        return text;
    } catch (error) {
        console.error('OCR error:', error);
        throw new Error(`OCR failed: ${error.message}`);
    }
}

// Detect barcode in image and draw on canvas
export async function detectBarcode(imageDataUrl, canvasId) {
    return new Promise((resolve, reject) => {
        try {
            console.log('Detecting barcode in image...');
            
            // Create an image element
            const img = new Image();
            img.onload = function() {
                // Use Quagga to detect barcode
                const config = {
                    inputStream: {
                        size: img.width
                    },
                    locator: {
                        halfSample: true,
                        patchSize: "medium"
                    },
                    numOfWorkers: 2,
                    decoder: {
                        readers: [
                            'code_128_reader',
                            'ean_reader',
                            'ean_8_reader',
                            'code_39_reader',
                            'upc_reader',
                            'upc_e_reader',
                            'i2of5_reader'
                        ]
                    },
                    locate: true,
                    src: imageDataUrl
                };

                Quagga.decodeSingle(config, function(result) {
                    if (result && result.codeResult) {
                        console.log('Barcode detected:', result.codeResult.code);
                        
                        // Draw the barcode box and line on canvas
                        const canvas = document.getElementById(canvasId);
                        if (canvas) {
                            const ctx = canvas.getContext('2d');
                            canvas.width = img.width;
                            canvas.height = img.height;
                            
                            // Draw the bounding box
                            if (result.box) {
                                ctx.strokeStyle = 'lime';
                                ctx.lineWidth = 3;
                                ctx.beginPath();
                                ctx.moveTo(result.box[0][0], result.box[0][1]);
                                for (let i = 1; i < result.box.length; i++) {
                                    ctx.lineTo(result.box[i][0], result.box[i][1]);
                                }
                                ctx.lineTo(result.box[0][0], result.box[0][1]);
                                ctx.stroke();
                            }
                            
                            // Draw the scan line
                            if (result.line) {
                                ctx.strokeStyle = 'red';
                                ctx.lineWidth = 2;
                                ctx.beginPath();
                                ctx.moveTo(result.line[0].x, result.line[0].y);
                                ctx.lineTo(result.line[1].x, result.line[1].y);
                                ctx.stroke();
                            }
                        }
                        
                        resolve({
                            code: result.codeResult.code,
                            format: result.codeResult.format
                        });
                    } else {
                        console.log('No barcode detected in image');
                        resolve(null);
                    }
                });
            };
            
            img.onerror = function() {
                reject(new Error('Failed to load image for barcode detection'));
            };
            
            img.src = imageDataUrl;
        } catch (error) {
            console.error('Barcode detection error:', error);
            reject(error);
        }
    });
}

// Cleanup function
export async function cleanup() {
    if (worker) {
        console.log('Terminating Tesseract worker...');
        await worker.terminate();
        worker = null;
    }
}
