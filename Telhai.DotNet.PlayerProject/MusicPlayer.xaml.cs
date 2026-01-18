using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using Telhai.DotNet.PlayerProject.Models;
using Telhai.DotNet.PlayerProject.Services;


namespace Telhai.DotNet.PlayerProject
{
    /// <summary>
    /// Interaction logic for MusicPlayer.xaml
    /// </summary>
    public partial class MusicPlayer : Window
    {
        public string MyProperty { get; set; } = "XXXX";

        private MediaPlayer mediaPlayer = new MediaPlayer();
        private DispatcherTimer timer = new DispatcherTimer();
        private List<MusicTrack> library = new List<MusicTrack>();
        private bool isDragging = false;
        private const string FILE_NAME = "library.json";

        // iTunes Service for API calls
        private readonly ItunesService _itunesService = new ItunesService();
        
        // CancellationTokenSource to cancel previous API requests when switching songs
        private CancellationTokenSource? _currentSearchCts;

        // Timer for rotating album art images during playback
        private DispatcherTimer? _imageRotationTimer;
        private int _currentImageIndex = 0;
        private MusicTrack? _currentPlayingTrack;

        public MusicPlayer()
        {
            //--init all Hardcoded xaml into Elements Tree
            InitializeComponent();

            this.Loaded += MusicPlayer_Loaded;

            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += Timer_Tick;

            // Initialize image rotation timer (rotates every 3 seconds)
            _imageRotationTimer = new DispatcherTimer();
            _imageRotationTimer.Interval = TimeSpan.FromSeconds(3);
            _imageRotationTimer.Tick += ImageRotationTimer_Tick;

            LoadLibrary();
        }

        private void MusicPlayer_Loaded(object sender, RoutedEventArgs e)
        {
            LoadLibrary();
            // Set default album art on load
            SetDefaultAlbumArt();
        }

        /// <summary>
        /// Timer tick for rotating album art images
        /// </summary>
        private void ImageRotationTimer_Tick(object? sender, EventArgs e)
        {
            if (_currentPlayingTrack != null && _currentPlayingTrack.ArtworkUrls.Count > 1)
            {
                _currentImageIndex = (_currentImageIndex + 1) % _currentPlayingTrack.ArtworkUrls.Count;
                DisplayImageAtIndex(_currentImageIndex);
            }
        }

        /// <summary>
        /// Displays the image at the specified index from current track's artwork
        /// </summary>
        private void DisplayImageAtIndex(int index)
        {
            if (_currentPlayingTrack != null && index >= 0 && index < _currentPlayingTrack.ArtworkUrls.Count)
            {
                string artworkUrl = _currentPlayingTrack.ArtworkUrls[index];
                try
                {
                    imgAlbumArt.Source = new BitmapImage(new Uri(artworkUrl, UriKind.RelativeOrAbsolute));
                }
                catch
                {
                    SetDefaultAlbumArt();
                }
            }
        }

        /// <summary>
        /// Sets the default album art (clears the image, border background shows through)
        /// </summary>
        private void SetDefaultAlbumArt()
        {
            imgAlbumArt.Source = null;
        }

        /// <summary>
        /// Single click on library item - shows song name and cached metadata if available
        /// </summary>
        private void LstLibrary_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                // Display song name and file path
                txtTrackName.Text = track.Title;
                txtFilePath.Text = track.FilePath;
                
