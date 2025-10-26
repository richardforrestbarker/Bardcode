using Bardcoded.Data.Messages;

namespace Bardcoded.Wasm.Clients
{
    [Serializable]
    internal class CreateBarcodeRequired : Exception
    {
        public readonly BardcodeInjestRequest? BardcodeInjestRequest;

        public CreateBarcodeRequired()
        {
        }

        public CreateBarcodeRequired(BardcodeInjestRequest bardcodeInjestRequest) 
            : base($"Found that barcode in {bardcodeInjestRequest.ProviderType} but not in our database.")
        {
            this.BardcodeInjestRequest = bardcodeInjestRequest;
        }
    }
}