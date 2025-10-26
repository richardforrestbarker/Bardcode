using Bardcoded.ApiService.Data.Store;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Text.Json;

namespace Bardcoded.ApiService.Data
{
    public interface IBarcodeDataContext
    {
        Task<List<BarcodeData>> GetAll();
        Task DeleteBarcode(string bard);
        Task DeleteAll();
        Task<BarcodeData> GetBarcode(string bard);

        Task<Guid> InsertBarcode(BarcodeData data);

        Task<BarcodeData> UpdateBarcode(BarcodeData data);

        Task<BarcodeDataProvided?> GetBarcodeDataProvided(string bard);
        void InsertBarcodeDataProvided(BarcodeDataProvided data);
    }
    public class BarcodeDataContext(DbContextOptions<BarcodeDataContext> options) : DbContext(options), IBarcodeDataContext
    {
        public DbSet<BarcodeData> Barcodes { get; set; }
        public DbSet<BarcodeUpdate> BarcodeUpdates { get; set; }
        public DbSet<BarcodeDataProvided> BarcodeDataProvided { get; set; }
        public DbSet<InventoryItem> InventoryItems { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            
            // Configure BarcodeDataProvided to use Bard as primary key and foreign key
            builder.Entity<BarcodeDataProvided>()
                .HasKey(b => b.Bard);
            
            builder.Entity<BarcodeDataProvided>()
                .HasOne<BarcodeData>()
                .WithOne()
                .HasForeignKey<BarcodeDataProvided>(b => b.Bard)
                .HasPrincipalKey<BarcodeData>(b => b.Bard)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure InventoryItem
            builder.Entity<InventoryItem>()
                .HasKey(i => i.Id);

            builder.Entity<InventoryItem>()
                .HasIndex(i => new { i.Barcode, i.BarcodeType })
                .IsUnique();

            builder.Entity<InventoryItem>()
                .Property(i => i.RowVersion)
                .IsRowVersion();

            builder.Entity<InventoryItem>()
                .Property(i => i.LastUpdatedBy)
                .IsRequired();

            builder.Entity<InventoryItem>()
                .Property(i => i.Barcode)
                .IsRequired();

            builder.Entity<InventoryItem>()
                .Property(i => i.BarcodeType)
                .IsRequired();
        }

        public Task DeleteAll()
        {

            return Task.CompletedTask;
        }

        public async Task DeleteBarcode(string bard)
        {
            var entity = await Barcodes.Where(x => x.Bard.Equals(bard)).FirstAsync();
            Barcodes.Remove(entity);
            await SaveChangesAsync();
        }

        public Task<List<BarcodeData>> GetAll()
        {
            return Task.FromResult(Barcodes.ToList());
        }

        public Task<BarcodeData> GetBarcode(string bard)
        {
            return Barcodes.Where(c => c.Bard.Equals(bard)).SingleOrDefaultAsync();
        }

        public async Task<Guid> InsertBarcode(BarcodeData data)
        {
            try
            {
                var code = await GetBarcode(data.Bard);
                if (code != default(BarcodeData))
                {
                    throw new InvalidOperationException("Barcode exists.");
                }
                Barcodes.Add(data);
                await SaveChangesAsync();
                return data.Id;
            }
            catch (InvalidOperationException inval)
            {
                throw new InvalidOperationException("Can't create an entry for that barcode.", inval);
            }

        }

        public async Task<BarcodeData> UpdateBarcode(BarcodeData data)
        {
            try
            {
                var code = await GetBarcode(data.Bard);
                if (code == default(BarcodeData))
                {
                    throw new InvalidOperationException("Barcode doesn't exist.");
                }
                BarcodeUpdates.Add(new BarcodeUpdate()
                {
                    BarcodeId = data.Id,
                    UpdateDate = DateTime.Now,
                    NewBarcodeJson = JsonSerializer.Serialize(data),
                    OldBarcodeJson = JsonSerializer.Serialize(code)
                });
                Barcodes.Update(data);
                await SaveChangesAsync();
                return data;
            }
            catch (InvalidOperationException inval)
            {
                throw new InvalidOperationException("Can't update the entry for that barcode.", inval);
            }
        }

        public Task<BarcodeDataProvided?> GetBarcodeDataProvided(string bard)
        {
            return BarcodeDataProvided.Where(c => c.Bard.Equals(bard)).SingleOrDefaultAsync();
        }

        public void InsertBarcodeDataProvided(BarcodeDataProvided data)
        {
            try
            {
                BarcodeDataProvided.Add(data);
                SaveChanges();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Can't create provider data entry for that barcode.", ex);
            }
        }
    }
}
