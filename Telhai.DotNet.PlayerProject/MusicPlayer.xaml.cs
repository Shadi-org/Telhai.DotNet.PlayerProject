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
using System.Text.RegularExpressions;

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
        
        // iTunes Service and cancellation support
        private readonly ItunesService _itunesService = new ItunesService();
        private CancellationTokenSource? _currentSearchCts;

        public MusicPlayer()
        {
            //--init all Hardcoded xaml into Elements Tree
            InitializeComponent();

            this.Loaded += MusicPlayer_Loaded;


            //this.MouseDoubleClick += MusicPlayer_MouseDoubleClick;
            //this.MouseDoubleClick += new MouseButtonEventHandler(MusicPlayer_MouseDoubleClick);

            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += Timer_Tick;

            LoadLibrary(); // <--- ADD THIS
        }

        private void MusicPlayer_Loaded(object sender, RoutedEventArgs e)
        {
            LoadLibrary();
        }



        // --- EMPTY PLACEHOLDERS TO MAKE IT BUILD ---
        private void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                PlayTrackAsync(track);
            }
            else if (mediaPlayer.Source != null)
            {
                mediaPlayer.Play();
                timer.Start();
                txtStatus.Text = "Playing";
            }
        }
        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Pause();
            txtStatus.Text = "Paused";
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Stop();
            timer.Stop();
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
        
        // Single click - show file name and path only
        private void LstLibrary_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                // Display file info without playing
                txtTrackName.Text = track.Title;
                txtFilePath.Text = track.FilePath;
                txtArtistName.Text = "-";
                txtAlbumName.Text = "-";
                txtApiStatus.Text = "Click PLAY or double-click to load song details";
            }
        }
        
        // Double click - play and fetch API data
        private void LstLibrary_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                PlayTrackAsync(track);
            }
        }
        
        // Async method to play track and fetch iTunes data
        private async void PlayTrackAsync(MusicTrack track)
        {
            // Start playing immediately
            mediaPlayer.Open(new Uri(track.FilePath));
            mediaPlayer.Play();
            timer.Start();
            txtCurrentSong.Text = track.Title;
            txtStatus.Text = "Playing";
            txtFilePath.Text = track.FilePath;
            
            // Cancel any previous API request
            _currentSearchCts?.Cancel();
            _currentSearchCts = new CancellationTokenSource();
            var token = _currentSearchCts.Token;
            
            // Reset display with defaults
            txtTrackName.Text = track.Title;
            txtArtistName.Text = "-";
            txtAlbumName.Text = "-";
            imgAlbumArt.Source = (ImageSource)Application.Current.Resources["DefaultAlbumCover"];
            txtApiStatus.Text = "Searching iTunes...";
            
            try
            {
                // Parse search term from filename
                string searchTerm = ParseSearchTermFromFilename(track.Title);
                
                // Fetch data from iTunes API (async, non-blocking)
                var trackInfo = await _itunesService.SearchOneAsync(searchTerm, token);
                
                // Check if cancelled
                if (token.IsCancellationRequested)
                    return;
                
                if (trackInfo != null)
                {
                    // Update UI with API data
                    txtTrackName.Text = trackInfo.TrackName ?? track.Title;
                    txtArtistName.Text = trackInfo.ArtistName ?? "-";
                    txtAlbumName.Text = trackInfo.AlbumName ?? "-";
                    txtApiStatus.Text = "Data loaded from iTunes";
                    
                    // Load album artwork
                    if (!string.IsNullOrEmpty(trackInfo.ArtworkUrl))
                    {
                        try
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(trackInfo.ArtworkUrl);
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            imgAlbumArt.Source = bitmap;
                        }
                        catch
                        {
                            // Keep default image on error
                        }
                    }
                }
                else
                {
                    // No results from API - show file info
                    txtTrackName.Text = track.Title;
                    txtApiStatus.Text = "No iTunes data found";
                }
            }
            catch (OperationCanceledException)
            {
                // Request was cancelled - ignore
            }
            catch (Exception ex)
            {
                // Error occurred - show file info
                txtTrackName.Text = track.Title;
                txtArtistName.Text = "-";
                txtAlbumName.Text = "-";
                txtApiStatus.Text = $"Error: {ex.Message}";
                imgAlbumArt.Source = (ImageSource)Application.Current.Resources["DefaultAlbumCover"];
            }
        }
        
        // Parse song name from filename (handles "Artist - Song" or "Song" formats)
        private string ParseSearchTermFromFilename(string filename)
        {
            // Remove common file artifacts
            string cleaned = filename.Trim();
            
            // Check for "Artist - Song" format (with hyphen separator)
            if (cleaned.Contains(" - "))
            {
                // Keep both artist and song for better search results
                return cleaned.Replace(" - ", " ");
            }
            
            // Check for "Artist-Song" format (hyphen without spaces)
            if (Regex.IsMatch(cleaned, @"^[^-]+-[^-]+$"))
            {
                return cleaned.Replace("-", " ");
            }
            
            // Return as-is for simple song names
            return cleaned;
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


        //private void MusicPlayer_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        //{
        //    MainWindow p = new MainWindow();
        //    p.Title = "YYYY";
        //    p.Show();
        //}
    }
}