                // Display cached metadata if available (no API call)
                if (track.HasCachedMetadata)
                {
                    txtArtistName.Text = track.ArtistName ?? "";
                    txtAlbumName.Text = track.AlbumName ?? "";
                    
                    // Display first artwork if available
                    if (track.ArtworkUrls.Count > 0)
                    {
                        try
                        {
                            imgAlbumArt.Source = new BitmapImage(new Uri(track.ArtworkUrls[0], UriKind.RelativeOrAbsolute));
                        }
                        catch
                        {
                            SetDefaultAlbumArt();
                        }
                    }
                    else
                    {
                        SetDefaultAlbumArt();
                    }
                }
                else
                {
                    // No cached metadata - clear API-related fields
                    txtArtistName.Text = "";
                    txtAlbumName.Text = "";
                    SetDefaultAlbumArt();
                }
            }
        }

        /// <summary>
        /// Double click on library item - plays the song and fetches metadata
        /// </summary>
        private void LstLibrary_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                PlayTrackAsync(track);
            }
        }

        /// <summary>
        /// Play button click - plays selected song and fetches metadata
        /// </summary>
        private void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                PlayTrackAsync(track);
            }
            else if (mediaPlayer.Source != null)
            {
                // Resume paused playback
                mediaPlayer.Play();
                timer.Start();
                
                // Resume image rotation if track has multiple images
                if (_currentPlayingTrack != null && _currentPlayingTrack.ArtworkUrls.Count > 1)
                {
                    _imageRotationTimer?.Start();
                }
                
                txtStatus.Text = "Playing";
            }
        }

        /// <summary>
        /// Plays the track and uses cached metadata or fetches from iTunes API
        /// </summary>
        private async void PlayTrackAsync(MusicTrack track)
        {
            // Stop previous image rotation
            _imageRotationTimer?.Stop();
            _currentPlayingTrack = track;
            _currentImageIndex = 0;

            // Start playing immediately - don't block UI
            mediaPlayer.Open(new Uri(track.FilePath));
            mediaPlayer.Play();
            timer.Start();
            txtCurrentSong.Text = track.Title;
            txtStatus.Text = "Playing";

            // Update basic info immediately
            txtTrackName.Text = track.Title;
            txtFilePath.Text = track.FilePath;

            // Check if metadata is already cached
            if (track.HasCachedMetadata)
            {
                // Use cached metadata - no API call needed
                DisplayCachedMetadata(track);
                txtStatus.Text = "Playing";
            }
            else
            {
                // Clear while loading
                SetDefaultAlbumArt();
                txtArtistName.Text = "";
                txtAlbumName.Text = "";

                // Cancel any previous API request
                _currentSearchCts?.Cancel();
                _currentSearchCts = new CancellationTokenSource();
                var cancellationToken = _currentSearchCts.Token;

                // Fetch metadata from iTunes API asynchronously
                await FetchAndDisplayMetadataAsync(track, cancellationToken);
            }
        }

        /// <summary>
        /// Displays cached metadata and starts image rotation if multiple images
        /// </summary>
        private void DisplayCachedMetadata(MusicTrack track)
        {
            txtTrackName.Text = track.Title;
            txtArtistName.Text = track.ArtistName ?? "";
            txtAlbumName.Text = track.AlbumName ?? "";
            txtFilePath.Text = track.FilePath;

            // Display first image
            if (track.ArtworkUrls.Count > 0)
            {
                DisplayImageAtIndex(0);
                
                // Start rotation timer if multiple images
                if (track.ArtworkUrls.Count > 1)
                {
                    _imageRotationTimer?.Start();
                }
            }
            else
            {
                SetDefaultAlbumArt();
            }
        }

        /// <summary>
        /// Fetches metadata from iTunes API, updates UI, and caches to JSON
        /// </summary>
        private async Task FetchAndDisplayMetadataAsync(MusicTrack track, CancellationToken cancellationToken)
        {
            try
            {
                // Parse search term from file name (artist - song or just song)
                string searchTerm = ParseSearchTermFromFileName(track.Title);

                txtStatus.Text = "Fetching metadata...";

                // Call iTunes API asynchronously
                var trackInfo = await _itunesService.SearchOneAsync(searchTerm, cancellationToken);

                // Check if operation was cancelled
                cancellationToken.ThrowIfCancellationRequested();

                if (trackInfo != null)
                {
                    // Update track with metadata from API (cache it)
                    track.Title = trackInfo.TrackName ?? track.Title;
                    track.ArtistName = trackInfo.ArtistName;
                    track.AlbumName = trackInfo.AlbumName;
                    
                    // Store artwork URL in collection
                    if (!string.IsNullOrWhiteSpace(trackInfo.ArtworkUrl))
                    {
                        track.ArtworkUrls.Clear();
                        track.ArtworkUrls.Add(trackInfo.ArtworkUrl);
                    }
                    
                    track.HasCachedMetadata = true;

                    // Save to JSON file
                    SaveLibrary();

                    // Update UI with metadata
                    DisplayCachedMetadata(track);
                    
                    // Refresh library display to show updated title
                    UpdateLibraryUI();

                    txtStatus.Text = "Playing";
                }
                else
                {
                    // No results from API - show file info
                    DisplayFileInfoOnly(track);
                    txtStatus.Text = "Playing (no metadata found)";
                }
            }
            catch (OperationCanceledException)
            {
                // Request was cancelled (user switched songs) - do nothing
            }
            catch (Exception ex)
            {
                // Error fetching metadata - show file info only
                DisplayFileInfoOnly(track);
                txtStatus.Text = $"Playing (metadata error)";
                System.Diagnostics.Debug.WriteLine($"iTunes API error: {ex.Message}");
            }
        }

        /// <summary>
        /// Parses search term from file name - handles "Artist - Song" or just "Song" format
        /// </summary>
        private string ParseSearchTermFromFileName(string fileName)
        {
            // Remove file extension if present
            string name = System.IO.Path.GetFileNameWithoutExtension(fileName);

            // Replace common separators with spaces for better search
            // Handles formats like "Artist - Song", "Artist-Song", "Artist_Song"
            name = name.Replace("-", " ").Replace("_", " ");

            // Remove extra whitespace
            name = string.Join(" ", name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

            return name;
        }

        /// <summary>
        /// Displays only file information when API fails or returns no results
        /// </summary>
        private void DisplayFileInfoOnly(MusicTrack track)
        {
            txtTrackName.Text = System.IO.Path.GetFileNameWithoutExtension(track.FilePath);
            txtArtistName.Text = "Unknown Artist";
            txtAlbumName.Text = "Unknown Album";
            txtFilePath.Text = track.FilePath;
            SetDefaultAlbumArt();
        }

        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Pause();
            _imageRotationTimer?.Stop(); // Stop image rotation when paused
            txtStatus.Text = "Paused";
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Stop();
            timer.Stop();
            _imageRotationTimer?.Stop(); // Stop image rotation when stopped
            sliderProgress.Value = 0;
            txtStatus.Text = "Stopped";
        }

        private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            mediaPlayer.Volume = sliderVolume.Value;
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Multiselect = true;
            ofd.Filter = "MP3 Files|*.mp3";

            if (ofd.ShowDialog() == true)
            {
                foreach (string file in ofd.FileNames)
                {
                    MusicTrack track = new MusicTrack
                    {
                        Title = System.IO.Path.GetFileNameWithoutExtension(file),
                        FilePath = file
                    };
                    library.Add(track);
                }
                UpdateLibraryUI();
                SaveLibrary();
            }
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                library.Remove(track);
                UpdateLibraryUI();
                SaveLibrary();
            }
        }

        /// <summary>
        /// Opens the Song Editor window for the selected track
        /// </summary>
        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                SongEditorWindow editor = new SongEditorWindow(track);
                editor.Owner = this;
                
                // Subscribe to save event
                editor.OnTrackSaved += Editor_OnTrackSaved;
                
                editor.ShowDialog();
            }
            else
            {
                MessageBox.Show("Please select a song to edit.", "No Song Selected", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Handles the track saved event from the editor
        /// </summary>
        private void Editor_OnTrackSaved(MusicTrack track)
        {
            // Save the updated library to JSON
            SaveLibrary();
            
            // Refresh UI
            UpdateLibraryUI();
            
            // If this is the currently playing track, update display
            if (_currentPlayingTrack == track)
            {
                DisplayCachedMetadata(track);
            }
            else
            {
                // Re-select to refresh display
                int index = library.IndexOf(track);
                if (index >= 0)
                {
                    lstLibrary.SelectedIndex = index;
                }
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            // Update slider ONLY if music is loaded AND user is NOT holding the handle
            if (mediaPlayer.Source != null && mediaPlayer.NaturalDuration.HasTimeSpan && !isDragging)
            {
                sliderProgress.Maximum = mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                sliderProgress.Value = mediaPlayer.Position.TotalSeconds;
            }
        }

        private void Slider_DragStarted(object sender, MouseButtonEventArgs e)
        {
            isDragging = true; // Stop timer updates
        }

        private void Slider_DragCompleted(object sender, MouseButtonEventArgs e)
        {
            isDragging = false; // Resume timer updates
            mediaPlayer.Position = TimeSpan.FromSeconds(sliderProgress.Value);
        }

        private void UpdateLibraryUI()
        {
            lstLibrary.ItemsSource = null;
            lstLibrary.ItemsSource = library;
        }

        private void SaveLibrary()
        {
            string json = JsonSerializer.Serialize(library);
            File.WriteAllText(FILE_NAME, json);
        }

        private void LoadLibrary()
        {
            if (File.Exists(FILE_NAME))
            {
                // read file
                string json = File.ReadAllText(FILE_NAME);
                // create list of music track from json
                library = JsonSerializer.Deserialize<List<MusicTrack>>(json) ?? new List<MusicTrack>();
                UpdateLibraryUI();
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            Settings settingsWin = new Settings();

            // Listen for the results
            settingsWin.OnScanCompleted += SettingsWin_OnScanCompleted;

            settingsWin.ShowDialog();
        }

        private void SettingsWin_OnScanCompleted(List<MusicTrack> newTracks)
        {
            foreach (var track in newTracks)
            {
                // Prevent duplicates based on FilePath
                if (!library.Any(x => x.FilePath == track.FilePath))
                {
                    library.Add(track);
                }
            }

            UpdateLibraryUI();
            SaveLibrary();
        }
    }
}
