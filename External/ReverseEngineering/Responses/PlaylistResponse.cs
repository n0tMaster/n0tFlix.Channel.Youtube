using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using n0tFlix.YoutubeManager.Exceptions;
using n0tFlix.YoutubeManager.Internal;
using n0tFlix.YoutubeManager.Internal.Extensions;

namespace n0tFlix.YoutubeManager.ReverseEngineering.Responses
{
    internal partial class PlaylistResponse
    {
        private readonly JsonElement _root;

        public PlaylistResponse(JsonElement root) => _root = root;

        public bool IsPlaylistAvailable() => _root
            .GetPropertyOrNull("title") != null;

        public string GetTitle() => _root
            .GetProperty("title")
            .GetString();

        public string? TryGetAuthor() => _root
            .GetPropertyOrNull("author")?
            .GetString();

        public string? TryGetDescription() => _root
            .GetPropertyOrNull("description")?
            .GetString();

        public long? TryGetViewCount() => _root
            .GetPropertyOrNull("views")?
            .GetInt64();

        public long? TryGetLikeCount() => _root
            .GetPropertyOrNull("likes")?
            .GetInt64();

        public long? TryGetDislikeCount() => _root
            .GetPropertyOrNull("dislikes")?
            .GetInt64();

        public IEnumerable<Video> GetVideos() => Fallback.ToEmpty(
            _root
                .GetPropertyOrNull("video")?
                .EnumerateArray()
                .Select(j => new Video(j))
        );
    }

    internal partial class PlaylistResponse
    {
        public class Video
        {
            private readonly JsonElement _root;

            public Video(JsonElement root) => _root = root;

            public string GetId() => _root
                .GetProperty("encrypted_id")
                .GetString();

            public string GetAuthor() => _root
                .GetProperty("author")
                .GetString();

            public string GetChannelId() => _root
                .GetProperty("user_id")
                .GetString()
                .Pipe(id => "UC" + id);

            public DateTimeOffset GetUploadDate() => _root
                .GetProperty("time_created")
                .GetInt64()
                .Pipe(Epoch.ToDateTimeOffset);

            public string GetTitle() => _root
                .GetProperty("title")
                .GetString();

            public string GetDescription() => _root
                .GetProperty("description")
                .GetString();

            public TimeSpan GetDuration() => _root
                .GetProperty("length_seconds")
                .GetDouble()
                .Pipe(TimeSpan.FromSeconds);

            public long GetViewCount() => _root
                .GetProperty("views")
                .GetString()
                .StripNonDigit()
                .ParseLong();

            public long GetLikeCount() => _root
                .GetProperty("likes")
                .GetInt64();

            public long GetDislikeCount() => _root
                .GetProperty("dislikes")
                .GetInt64();

            public IReadOnlyList<string> GetKeywords() => _root
                .GetProperty("keywords")
                .GetString()
                .Pipe(s => Regex.Matches(s, "\"[^\"]+\"|\\S+"))
                .Cast<Match>()
                .Select(m => m.Value)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim('"'))
                .ToArray();
        }
    }

    internal partial class PlaylistResponse
    {
        public static PlaylistResponse Parse(string raw) => new PlaylistResponse(
            Json.TryParse(raw) ?? throw TransientFailureException.Generic("Playlist response is broken.")
        );

        public static async Task<PlaylistResponse> GetAsync(YoutubeHttpClient httpClient, string id, int index = 0) =>
            await Retry.WrapAsync(async () =>
            {
                var url = $"https://youtube.com/list_ajax?style=json&action_get_list=1&list={id}&index={index}&hl=en";
                var raw = await httpClient.GetStringAsync(url, false);
                
                var result = Parse(raw);

                if (!result.IsPlaylistAvailable())
                    throw PlaylistUnavailableException.Unavailable(id);

                return result;
            });

        public static async Task<PlaylistResponse> GetSearchResultsAsync(YoutubeHttpClient httpClient, string query, int page = 0) =>
            await Retry.WrapAsync(async () =>
            {
                var queryEncoded = Uri.EscapeUriString(query);

                var url = $"https://youtube.com/search_ajax?style=json&search_query={queryEncoded}&page={page}&hl=en";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);

                // These headers are required for some reason
                request.Headers.Add("x-youtube-client-name", "56");
                request.Headers.Add("x-youtube-client-version", "20200911");

                // Don't ensure success here but rather return an empty list
                using var response = await httpClient.SendAsync(request);
                var raw = await response.Content.ReadAsStringAsync();

                return Parse(raw);
            });
    }
}
