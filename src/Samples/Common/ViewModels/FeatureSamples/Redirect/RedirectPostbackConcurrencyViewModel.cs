using System.Diagnostics.Metrics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotVVM.Core.Storage;
using DotVVM.Framework.ViewModel;

namespace DotVVM.Samples.BasicSamples.ViewModels.FeatureSamples.Redirect
{
    public class RedirectPostbackConcurrencyViewModel : DotvvmViewModelBase
    {
        public static int GlobalCounter = 0;
        private readonly IReturnedFileStorage returnedFileStorage;

        [Bind(Direction.ServerToClient)]
        public int Counter { get; set; } = GlobalCounter;

        public int MiniCounter { get; set; } = 0;

        [FromQuery("empty")]
        public bool IsEmptyPage { get; set; } = false;
        [FromQuery("loadDelay")]
        public int LoadDelay { get; set; } = 0;

        public RedirectPostbackConcurrencyViewModel(IReturnedFileStorage returnedFileStorage)
        {
            this.returnedFileStorage = returnedFileStorage;
        }
        public override async Task Init()
        {
            await Task.Delay(LoadDelay); // delay to enable user to click DelayIncrement button between it succeeding and loading the next page
            await base.Init();
        }

        public async Task DelayIncrement()
        {
            await Task.Delay(1000);

            Interlocked.Increment(ref GlobalCounter);

            Context.RedirectToRoute(Context.Route.RouteName, query: new { empty = true, loadDelay = 2000 });
        }

        public async Task GetFileStandard()
        {
            await Context.ReturnFileAsync("test file"u8.ToArray(), "test.txt", "text/plain");
        }

        public async Task GetFileCustom()
        {
            var metadata = new ReturnedFileMetadata()
            {
                FileName = "test_custom.txt",
                MimeType = "text/plain",
                AttachmentDispositionType = "attachment"
            };

            var stream = new MemoryStream("test custom file"u8.ToArray());
            var generatedFileId = await returnedFileStorage.StoreFileAsync(stream, metadata).ConfigureAwait(false);

            var url = Context.TranslateVirtualPath("~/_dotvvm/returnedFile?id=" + generatedFileId);
            Context.RedirectToUrl(url);
        }
    }
}
