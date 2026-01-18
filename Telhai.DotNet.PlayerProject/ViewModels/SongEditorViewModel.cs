using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Win32;
using ShadiAbuJaber.DotNet.PlayerProject.Models;

namespace ShadiAbuJaber.DotNet.PlayerProject.ViewModels
{
    public class SongEditorViewModel : INotifyPropertyChanged
    {
        private readonly MusicTrack _track;
        private string _title;
        private string _artistName;
        private string _albumName;
        private string _filePath;
        private string? _selectedArtwork;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action<MusicTrack>? OnSaveRequested;

        public SongEditorViewModel(MusicTrack track)
        {
            _track = track;
            
            // Initialize from track
            _title = track.Title;
            _artistName = track.ArtistName ?? "";
            _albumName = track.AlbumName ?? "";
            _filePath = track.FilePath;
            
            // Initialize artwork collection
            ArtworkUrls = new ObservableCollection<string>(track.ArtworkUrls);
            if (ArtworkUrls.Count > 0)
            {
                _selectedArtwork = ArtworkUrls[0];
            }

            // Initialize commands
            SaveCommand = new RelayCommand(Save);
            AddImageCommand = new RelayCommand(AddImage);
            RemoveImageCommand = new RelayCommand(RemoveImage, () => SelectedArtwork != null);
        }

        public string Title
        {
            get => _title;
            set
            {
                if (_title != value)
                {
                    _title = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ArtistName
        {
            get => _artistName;
            set
            {
                if (_artistName != value)
                {
                    _artistName = value;
                    OnPropertyChanged();
                }
            }
        }

        public string AlbumName
        {
            get => _albumName;
            set
            {
                if (_albumName != value)
                {
                    _albumName = value;
                    OnPropertyChanged();
                }
            }
        }

        public string FilePath
        {
            get => _filePath;
            set
            {
                if (_filePath != value)
                {
                    _filePath = value;
                    OnPropertyChanged();
                }
            }
        }

        public string? SelectedArtwork
        {
            get => _selectedArtwork;
            set
            {
                if (_selectedArtwork != value)
                {
                    _selectedArtwork = value;
                    OnPropertyChanged();
                    (RemoveImageCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public ObservableCollection<string> ArtworkUrls { get; }

        public ICommand SaveCommand { get; }
        public ICommand AddImageCommand { get; }
        public ICommand RemoveImageCommand { get; }

        private void Save()
        {
            // Update the track with edited values
            _track.Title = Title;
            _track.ArtistName = string.IsNullOrWhiteSpace(ArtistName) ? null : ArtistName;
            _track.AlbumName = string.IsNullOrWhiteSpace(AlbumName) ? null : AlbumName;
            _track.ArtworkUrls = new System.Collections.Generic.List<string>(ArtworkUrls);
            _track.HasCachedMetadata = true;

            // Raise save event
            OnSaveRequested?.Invoke(_track);
        }

        private void AddImage()
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.gif;*.bmp|All Files|*.*",
                Title = "Select Album Art Image"
            };

            if (ofd.ShowDialog() == true)
            {
                string imagePath = ofd.FileName;
                ArtworkUrls.Add(imagePath);
                
                // Select the newly added image
                SelectedArtwork = imagePath;
            }
        }

        private void RemoveImage()
        {
            if (SelectedArtwork != null && ArtworkUrls.Contains(SelectedArtwork))
            {
                int index = ArtworkUrls.IndexOf(SelectedArtwork);
                ArtworkUrls.Remove(SelectedArtwork);
                
                // Select next available or null
                if (ArtworkUrls.Count > 0)
                {
                    SelectedArtwork = ArtworkUrls[Math.Min(index, ArtworkUrls.Count - 1)];
                }
                else
                {
                    SelectedArtwork = null;
                }
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Simple ICommand implementation for MVVM
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter) => _execute();

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
