export class CameraStreamBarcodeReader {
    constructor() {
        this.config = {
            inputStream: {
                name: "Live",
                type: "LiveStream",
                target: document.querySelector('#scanner-container'),
                constraints: {
                    width: 480,
                    height: 320,
                    facingMode: "environment"
                },
                area: { // defines rectangle of the detection/localization area
                    top: "25%",    // top offset
                    right: "5%",  // right offset
                    left: "5%",   // left offset
                    bottom: "25%"  // bottom offset
                },
            },
            numOfWorkers: navigator.hardwareConcurrency,
            locate: false,
            frequency: 10,
            multiple: true,
            locator: {
                halfSample: true,
                patchSize: "medium", // x-small, small, medium, large, x-large
            },
            decoder: {
                readers: [
                    "code_128_reader",
                    "ean_reader",
                    "ean_8_reader",
                    "code_39_reader",
                    "code_39_vin_reader",
                    "codabar_reader",
                    "upc_reader",
                    "upc_e_reader",
                    "i2of5_reader"
                ],

            },
        }
        console.log("CameraStreamBarcodeReader constructed.");
    }

    liveInit = () => {
        console.log("BarcodeReader initializing.");
        Quagga.init(this.config, function (err) {
            if (err) {
                console.error(err);
                return
            }
            console.log("Initialization finished. Ready to start");
            Quagga.onDetected(this.detected);
            Quagga.onProcessed(this.processed);
        });
    }

    processed = (result) => {
        if (!result) {
            return;
        }
        var drawingCtx = Quagga.canvas.ctx.overlay,
            drawingCanvas = Quagga.canvas.dom.overlay;
        if (result.boxes) {
            drawingCtx.clearRect(0, 0, parseInt(drawingCanvas.getAttribute("width")), parseInt(drawingCanvas.getAttribute("height")));
            result.boxes.filter(function (box) {
                return box !== result.box;
            }).forEach(function (box) {
                Quagga.ImageDebug.drawPath(box, { x: 0, y: 1 }, drawingCtx, { color: "red", lineWidth: 3 });
            });
        }

        if (result.box) {
            //Quagga.ImageDebug.drawPath(result.box, { x: 0, y: 1 }, drawingCtx, { color: "#00F", lineWidth: 2 });
        }

        if (result.codeResult && result.codeResult.code) {
            //Quagga.ImageDebug.drawPath(result.line, { x: 'x', y: 'y' }, drawingCtx, { color: 'green', lineWidth: 3 });
        }
    }


    detected = function (result) {
        console.log(result.codeResult);

        if (result.codeResult.startInfo.error < 0.12) {
            //alert(result.codeResult.startInfo.error + " | " + result.codeResult.startInfo.error + " Result: " + result.codeResult.code);
            document.getElementById('scanner-container').style.display = 'none';
            Quagga.stop();
            instance.invokeMethod('BarcodeFound', result.codeResult.code, result.codeResult.format);
        }
    }


    startReader = (instance) => {
        this.instance = instance;
        console.log("Starting Live Read");
        Quagga.start();
    }

    stopReader = () => {
        document.getElementById('scanner-container').style.display = 'none';
        Quagga.stop();
    }

}

export class ImageBarcodeReader {
    constructor(opts = { debug: false }) {
        this.debug = opts.debug;
        //document.getElementById('scanner-container').style.display = 'block';
        //document.getElementById('helperlines').style.display = 'block';
        this.fileUploadConfig = {
            numOfWorkers: 2,
            frequency: 16,
            locator: {
                halfSample: true,
                patchSize: "medium", // x-small, small, medium, large, x-large
                debug: {
                    showCanvas: false,
                    showPatches: false,
                    showFoundPatches: false,
                    showSkeleton: false,
                    showLabels: false,
                    showPatchLabels: false,
                    showRemainingPatchLabels: false,
                    boxFromPatches: {
                        showTransformed: false,
                        showTransformedBox: false,
                        showBB: false
                    }
                }
            },
            debug: this.debug,
            decoder: {
                readers: [
                    'code_128_reader',
                    'ean_reader',
                    'ean_8_reader',
                    'code_39_reader',
                    'code_39_vin_reader',
                    'code_93_reader',
                    'codabar_reader',
                    'upc_reader',
                    'upc_e_reader',
                    'i2of5_reader',
                    '2of5_reader'
                ],
                debug: {
                    drawBoundingBox: this.debug,
                    showFrequency: this.debug,
                    drawScanline: this.debug,
                    showPattern: this.debug
                },
                multiple: true
            },
            locate: true, // try to locate the barcode in the image
        };

        console.log("BarcodeReader constructed.");
    }

    readFromFile = (dataImageString, instance) => {
        console.log("BarcodeReader reading from the given file.");
        var config = $.extend(this.fileUploadConfig, { src: dataImageString });
        Quagga.decodeSingle(config, (result) => {
            console.log("result handler called", result);
            if (result.codeResult) {
                console.log("result", result.codeResult.code);
                instance.invokeMethod('BarcodeFound', result.codeResult.code, result.codeResult.format);

                if (this.debug) {
                    var code = result.codeResult.code,
                        $node,
                        canvas = Quagga.canvas.dom.image;
                    $node = $('<li><div class="thumbnail"><div class="imgWrapper"><img /></div><div class="caption"><h4 class="code"></h4></div></div></li>');
                    $node.find("img").attr("src", canvas.toDataURL());
                    $node.find("h4.code").html(code);
                    $("#result_strip ul.thumbnails").prepend($node);
                }

            } else {
                console.log("not detected");
                instance.invokeMethod('BarcodeNotFound');
            }
        });
    }
}
