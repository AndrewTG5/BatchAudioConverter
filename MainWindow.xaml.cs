using CSCore.Codecs;
using CSCore.Codecs.WAV;
using CSCore.DSP;
using CSCore.MediaFoundation;
using CSCore.SoundOut;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace BatchAudioConverter
{

    public class FileItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Type { get; set; }
    }

    public class FolderItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public ObservableCollection<FileItem> Files { get; set; }
    }

    public sealed partial class MainWindow : Window
    {

        private readonly ObservableCollection<FolderItem> folders = new();
        private string outputFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        private string outputFormat = null;
        private int bitrate = 192000;
        private int samplerate = 0; // 0 to match source sample rate


        public MainWindow()
        {
            InitializeComponent();
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            FoldersListView.ItemsSource = folders;
        }

        private void ImportHandler(StorageFile file)
        {
            //get the folder the file is in
            var folderPath = Path.GetDirectoryName(file.Path);

            // check if a folder exists, if so, check if the file is already in it. if not, add it
            if (folders.Count > 0)
            {
                foreach (var folder in folders)
                {
                    if (folderPath == folder.Path)
                    {
                        foreach (var f in folder.Files)
                        {
                            if (f.Path == file.Path)
                            {
                                return;
                            }

                        }
                        folder.Files.Add(new FileItem
                        {
                            Name = file.Name,
                            Path = file.Path,
                            Type = file.FileType
                        });
                        return;
                    }
                }
            }

            // else, create a new folder and add the file to it
            var newFolder = new FolderItem
            {
                Name = Path.GetFileName(folderPath),
                Path = folderPath,
                Files = new ObservableCollection<FileItem>()
            };
            newFolder.Files.Add(new FileItem
            {
                Name = file.Name,
                Path = file.Path,
                Type = file.FileType
            });
            folders.Add(newFolder);
        }

        private async void AddFilesButton_Click(object sender, RoutedEventArgs e)
        {
            var openPicker = new FileOpenPicker();
            openPicker.FileTypeFilter.Add(".mp3");
            openPicker.FileTypeFilter.Add(".wav");
            openPicker.FileTypeFilter.Add(".flac");
            openPicker.ViewMode = PickerViewMode.List;
            openPicker.SuggestedStartLocation = PickerLocationId.ComputerFolder;

            // Retrieve the window handle (HWND) of the current WinUI 3 window.
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            // Initialize the file picker with the window handle (HWND).
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);

            // Open the picker for the user to pick a file
            IReadOnlyList<StorageFile> files = await openPicker.PickMultipleFilesAsync();
            if (files.Count > 0)
            {
                foreach (var file in files)
                {
                    ImportHandler(file);
                }
            }
        }

        private async void AddFolderButton_Click(object sender, RoutedEventArgs e)
        {
            FolderPicker folderPicker = new FolderPicker();
            folderPicker.FileTypeFilter.Add(".mp3");
            folderPicker.FileTypeFilter.Add(".wav");
            folderPicker.FileTypeFilter.Add(".flac");
            folderPicker.ViewMode = PickerViewMode.List;
            folderPicker.SuggestedStartLocation = PickerLocationId.ComputerFolder;

            // Retrieve the window handle (HWND) of the current WinUI 3 window.
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            // Initialize the file picker with the window handle (HWND).
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hWnd);

            StorageFolder folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                var files = await folder.GetFilesAsync();
                foreach (var file in files)
                {
                    if (file.FileType == ".mp3" || file.FileType == ".wav" || file.FileType == ".flac")
                    {
                        ImportHandler(file);
                    }
                }
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            // if the user has selected a folder, delete the folder, else delete the file
            Button button = sender as Button;
            if (button.DataContext is FolderItem folder)
            {
                folders.Remove(folder);
            }
            else if (button.DataContext is FileItem file)
            {
                foreach (var folderItem in folders)
                {
                    if (folderItem.Path == Path.GetDirectoryName(file.Path))
                    {
                        folderItem.Files.Remove(file);
                        if (folderItem.Files.Count == 0)
                        {
                            folders.Remove(folderItem);
                        }
                        return;
                    }
                }
            }
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton radioButton = sender as RadioButton;
            outputFormat = radioButton.Content.ToString().ToLower();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // picks a folder to save the converted files to
            FolderPicker folderPicker = new FolderPicker();
            folderPicker.ViewMode = PickerViewMode.List;
            folderPicker.SuggestedStartLocation = PickerLocationId.ComputerFolder;

            // Retrieve the window handle (HWND) of the current WinUI 3 window.
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            // Initialize the file picker with the window handle (HWND).
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hWnd);

            StorageFolder folder = folderPicker.PickSingleFolderAsync().AsTask().Result;
            if (folder != null)
            {
                outputFolder = folder.Path;
            }
        }

        private async void ConfigButton_Click(object sender, RoutedEventArgs e)
        {
            if (outputFormat == null)
            {
                ContentDialog noFormatDialog = new()
                {
                    Title = "Unable to configure",
                    Content = "You did not select an output format",
                    CloseButtonText = "Ok",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = Content.XamlRoot
                };
                await noFormatDialog.ShowAsync();
                return;
            }
            else if (outputFormat == "wav")
            {
                ContentDialog configDialog = new()
                {
                    Title = "Configure format",
                    Content = new StackPanel
                    {
                        Children =
                        {
                            new TextBlock { Text = "Sample rate (Hz). Set to 0 to match source sample rate" },
                            new TextBox { Text = samplerate.ToString(), Name = "SampleRateTextBox" }                            
                        }
                    },
                    CloseButtonText = "Cancel",
                    PrimaryButtonText = "Ok",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = Content.XamlRoot

                };
                configDialog.PrimaryButtonClick += (s, args) =>
                {
                    samplerate = int.Parse(((TextBox)configDialog.FindName("SampleRateTextBox")).Text);
                };
                await configDialog.ShowAsync();
                return;
            }
            else
            {
                ContentDialog configDialog = new()
                {
                    Title = "Configure format",
                    Content = new StackPanel
                    {
                        Children =
                        {                         
                            new TextBlock { Text = "Sample rate (Hz). Set to 0 to match source sample rate" },
                            new TextBox { Text = samplerate.ToString(), Name = "SampleRateTextBox" },
                            new TextBlock { Text = "Bitrate (kbps)" },
                            new Slider { Value = bitrate/1000, Minimum = 96, Maximum = 320, StepFrequency = 2, Name = "BitrateSlider" },
                        }
                    },
                    CloseButtonText = "Cancel",
                    PrimaryButtonText = "Ok",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = Content.XamlRoot

                };
                configDialog.PrimaryButtonClick += (s, args) =>
                {
                    bitrate = (int)((Slider)configDialog.FindName("BitrateSlider")).Value * 1000;
                    samplerate = int.Parse(((TextBox)configDialog.FindName("SampleRateTextBox")).Text);
                };
                await configDialog.ShowAsync();
                return;
            }
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (outputFormat == null)
            {
                ContentDialog noFormatDialog = new()
                {
                    Title = "Unable to export",
                    Content = "You did not select an output format",
                    CloseButtonText = "Ok",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = Content.XamlRoot
                };
                await noFormatDialog.ShowAsync();
                return;
            }

            // create a list of files to convert
            List<string> filesToConvert = new();
            foreach (var folder in folders)
            {
                foreach (var file in folder.Files)
                {
                    filesToConvert.Add(file.Path);
                }
            }

            TextBlock progressText = new() { Text = $"You are exporting {filesToConvert.Count} file(s) to \"{outputFolder}\" in \"{outputFormat.ToUpper()}\" format" };
            ProgressBar progressBar = new() { Minimum = 0, Maximum = filesToConvert.Count };

            ContentDialog exportDialog = new()
            {
                Title = "Export files",
                Content = new StackPanel
                {
                    Children =
            {
                progressText,
                progressBar
            }
                },
                CloseButtonText = "Cancel",
                PrimaryButtonText = "Export",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot
            };

            // Create a Progress<int> object and subscribe to its ProgressChanged event
            Progress<int> progress = new();
            progress.ProgressChanged += (s, convertedFilesCount) =>
            {
                // Update the UI with the progress information
                // This code will run on the UI thread
                progressText.Text = $"Converted {convertedFilesCount} of {filesToConvert.Count} files";
                progressBar.Value = convertedFilesCount;
            };

            // Thread-safe counter
            int convertedFilesCount = 0;

            exportDialog.PrimaryButtonClick += async (s, args) =>
            {
                args.Cancel = true; // Keep the dialog open

                // remove buttons
                exportDialog.CloseButtonText = "Close";
                exportDialog.PrimaryButtonText = null;
                exportDialog.DefaultButton = ContentDialogButton.Close;

                // Convert the files in parallel and report progress
                await Task.Run(() =>
                {
                    Parallel.ForEach(filesToConvert, (file) =>
                    {
                        // Convert the file
                        ConvertFile(file, outputFolder, outputFormat, bitrate, samplerate);

                        // Report progress
                        int newCount = Interlocked.Increment(ref convertedFilesCount);
                        ((IProgress<int>)progress).Report(newCount);
                    });
                });
            };

            await exportDialog.ShowAsync();
        }

        private static void ConvertFile(string file, string outputfolder, string outputformat, int bitrate, int samplerate)
        {
            using CSCore.IWaveSource source = CodecFactory.Instance.GetCodec(file);
            switch (outputformat)
            {
                case "wav":
                    using (DirectSoundOut soundOut = new())
                    {
                        byte[] buffer;

                        if (samplerate == 0) // skip resampling if keeping source sample rate
                        {
                            buffer = new byte[source.WaveFormat.BytesPerSecond];
                            using WaveWriter waveWriter = new(Path.Combine(outputfolder, Path.GetFileNameWithoutExtension(file) + ".wav"), source.WaveFormat);
                            int read;
                            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                waveWriter.Write(buffer, 0, read);
                            }
                        }
                        else
                        {
                            using DmoResampler resampler = new(source, samplerate); // resample to specified sample rate
                            soundOut.Initialize(resampler);
                            buffer = new byte[resampler.WaveFormat.BytesPerSecond];
                            using WaveWriter waveWriter = new(Path.Combine(outputfolder, Path.GetFileNameWithoutExtension(file) + ".wav"), resampler.WaveFormat);
                            int read;
                            while ((read = resampler.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                waveWriter.Write(buffer, 0, read);
                            }
                        }
                    }
                    return;
                case "aac":
                    using (var encoder = MediaFoundationEncoder.CreateAACEncoder(source.WaveFormat, Path.Combine(outputfolder, Path.GetFileNameWithoutExtension(file) + ".aac"), bitrate))
                    {
                        if (samplerate == 0) // skip resampling if keeping source sample rate
                        {
                            byte[] buffer = new byte[source.WaveFormat.BytesPerSecond];
                            int read;
                            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                encoder.Write(buffer, 0, read);
                            }
                        }
                        else
                        {
                            using DmoResampler resampler = new(source, samplerate); // resample to specified sample rate
                            byte[] buffer = new byte[resampler.WaveFormat.BytesPerSecond];
                            int read;
                            while ((read = resampler.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                encoder.Write(buffer, 0, read);
                            }
                        }
                    }
                    return;
                case "mp3":
                    using (var encoder = MediaFoundationEncoder.CreateMP3Encoder(source.WaveFormat, Path.Combine(outputfolder, Path.GetFileNameWithoutExtension(file) + ".mp3"), bitrate))
                    {
                        if (samplerate == 0) // skip resampling if keeping source sample rate
                        {
                            byte[] buffer = new byte[source.WaveFormat.BytesPerSecond];
                            int read;
                            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                encoder.Write(buffer, 0, read);
                            }
                        }
                        else
                        {
                            using DmoResampler resampler = new(source, samplerate); // resample to specified sample rate
                            byte[] buffer = new byte[resampler.WaveFormat.BytesPerSecond];
                            int read;
                            while ((read = resampler.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                encoder.Write(buffer, 0, read);
                            }
                        }
                    }
                    return;
                default:
                    return;

            }
        }

    }
}
