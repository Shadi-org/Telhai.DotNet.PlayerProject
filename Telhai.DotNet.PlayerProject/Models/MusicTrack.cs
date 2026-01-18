using System;
using System.Collections.Generic;
using System.Text;

namespace ShadiAbuJaber.DotNet.PlayerProject.Models
{
    public class MusicTrack
    {
        public string Title { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;

        // Cached metadata from iTunes API
        public string? ArtistName { get; set; }
        public string? AlbumName { get; set; }
        
        // Collection of artwork URLs (supports multiple images)
        public List<string> ArtworkUrls { get; set; } = new List<string>();
        
        // Flag to indicate if metadata has been fetched from API
        public bool HasCachedMetadata { get; set; } = false;

        // This makes sure the ListBox shows the Name, not "MyMusicPlayer.MusicTrack"
        public override string ToString()
        {
            return Title;
        }
    }
}
