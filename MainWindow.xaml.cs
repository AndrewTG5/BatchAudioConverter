using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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

        private ObservableCollection<FolderItem> folders = new ObservableCollection<FolderItem>();


        public MainWindow()
        {
            this.InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;
            FoldersListView.ItemsSource = folders;
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
    }
}
