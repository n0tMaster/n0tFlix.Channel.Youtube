using System;
using System.Linq;
using System.Threading.Tasks;
using n0tFlix.YoutubeManager.Common;
using n0tFlix.YoutubeManager.Exceptions;
using n0tFlix.YoutubeManager.ReverseEngineering;
using n0tFlix.YoutubeManager.ReverseEngineering.Responses;
using n0tFlix.YoutubeManager.Videos.ClosedCaptions;
using n0tFlix.YoutubeManager.Videos.Streams;

namespace n0tFlix.YoutubeManager.Videos
{
    /// <summary>
    /// Queries related to YouTube videos.
    /// </summary>
    public class VideoClient
    {
        private readonly YoutubeHttpClient _httpClient;

        /// <summary>
        /// Queries related to media streams of YouTube videos.
        /// </summary>
        public StreamClient Streams { get; }

        /// <summary>
        /// Queries related to closed captions of YouTube videos.
        /// </summary>
        public ClosedCaptionClient ClosedCaptions { get; }

        /// <summary>
        /// Initializes an instance of <see cref="VideoClient"/>.
        /// </summary>
        internal VideoClient(YoutubeHttpClient httpClient)
        {
            _httpClient = httpClient;

            Streams = new StreamClient(httpClient);
            ClosedCaptions = new ClosedCaptionClient(httpClient);
        }

        private async Task<Video> GetVideoFromMixPlaylistAsync(VideoId id)
        {
            var playlistInfo = await PlaylistResponse.GetAsync(_httpClient, "RD" + id.Value);
            var video = playlistInfo.GetVideos().First(x => x.GetId() == id.Value);

            return new Video(
                id,
                video.GetTitle(),
                video.GetAuthor(),
                video.GetChannelId(),
                video.GetUploadDate(),
                video.GetDescription(),
                video.GetDuration(),
                new ThumbnailSet(id),
                video.GetKeywords(),
                new Engagement(
                    video.GetViewCount(),
                    video.GetLikeCount(),
                    video.GetDislikeCount()
                )
            );
        }

        private async Task<Video> GetVideoFromWatchPageAsync(VideoId id)
        {
            var videoInfoResponse = await VideoInfoResponse.GetAsync(_httpClient, id);
            var playerResponse = videoInfoResponse.GetPlayerResponse();

            var watchPage = await WatchPage.GetAsync(_httpClient, id);

            return new Video(
                id,
                playerResponse.GetVideoTitle(),
                playerResponse.GetVideoAuthor(),
                playerResponse.GetVideoChannelId(),
                playerResponse.GetVideoUploadDate(),
                playerResponse.GetVideoDescription(),
                playerResponse.GetVideoDuration(),
                new ThumbnailSet(id),
                playerResponse.GetVideoKeywords(),
                new Engagement(
                    playerResponse.TryGetVideoViewCount() ?? 0,
                    watchPage.TryGetVideoLikeCount() ?? 0,
                    watchPage.TryGetVideoDislikeCount() ?? 0
                )
            );
        }
        
        /// <summary>
        /// Gets the metadata associated with the specified video.
        /// </summary>
        public async Task<Video> GetAsync(VideoId id)
        {
            // We can try to extract video metadata from two sources: mix playlist and the video watch page.
            // First is significantly faster but doesn't always work.

            try
            {
                return await GetVideoFromMixPlaylistAsync(id);
            }
            catch (Exception ex) when (ex is YoutubeManagerException || ex is InvalidOperationException)
            {
                return await GetVideoFromWatchPageAsync(id);
            }
        }
    }
}
