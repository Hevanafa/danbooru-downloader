using System;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.IO;
using System.Net;
using System.Windows;

namespace DanbooruDownloader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        void appendOutput(string text)
        {
            txbOutput.Text += text + "\n";
        }

        // Optional todo: check if the page has original size
        // Done: implement basic file downloader
        // Done: obtain the artists
        // Done: obtain the page ID
        const string ChromeUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/92.0.4515.131 Safari/537.36";

        int tempDownloadCounter = 0;

        void createDownloadsFolder()
        {
            if (!Directory.Exists("Downloads"))
                Directory.CreateDirectory("Downloads");
        }

        async Task<string> getHtmlString(Uri uri) {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(uri);
                req.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
                req.UserAgent = ChromeUserAgent;

                var res = await req.GetResponseAsync();
                var sr = new StreamReader(res.GetResponseStream());
                var HtmlString = await sr.ReadToEndAsync();

                // Dispose everything
                req = null;
                res.Dispose();
                sr.Dispose();

                return HtmlString;
            }
            catch (Exception ex) {
                appendOutput($"Error: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Get the download link without query options
        /// </summary>
        /// <param name="HtmlString"></param>
        /// <returns></returns>
        string getDownloadHref(string HtmlString) {
            // Get the download link
            // "li#post-option-download"
            var downloadTag = Regex.Match(
                HtmlString,
                @"(?<=<li id=""post-option-download"">)(.*?)(?=<\/li>)",
                RegexOptions.Singleline
            ).Value;

            var downloadHref = Regex.Match(
                downloadTag,
                @"(?<=href="")(.*?)(?="")"
            ).Value;

            return new Uri(downloadHref).GetLeftPart(UriPartial.Path);
        }

        string[] getArtists(string HtmlString) {
            // Get the artist names
            //"ul.artist-tag-list"
            var artistTagList = Regex.Match(
                HtmlString,
                @"(?<=<ul class=""artist-tag-list"">)(.*?)(?=<\/ul>)",
                RegexOptions.Singleline
            ).Value;

            return Regex.Matches(
                artistTagList,
                @"(?<=data-tag-name="")(.*?)(?="")"
            ).Cast<Match>()
            .Select(x => x.Value)
            .ToArray();
        }

        async Task<long> getContentLength(Uri fileUri) {
            var req = (HttpWebRequest)WebRequest.Create(fileUri);
            var res = await req.GetResponseAsync();

            var fileSize = res.ContentLength;

            req = null;
            res.Dispose();

            return fileSize;
        }

        async Task<bool> performDownload(string downloadHref, string targetFilename) {
            try
            {
                var currentCounter = tempDownloadCounter;
                var tempFilename = $"temp_{currentCounter}";
                tempDownloadCounter++;

                // Start downloading
                using (var wc = new WebClient())
                {
                    wc.DownloadProgressChanged += (sender, e) =>
                    {
                        prgDownload.Value = e.BytesReceived / (double)e.TotalBytesToReceive * 100d;
                    };

                    await wc.DownloadFileTaskAsync(downloadHref, tempFilename);
                }

                if (File.Exists(targetFilename))
                    File.Delete(targetFilename);

                File.Move(
                    tempFilename,
                    targetFilename
                );

                return true;
            }
            catch (Exception ex) {
                appendOutput("Error: " + ex.Message);
                return false;
            }
        }

        async void startDownload()
        {
            // Sample: https://danbooru.donmai.us/posts/4664184?q=squchan+

            try
            {
                var uri = new Uri(txbUri.Text);

                if (!uri.ToString().Contains("danbooru.donmai.us/posts"))
                    throw new ArgumentException("The URL isn't a link to a Danbooru post.");

                appendOutput("Starting download...");

                var HtmlString = await getHtmlString(uri);

                appendOutput($"Page is completely loaded ({HtmlString.Length} chars)");

                var downloadHref = getDownloadHref(HtmlString);
                var ext = Path.GetExtension(downloadHref);
                var artists = getArtists(HtmlString);
                var postId = Path.GetFileName(uri.GetLeftPart(UriPartial.Path));


                var fileSize = await getContentLength(new Uri(downloadHref));

                // Prepare the directory & the file name
                appendOutput($"Downloading from {downloadHref}\nFile size: {fileSize} bytes");
                createDownloadsFolder();

                var targetFilename = Path.Combine(
                    "Downloads",
                    string.Join(",", artists) +
                    " - " +
                    postId +
                    ext
                );

                appendOutput($"Saving as \"{targetFilename}\"");

                //Path.GetFileName(
                //    new Uri(href).GetLeftPart(UriPartial.Path)
                //)


                if (await performDownload(downloadHref, targetFilename))
                    appendOutput("The download is finished.");
                else appendOutput("The download is failed.");
            }
            catch (Exception ex)
            {
                appendOutput($"Error: {ex.Message}");
            }
            finally
            {
                btnDownload.IsEnabled = true;
            }
        }

        private void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            btnDownload.IsEnabled = false;
            startDownload();
        }

        private void BtnDownloadsFolder_Click(object sender, RoutedEventArgs e)
        {
            createDownloadsFolder();

            Process.Start(
                "explorer.exe",
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Downloads"
                )
            );
        }

        private void TxbOutput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            txbOutput.ScrollToEnd();
        }
    }
}
