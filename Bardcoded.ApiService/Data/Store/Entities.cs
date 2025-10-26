namespace Bardcoded.ApiService.Data.Store
{
    public class BarcodeData
    {
        public Guid Id { get; set; }
        public String Bard { get; set; }
        public String Source { get; set; }
        public String Name { get; set; }
        public String Description { get; set; }
        public String Base64Image { get; set; } // needs to be stored as clob or blob
        public String ImageType { get; set; }
    }
    public class BarcodeUpdate
    {
        public Guid Id { get; set; }
        public Guid BarcodeId { get; set; }
        public String OldBarcodeJson { get; set; }
        public String NewBarcodeJson { get; set; }
        public DateTime UpdateDate { get; set; }
    }

    public class BarcodeDataProvided
    {
        public String Bard { get; set; }
        public DateTime LastUpdated { get; set; }
        public String ProviderJson { get; set; }
        public String ProviderType { get; set; }
    }
}
