using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MahApps.Metro.Controls;
using System.IO;
using System.Diagnostics;
using NAudio;
using NAudio.Wave;
using System.Windows.Threading;
using System.ComponentModel;
using Mahanaim.SoundTouch;


namespace Mahanaim
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private object dummyNode = null;
        public string SelectedImagePath { get; set; }
        private string SelectedFullPath { get; set; }
        private string TempFile { get; set; }
        private FileInfo file { get; set; }
        private bool IsPlaying = false;
        private bool IsStopped = true;


        // Slider
        private DispatcherTimer timer = new DispatcherTimer();
        private double sliderPosition;
        const double sliderMax = 100.0;

        // Editing
        private bool IsEditEnding = false;

        private AudioPlayback audioPlayback;
        private VarispeedSampleProvider speedControl; //todo

        public MainWindow()
        {
            InitializeComponent();

            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += TimerOnTick;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            audioPlayback = new AudioPlayback();

            foreach (string s in Directory.GetLogicalDrives())
            {
                TreeViewItem item = new TreeViewItem();
                item.Header = s;
                item.Tag = s;
                item.FontWeight = FontWeights.Normal;
                item.Items.Add(dummyNode);
                item.Expanded += new RoutedEventHandler(Folder_Expanded);
                foldersItem.Items.Add(item);
            }

            foreach (string s in Directory.GetLogicalDrives())
            {
                TreeViewItem item = new TreeViewItem();
                item.Header = s;
                item.Tag = s;
                item.FontWeight = FontWeights.Normal;
                item.Items.Add(dummyNode);
                item.Expanded += new RoutedEventHandler(Folder_Expanded);
                SaveTreeView.Items.Add(item);
            }



        }

        #region Folders
        void Folder_Expanded(object sender, RoutedEventArgs e)
        {
            TreeViewItem item = (TreeViewItem)sender;
            if (item.Items.Count == 1 && item.Items[0] == dummyNode)
            {
                item.Items.Clear();
                try
                {
                    foreach (string s in Directory.GetDirectories(item.Tag.ToString()))
                    {
                        TreeViewItem subitem = new TreeViewItem();
                        subitem.Header = s.Substring(s.LastIndexOf("\\") + 1);
                        subitem.Tag = s;
                        subitem.FontWeight = FontWeights.Normal;
                        subitem.Items.Add(dummyNode);
                        subitem.Expanded += new RoutedEventHandler(Folder_Expanded);
                        item.Items.Add(subitem);
                    }
                }
                catch (Exception) { }
            }
        }

        private void FoldersItem_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            TreeView tree = (TreeView)sender;
            TreeViewItem temp = ((TreeViewItem)tree.SelectedItem);

            if (temp == null)
                return;

            SelectedImagePath = "";
            string temp1 = "";
            string temp2 = "";
            while (true)
            {
                temp1 = temp.Header.ToString();
                if (temp1.Contains(@"\"))
                {
                    temp2 = "";
                }
                SelectedImagePath = temp1 + temp2 + SelectedImagePath;
                if (temp.Parent.GetType().Equals(typeof(TreeView)))
                {
                    break;
                }
                temp = ((TreeViewItem)temp.Parent);
                temp2 = @"\";
            }
            //show user selected path
            if (tree.Name == "SaveTreeView")
                lblTo.Content = SelectedImagePath;
            else
                lblFrom.Content = SelectedImagePath;

            GetFolderList(SelectedImagePath, sender);
        }

        private void GetFolderList(string path, object sender)
        {
            TreeView tree = (TreeView)sender;

            try
            {
                if (Directory.Exists(path))
                {
                    DirectoryInfo di = new DirectoryInfo(path);
                    FileInfo[] files = di.GetFiles();

                    if (tree.Name == "SaveTreeView")
                    {
                        FolderListViewRight.Items.Clear();
                        foreach (FileInfo f in files)
                        {
                            FolderListViewRight.Items.Add(f.Name);
                        }
                    }
                    else
                    {
                        LeftGridView.Items.Clear();
                        List<DisplayIndex> list = new List<DisplayIndex>();
                        foreach (FileInfo f in files)
                        {
                            if (f.Extension == ".mp3")
                            {
                                list.Add(new DisplayIndex()
                                {
                                    Name = f.Name.Replace(f.Extension, ""),
                                    CreateTime = f.CreationTime,
                                    FullName = f.FullName
                                });
                            }
                        }
                        LeftGridView.ItemsSource = list;
                    }
                }
                else
                {
                    MessageBox.Show("Folder doesn't exist");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StopPlayer();

            DataGrid lv = (DataGrid)sender;
            DisplayIndex item = (DisplayIndex)lv.SelectedItem;
            SelectedFullPath = item.FullName;
            Debug.WriteLine("Selected ::: " + item.FullName);
        }


        #endregion

        /// <summary>
        /// Calculate & Update slider position
        /// </summary>
        private void TimerOnTick(object sender, EventArgs eventArgs)
        {
            if (audioPlayback.fileStream != null && !(audioPlayback.fileStream.Position >= audioPlayback.fileStream.Length))
            {
                sliderPosition = Math.Min(sliderMax, audioPlayback.fileStream.Position * sliderMax / audioPlayback.fileStream.Length);

                ProgressSlider.Value = sliderPosition;
                DisplayPosition();

                Debug.WriteLine(audioPlayback.fileStream.Position + " :: " + sliderPosition);
            }
            else
            {
                // Reached maximum... STOP
                StopPlayer();
            }
        }

        private void DisplayPosition()
        {
            if (audioPlayback.fileStream != null)
                lbTime.Content = audioPlayback.fileStream.CurrentTime.ToString("mm\\:ss");
            else
                lbTime.Content = "00:00";
        }


        #region Player
        private void Play_Click(object sender, RoutedEventArgs e)
        {
            DisplayIndex item = (DisplayIndex)LeftGridView.SelectedItem;

            if (item == null)
            {
                MessageBox.Show("Please select a Mp3");
                return;
            }

            // Very first
            if (IsStopped && !IsPlaying)
            {
                audioPlayback.Load(item.FullName);
                audioPlayback.Play();
                IsPlaying = true;
                Debug.WriteLine("Playing ::: " + item.FullName);
                timer.Start();
            }
            // Pause it
            else if (IsPlaying)
            {
                Pause();
            }
            else
            {
                audioPlayback.Play();
                Debug.WriteLine("Play AGAIN ::: " + item.FullName);
                IsPlaying = true;
                IsStopped = false;
                timer.Start();
            }
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            StopPlayer();
        }

        private void StopPlayer()
        {
            audioPlayback.Stop();
            audioPlayback.Dispose();
            timer.Stop();
            ProgressSlider.Value = 0;
            DisplayPosition();

            IsStopped = true;
            IsPlaying = false;

            Debug.WriteLine("### Stop / Dispose ###");
        }

        private void Pause()
        {
            audioPlayback.Pause();
            Debug.WriteLine("::: Paused ::: ");
            IsPlaying = false;
            IsStopped = false;
            timer.Stop();
        }

        private void ProgressSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            // Stop updating tick
            timer.Stop();
        }

        private void ProgressSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            UpdateTick(sender);
        }

        private void ProgressSlider_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // update tick
            UpdateTick(sender);
        }

        private void UpdateTick(object sender)
        {
            Slider s = (Slider)sender;

            if (IsStopped && !IsPlaying)
            {
                ProgressSlider.Value = 0;
                return;
            }

            if (audioPlayback.fileStream != null && IsPlaying)
            {
                timer.Start();
                sliderPosition = s.Value;
                audioPlayback.fileStream.Position = CalculateStreamPosition(audioPlayback, sliderPosition, sliderMax);
            }
            else
            {
                // paused but moved
                sliderPosition = s.Value;
                audioPlayback.fileStream.Position = CalculateStreamPosition(audioPlayback, sliderPosition, sliderMax);
                DisplayPosition();
            }
        }

        private long CalculateStreamPosition(AudioPlayback audioPlayback, double sliderPosition, double sliderMax)
        {
            return Math.Min(audioPlayback.fileStream.Length, Convert.ToInt64((sliderPosition * audioPlayback.fileStream.Length) / sliderMax));
        }

        private void Split_Click(object sender, RoutedEventArgs e)
        {
            // Check if SelectedFullPath
            if (string.IsNullOrEmpty(SelectedFullPath))
            {
                MessageBox.Show("파일을 먼저 선택해 주세요");
                return;
            }

            // Pause the player
            if (IsPlaying)
                Pause();

            Debug.WriteLine("Split from " + SelectedFullPath);

            file = new FileInfo(SelectedFullPath);

            if (!file.Exists)
            {
                MessageBox.Show("이러한 파일이 없습니다 \r\n " + SelectedFullPath);
                return;
            }

            // check if file already exist and set the name
            string newName = CheckIfExist(file.DirectoryName, file.Name.Replace(file.Extension, ""), file.Extension);

            //TrimMp3(SelectedFullPath, SelectedFullPath, new TimeSpan(0), audioPlayback.fileStream.CurrentTime);
            TrimMp3(SelectedFullPath, newName + file.Extension, audioPlayback.fileStream.CurrentTime, audioPlayback.fileStream.TotalTime);

            // Delete temp file
            if (!string.IsNullOrEmpty(TempFile))
            {
                StopPlayer();
                //File.Delete(SelectedFullPath);
                //File.Move(TempFile, SelectedFullPath);
                TempFile = null;
            }

            // Update ListView
            //FolderListViewLeft.ItemsSource = FolderListViewLeft.ItemsSource;
        }

        void TrimMp3(string inputPath, string outputPath, TimeSpan begin, TimeSpan end)
        {
            if (begin > end)
                throw new ArgumentOutOfRangeException("end", "end should be greater than begin");

            using (var reader = new JongReader(inputPath))
            {
                Id3v2Tag id3 = reader.Id3v2Tag;


                if (inputPath == outputPath)
                {
                    outputPath = System.IO.Path.Combine(file.DirectoryName, "temp.mp3");
                    TempFile = outputPath;
                }

                //if ((output.Position == 0) && (reader.Id3v2Tag != null))
                //{
                //    output.Write(reader.Id3v2Tag.RawData,
                //                 0,
                //                 reader.Id3v2Tag.RawData.Length);
                //}
                using (var writer = File.Create(outputPath))
                {
                    Mp3Frame frame;
                    while ((frame = reader.ReadNextFrame()) != null)
                        if (reader.CurrentTime >= begin)
                        {
                            if (reader.CurrentTime <= end)
                                writer.Write(frame.RawData, 0, frame.RawData.Length);
                            else break;
                        }

                }

            }
        }

        #endregion // end of Player

        private string CheckIfExist(string dir, string name, string ext)
        {
            if (File.Exists(dir + "\\" + name + ext))
            {
                // append '_01'
                return CheckIfExist(dir, name + "_01", ext);
            }
            else
            {
                return dir + "\\" + name;
            }
        }

        private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            // Adding row index to DataGrid
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }

        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (IsEditEnding)
            {
                return;
            }
            try
            {
                IsEditEnding = true;
                var prev = (DisplayIndex)e.Row.Item;
                var text = ((TextBox)e.EditingElement).Text;


                if (string.IsNullOrEmpty(text))
                {
                    MessageBox.Show("It cannot be empy name");

                    e.Cancel = true;
                    (sender as DataGrid).CancelEdit(DataGridEditingUnit.Cell);

                }
                else if (prev.Name != text)
                {
                    // todo: go change it!
                    Debug.WriteLine("#Edited to " + text);
                }
            }
            finally
            {
                IsEditEnding = false;
            }


        }

        private void DataGrid_CurrentCellChanged(object sender, EventArgs e)
        {

        }
    }

    // Columns to show on listbox
    public class DisplayIndex
    {
        public string Name { get; set; }
        public DateTime CreateTime { get; set; }
        public string FullName { get; set; }
    }
}




