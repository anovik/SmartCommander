﻿using Avalonia.Controls;
using Avalonia.Media;
using MessageBox.Avalonia.Enums;
using ReactiveUI;
using SmartCommander.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.InteropServices;

namespace SmartCommander.ViewModels
{
    public enum SortingBy
    {
        SortingByName = 0,
        SortingByExt,
        SortingBySize,
        SortingByDate,        
    }

    public class FilesPaneViewModel : ViewModelBase
    {
        private string _currentDirectory;

        private int _totalFiles = 0;
        private int _totalFolders = 0;

        private bool _isSelected;

        public string CurrentDirectory
        {
            get => _currentDirectory;
            set
            {
                _currentDirectory = value;
                GetFilesFolders(CurrentDirectory, FoldersFilesList);
                this.RaisePropertyChanged("CurrentDirectory");
                this.RaisePropertyChanged("CurrentDirectoryInfo");
            }
        }


        private MainWindowViewModel _mainVM;

        public string CurrentDirectoryInfo
        {
            get { return string.Format("Files: {0}, folders: {1}.", _totalFiles, _totalFolders); }
        }

        public FileViewModel CurrentItem { get; set; }

        public SortingBy Sorting { get; set; } = SortingBy.SortingByName;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                this.RaisePropertyChanged("IsSelected");
                this.RaisePropertyChanged("GridBorderBrush");
            }
        }

        public bool IsCurrentDirectoryDisplayed
        {
            get => OptionsModel.Instance.IsCurrentDirectoryDisplayed;            
        }


        public Brush GridBorderBrush => IsSelected ? new SolidColorBrush(Colors.LightSkyBlue) : new SolidColorBrush(Colors.Transparent);

        public ObservableCollection<FileViewModel> FoldersFilesList { get; set; } = new ObservableCollection<FileViewModel>();         
        
        public FilesPaneViewModel()
        {

        }

        public FilesPaneViewModel(MainWindowViewModel mainVM)
        {
            CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            EnterCommand = ReactiveCommand.Create(Enter);
            _mainVM = mainVM;
        }

        public ReactiveCommand<Unit, Unit> EnterCommand { get; }

        public void CellPointerPressed(object sender, object parameter)
        {
            _mainVM.SelectedPane = this;
        }

        public void SortingStarted(object sender, object parameter)
        {
            _mainVM.SelectedPane = this;       
            
            DataGridColumnEventArgs? args = parameter as DataGridColumnEventArgs;
            if (args != null)
            {
                if (args.Column.Header.ToString() == "Name")
                {
                    Sorting = SortingBy.SortingByName;
                }
                if (args.Column.Header.ToString() == "Extension")
                {
                    Sorting = SortingBy.SortingByExt;
                }
                if (args.Column.Header.ToString() == "Size")
                {
                    Sorting = SortingBy.SortingBySize;
                }
                if (args.Column.Header.ToString() == "Date")
                {
                    Sorting = SortingBy.SortingByDate;
                }

                args.Handled = true;
            }
        }

        public void BeginningEdit(object sender, object parameter)
        {
            DataGridBeginningEditEventArgs? args = parameter as DataGridBeginningEditEventArgs;
            if (args != null)
            {
                args.Cancel = true;
            }
        }

        public void Tapped(object sender, object parameter)
        {
            _mainVM.SelectedPane = this;
        }


        public void Enter()
        {
            ProcessCurrentItem();
        }

        public void DoubleTapped(object sender, object parameter)
        {
            //TODO: if parameter source column header, then ignore
            ProcessCurrentItem();
        }     
            
        public void Execute(string? command)
        {
            if (!string.IsNullOrEmpty(command))
            {
                new Process
                {
                    StartInfo = new ProcessStartInfo(command)
                    {
                        WorkingDirectory = CurrentDirectory,
                        UseShellExecute = true
                    }
                }.Start();
            }
        }

        public void Update()
        {
            // TODO: may be there is anything smarter?
            CurrentDirectory = CurrentDirectory;
        }

        public void View()
        {
            if (!CurrentItem.IsFolder)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {                    
                    Process.Start("less", CurrentItem.FullName);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start("LTFViewr5u.exe", CurrentItem.FullName);
                }
            }
            else
            {
                MessageBox_Show(null, "Can't view the folder", "Alert", ButtonEnum.Ok);
            }
        }

        public void Edit()
        {
            if (!CurrentItem.IsFolder)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {                    
                    Process.Start("vi", CurrentItem.FullName);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start("notepad.exe", CurrentItem.FullName);
                }
            }
            else
            {
               MessageBox_Show(null, "Can't edit the folder", "Alert", ButtonEnum.Ok);
            }
        }

        public bool NonEmptyFolder()
        {
            return OptionsModel.Instance.ConfirmationWhenDeleteNonEmpty && 
                CurrentItem.IsFolder && 
                !IsDirectoryEmpty(CurrentItem.FullName);
        }

        public void Delete()
        {
            try
            {
                if (CurrentItem.IsFolder)
                {
                    Directory.Delete(CurrentItem.FullName, true);
                }
                else
                {
                    File.Delete(CurrentItem.FullName);
                }               
            }
            catch
            {
            }
        }

        public bool IsDirectoryEmpty(string path)
        {
            return !Directory.EnumerateFileSystemEntries(path).Any();
        }

        public void CreateNewFolder(string name)
        {
            string newFolder = Path.Combine(CurrentDirectory, name);
            if (Directory.Exists(newFolder))
            {
                MessageBox_Show(null, "The folder already exists", "Alert", ButtonEnum.Ok);
                return;
            }
            Directory.CreateDirectory(newFolder);            
        }

        private void ProcessCurrentItem()
        {
            if (CurrentItem.IsFolder)
            {
                if (CurrentItem.FullName == "..")
                {
                    CurrentDirectory = Directory.GetParent(CurrentDirectory) != null ? Directory.GetParent(CurrentDirectory)!.FullName :
                        CurrentDirectory;
                }
                else
                {
                    CurrentDirectory = CurrentItem.FullName;
                }
            }
            else
            {
                // it is a file, open it
                new Process
                {
                    StartInfo = new ProcessStartInfo(CurrentItem.FullName)
                    {
                        UseShellExecute = true
                    }
                }.Start();
            }
        }

        private void GetFilesFolders(string dir, ObservableCollection<FileViewModel> filesFoldersList)
        {
            if (!Directory.Exists(dir) || !Path.IsPathFullyQualified(dir))
                return;
            filesFoldersList.Clear();
            _totalFolders = _totalFiles = 0;
            bool isParent = false;
            if (Directory.GetParent(CurrentDirectory) != null)
            {
                filesFoldersList.Add(new FileViewModel("..", true));
                isParent = true;
            }

            var options = new EnumerationOptions()
            {
                AttributesToSkip = OptionsModel.Instance.IsHiddenSystemFilesDisplayed ? 0 : FileAttributes.Hidden | FileAttributes.System,
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,                
            };

            var subdirectoryEntries = Directory.EnumerateDirectories(dir, "*", options);          
            var foldersList = new List<FileViewModel>();
            foreach (string subdirectory in subdirectoryEntries)
            {
                try
                {
                    foldersList.Add(new FileViewModel(subdirectory, true));
                    ++_totalFolders;
                }
                catch { }
            }          

            var filesList = new List<FileViewModel>();
            var fileEntries = Directory.EnumerateFiles(dir, "*", options);
            foreach (string fileName in fileEntries)
            {
                try
                {
                    filesList.Add(new FileViewModel(fileName, false));
                    ++_totalFiles;
                }
                catch { }
            }
            if (Sorting == SortingBy.SortingByName)
            {
                foldersList = foldersList.OrderBy(entry => entry.Name).ToList();
                filesList = filesList.OrderBy(entry => entry.Name).ToList();
            }
            else if (Sorting == SortingBy.SortingByExt)
            {
                foldersList = foldersList.OrderBy(entry => entry.Extension).ToList();
                filesList = filesList.OrderBy(entry => entry.Extension).ToList();
            }
            else if (Sorting == SortingBy.SortingBySize)
            {
                foldersList = foldersList.OrderBy(entry => entry.Size).ToList();
                filesList = filesList.OrderBy(entry => entry.Size).ToList();
            }
            else if (Sorting == SortingBy.SortingByDate)
            {
                foldersList = foldersList.OrderBy(entry => entry.DateCreated).ToList();
                filesList = filesList.OrderBy(entry => entry.DateCreated).ToList();
            }

            foreach (var folder in foldersList)
            {
                filesFoldersList.Add(folder);
            }

            foreach (var file in filesList)
            {
                filesFoldersList.Add(file);
            }     

            if (filesFoldersList.Count > 0)
            {
                CurrentItem = (isParent && filesFoldersList.Count > 1) ?
                    filesFoldersList[1] : filesFoldersList[0];
            }
        }
     
    }
}
