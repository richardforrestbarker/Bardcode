using Bardcoded.ApiService.Data;
using Bardcoded.Data.Messages;
using Bardcoded.ApiService.Providers;
using Bardcoded.ApiService.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Bardcoded.ApiService.Controllers
{
    [Route("/item")]
    [ApiController]
    [Produces("application/json")]
    [ConditionalAuthorize]
    public class ItemsController : ControllerBase
    {
        public IBarcodeDataContext Context { get; }
        public BarcodeFetcher Fetcher { get; }
        internal IOMapper Mapper { get; }

        public ItemsController(IBarcodeDataContext dataContext, BarcodeFetcher fetcher)
        {
            Context = dataContext;
            Fetcher = fetcher;
            Mapper = new IOMapper();
        }

        /// <summary>
        /// Gets all known barcodes.
        /// </summary>
        /// <returns>A List of known barcodes and their product images</returns>
        /// <remarks>
        /// Example response: 
        /// []
        /// </remarks>
        /// <response code="200">The list.</response>
        [HttpGet("/all")]
        [ProducesResponseType(typeof(BarcodeView), 200)]
        public async Task<IResult> GetAllItems()
        {
            var results = await Context.GetAll();
            var views = new List<BarcodeView>();
            foreach (var barcode in results)
            {
                var view = Mapper.Map(barcode);
                // Get provider information if available
                var providerData = await Context.GetBarcodeDataProvided(barcode.Bard);
                if (providerData != null)
                {
                    view.ProviderType = providerData.ProviderType;
                }
                views.Add(view);
            }
            return Results.Ok(views);
        }

        /// <summary>
        /// Gets a single item by its barcode.
        /// </summary>
        /// <param name="bard">The code of the item to get.</param>
        /// <returns>The item and an image.</returns>
        /// <response code="203">The item if the barcode was fetched from a data integration. The user can chose to store or ignore the data.</response>
        /// /// <response code="200">The item if the barcode was fetched from the bardcode database.</response>
        /// <response code="400">If the bard is null or empty string.</response>
        /// <response code="404">If the bard is not found.</response>
        [HttpGet()]
        [ProducesResponseType(typeof(BardcodeInjestRequest), 203)]
        [ProducesResponseType(typeof(BarcodeView), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        public async Task<IResult> Get([FromQuery] string bard, [FromQuery] string barcodeType)
        {
            if (String.IsNullOrWhiteSpace(bard))
            {
                return Results.BadRequest(new ProblemDetails() { Detail = "Please provide a bard.", Status = (int)HttpStatusCode.BadRequest, Title = "No Bard Given." });
            }
            var result = await Fetcher.FindItemInCache(bard, barcodeType);
            if(result == null) {
                result = await Fetcher.FindItemInDatabase(bard, barcodeType);
            }
            if (result == null) {
                var ingestRequest = await Fetcher.FindItemInNetwork(bard, barcodeType);
                if (ingestRequest != null)
                {
                    return Results.Json(ingestRequest,statusCode: 203);
                }
            }
            if (result == null)
            {
                return Results.NotFound(new ProblemDetails() { Detail = "That bard wasn't found.", Status = (int)HttpStatusCode.NotFound, Title = "Unknown Bard." });
            }
            return Results.Ok(result);
        }

        /// <summary>
        /// Creates an item by it's barcode. If that item is known already then returns a 409. If the barcode fails validation, returns a 400.
        /// </summary>
        /// <param name="request">The barcode ingest request</param>
        /// <returns>The item and an image.</returns>
        /// <response code="201">The item.</response>
        /// <response code="400">If the bard is null or empty string.</response>
        /// <response code="409">If the bard exists.</response>
        [HttpPost]
        [ProducesResponseType(typeof(BardcodeInjestRequest), 201)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 409)]
        public async Task<IResult> Post([FromBody] BardcodeInjestRequest request)
        {
            try
            {
                var mapper = new IOMapper();
                var create = mapper.Map(request);
                var id = await Context.InsertBarcode(create);
                
                // Store provider data if available
                if (!string.IsNullOrEmpty(request.ProviderType) && !string.IsNullOrEmpty(request.ProviderJson))
                {
                    Context.InsertBarcodeDataProvided(new Data.Store.BarcodeDataProvided
                    {
                        Bard = request.Bard,
                        LastUpdated = DateTime.UtcNow,
                        ProviderType = request.ProviderType,
                        ProviderJson = request.ProviderJson
                    });
                }
                
                Console.WriteLine($"barcode {id}:{create.Bard} was created");
                return Results.Created($"/item?bard={create.Bard}", request);
                
            }
            catch (InvalidOperationException inval)
            {
                return Results.Conflict(new ProblemDetails() { Detail = $"Multiple entries exist in the database for that bard. This is an application error state.", Title = "Uh Oh." });
            }
        }

        /// <summary>
        /// Updates an item by it's barcode. If that item is not found then returns a 404. If the barcode fails validation, returns a 400.
        /// </summary>
        /// <param name="bard">The code of the item to get.</param>
        /// <returns>The item and an image.</returns>
        /// <response code="200">The item.</response>
        /// <response code="400">If the bard is null or empty string.</response>
        /// <response code="404">If the bard doesn't exist.</response>
        [HttpPut()]
        [ProducesResponseType(typeof(BarcodeView), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        public async Task<IResult> Put([FromBody] BardcodeUpdateRequest request)
        {
            try
            {
                var code = await Context.GetBarcode(request.Bard);
                if (code == null)
                {
                    return Results.NotFound(new ProblemDetails() { Detail = $"That bard doesn't exist in the database. Use the post verb to create it.", Title = "Barcode Doesn't Exist." });
                }
                var mapper = new IOMapper();
                return Results.Ok(await Context.UpdateBarcode(mapper.Map(request)));
            }
            catch (InvalidOperationException inval)
            {
                return Results.Conflict(new ProblemDetails() { Detail = $"Multiple entries exist in the database for that bard. This is an application error state.", Title = "Uh Oh." });
            }
        }

        /// <summary>
        /// Does nothing. Will delete later
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(404)]
        public Task<IResult> Delete(int id)
        {
            return Task.FromResult(Results.NotFound());
        }
    }
}
