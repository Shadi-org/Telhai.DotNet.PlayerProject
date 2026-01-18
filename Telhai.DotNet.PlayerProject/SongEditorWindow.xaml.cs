using System;
using System.Windows;
using ShadiAbuJaber.DotNet.PlayerProject.Models;
using ShadiAbuJaber.DotNet.PlayerProject.ViewModels;

namespace ShadiAbuJaber.DotNet.PlayerProject
{
    /// <summary>
    /// Interaction logic for SongEditorWindow.xaml
    /// MVVM-based song editor - no API calls from this window
    /// </summary>
    public partial class SongEditorWindow : Window
    {
        private readonly SongEditorViewModel _viewModel;

        /// <summary>
        /// Event raised when the track is saved with changes
        /// </summary>
        public event Action<MusicTrack>? OnTrackSaved;

        public SongEditorWindow(MusicTrack track)
        {
            InitializeComponent();

            // Create and set the ViewModel
            _viewModel = new SongEditorViewModel(track);
            DataContext = _viewModel;

            // Subscribe to save event from ViewModel
            _viewModel.OnSaveRequested += ViewModel_OnSaveRequested;
        }

        private void ViewModel_OnSaveRequested(MusicTrack track)
        {
            // Raise event to notify parent window
            OnTrackSaved?.Invoke(track);
            
            // Close the editor
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            // Unsubscribe from events
            _viewModel.OnSaveRequested -= ViewModel_OnSaveRequested;
            base.OnClosed(e);
        }
    }
}
