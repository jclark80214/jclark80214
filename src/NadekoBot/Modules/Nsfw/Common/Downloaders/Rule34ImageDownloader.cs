﻿using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Nsfw.Common
{
    public class Rule34ImageDownloader : ImageDownloader<Rule34Object>
    {
        public Rule34ImageDownloader(HttpClient http) : base(Booru.Rule34, http)
        {
        }

        public override async Task<List<Rule34Object>> DownloadImagesAsync(string[] tags, int page, bool isExplicit = false, CancellationToken cancel = default)
        {
            var tagString = ImageDownloaderHelper.GetTagString(tags);
            var uri = $"https://rule34.xxx/index.php?page=dapi&s=post&q=index&json=1&limit=100" +
                      $"&tags={tagString}&pid={page}";
            var images = await _http.GetFromJsonAsync<List<Rule34Object>>(uri, _serializerOptions, cancel).ConfigureAwait(false);

            if (images is null)
                return new();
            
            return images
                .Where(img => !string.IsNullOrWhiteSpace(img.Directory) && !string.IsNullOrWhiteSpace(img.Image))
                .ToList();
        }
    }
}