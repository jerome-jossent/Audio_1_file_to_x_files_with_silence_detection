using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Shapes;

using PanAndZoom;
using NAudio_JJ;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using TagLib;
using System.Xml.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Windows.Threading;
using NAudio.Wave;

namespace OneTrackToXTracks_SplitterAudio
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        void NotifyPropertyChanged([CallerMemberName] String propertyName = "") { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }
        public event PropertyChangedEventHandler? PropertyChanged;

        string _Title;

        public bool _ready_to_Process
        {
            get => ready_to_Process;
            set
            {
                if (ready_to_Process == value) return;
                ready_to_Process = value;
                NotifyPropertyChanged();
            }
        }
        bool ready_to_Process;

        enum Mode { timeFromText, timeFromSilenceDetection }

        DATA data;

        Dictionary<Silence, Pastille> silences_pastille;
        Dictionary<ListBoxItem, Silence> listitems_silence;
        Pastille previousPastilleSelected;
        Polygon sound_peaks;
        Polygon sound_silences;
        Polygon silence_selected;

        double y_time;

        public enum ZLevelOnCanvas { tracks = 0, peaks = 3, silences = 5, pastilles = 1000 }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        void Window_Loaded(object sender, RoutedEventArgs e)
        {
            INITS();
            //GetInfo();
            ZoomBorder.MoveEvent += ZoomBorder_MoveEvent;
            ZoomBorder.ZoomChangeEvent += ZoomBorder_ZoomChangeEvent;
            ZoomBorder.MouseLeftButtonWithoutMoveEvent += ZoomBorder_MouseLeftButtonWithoutMoveEvent;
            //PreProcess();
        }

        void INITS()
        {
            _Title = Title;
            file.Text = @"D:\Videos\Download Videos\MOBY - Amiga Days (Remasters Vol.1) [[FULL ALBUM]].mp3";
            //file.Text = @"D:\Ma musique\Musique\_CHIPTUNE\Best of Chiptune [8 bit music, retro visuals].mp3";
            folder.Text = @"D:\Videos\Download Videos\TEST";
            txt.Text = "Progressive Funk (Impact Inc. - Vectorball)\r\nPapoornoo2 (Apology - Demodisk 1)\r\nThe Last Knight (Alcatraz - Megademo IV)\r\nDragonsfunk (Angels - Copper Master)\r\nPelforth Blues (Alcatraz - Music Disk 1)\r\nThe Knight is Back (Alcatraz - Music Disk 1)\r\nKnulla Kuk (Quartex - Substance)\r\nLet there be Funk (Dreamdealers - Tales Of A Dream \r\nGroovy Thing (Dreamdealers - Innervision)\r\n88, Funky Avenue\r\nP.A.T.A.O.P.A.\r\nDrink My Pain Away (The Special Brothers - Live #1)\r\nKanyenamaryamalabar\r\nCortouchka !\r\nHeads Up (Alliance Design/DRD - Arkham Asylum)\r\nRaging Fire (Dreamdealers - Raging Fire)\r\nLivin' Insanity (Sanity - Arte)\r\nElekfunk (Sanity - Arte)\r\nMobyle (Sanity - Arte)\r\nMore Than Music (Alcatraz - More Than Music)";
            author.Text = "MOBY";
        }

        #region From UI
        void SelectFile_btn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                file.Text = openFileDialog.FileName;

            GetInfo(false);
        }
        void CreateFolder_btn_Click(object sender, RoutedEventArgs e)
        {
            FileInfo fileInfo = new FileInfo(file.Text);
            string folder = fileInfo.DirectoryName;
            string filename = fileInfo.Name.Substring(0, fileInfo.Name.Length - fileInfo.Extension.Length);
            this.folder.Text = folder + "\\" + filename + "\\";
        }

        void SelectFolder_btn_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                folder.Text = folderBrowserDialog.SelectedPath;
        }

        void AnalyseFileFromJSON_btn_Click(object sender, RoutedEventArgs e)
        {
            GetInfo(true);
        }

        void AnalyseFile_btn_Click(object sender, RoutedEventArgs e)
        {
            GetInfo(false);
        }

        void PreProcess_btn_Click(object sender, RoutedEventArgs e) { PreProcess(); }
        void Go_btn_Click(object sender, RoutedEventArgs e) { Process(); }

        void lbox_silence_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return;

            Silence_selected((Silence)e.AddedItems[0]);
        }

        void lbox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return;

            Title_UC selection = (Title_UC)e.AddedItems[0];
            Title_selected(selection.title);
        }

        void ZoomBorder_MoveEvent(object sender, PanAndZoom.ZoomBorderEventArgs e)
        {
            if(data==null) return;
            double y_relative = (e.mouseRelativeY - e.relativeoffsetY) / e.scaleY;
            y_time = y_relative * data.totaltime;
            TimeSpan t = TimeSpan.FromSeconds(y_time);
            string titre = _Title + " - " + t.ToString("G");
            Dispatcher.BeginInvoke(() => (Title = titre));
        }

        void ZoomBorder_ZoomChangeEvent(object sender, ZoomBorderEventArgs args)
        {
            DrawOrUpdate_SilencesPastilles();
        }

        void ZoomBorder_MouseLeftButtonWithoutMoveEvent(object sender, ZoomBorderEventArgs args)
        {
            PlayAudioHere();
        }

        void GridZoom_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawOrUpdate_SilencesPastilles();
        }

        void Silence_selected_delete(object sender, RoutedEventArgs e)
        {
            data.silences.Remove((Silence)lbox_silence.SelectedItem);
            List_Silences();
            ReDrawGraph();
        }
        #endregion

        void GetInfo(bool usejsoninstead)
        {
            //reset data
            data = new DATA();

            //Read musiquefile
            data.totaltime = NAudio_JJ.NAudio_JJ.MusicTotalSeconds(file.Text);

            //get Peaks Amplitude
            string jsonfile = AppDomain.CurrentDomain.BaseDirectory + @"json.tmp";
            if (!System.IO.File.Exists(jsonfile))
                usejsoninstead = false;

            string path = file.Text;

            if (usejsoninstead)
            {
                data.peaks = Peak.Get_Peaks_FromJson(jsonfile);
                _progressbar.Dispatcher.Invoke(new Action(() => { _progressbar.Value = 100; }));
                GetInfo_2(data.peaks, ref data.silences, ref data.titles, data.totaltime);
            }
            else
            {
                Peak.peakAnalysingEvent += Peak_peakAnalysingEvent;
                data.peaks = Peak.Get_Peaks(path);
                Peak.peakAnalysingEvent -= Peak_peakAnalysingEvent;
                string jsonString = JsonSerializer.Serialize(data.peaks);
                System.IO.File.WriteAllText(jsonfile, jsonString);
                GetInfo_2(data.peaks, ref data.silences, ref data.titles, data.totaltime);
            }
        }

        void Peak_peakAnalysingEvent(object sender, Peak.PeakAnalysingEventArgs e)
        {
            Dispatcher.Invoke(new Action(() => { _progressbar.Value = e.Val; }), DispatcherPriority.Background, null);
        }

        void GetInfo_2(List<Peak> peaks, ref List<Silence> silences, ref List<Title> titles, double totaltime_sec)
        {
            //get silences
            silences = Silence.Get_Silences(peaks, 0.005, 0.2);

            TracksFinder(silences, ref titles);

            //TRIM data
            //silences 1 et n
            if (silences[0].debut == TimeSpan.Zero.TotalSeconds)
            {
                //change piste 1
                titles[0].start = TimeSpan.FromSeconds((double)silences[0].fin);
                //delete silence 1
                silences.RemoveAt(0);
            }
            if ((double)silences[silences.Count - 1].fin >= totaltime_sec)
            {
                //change piste n
                titles[titles.Count - 1].end = TimeSpan.FromSeconds(silences[silences.Count - 1].debut);
                //delete silence n
                silences.RemoveAt(silences.Count - 1);
            }

            List_Titles();
            List_Silences();
            ReDrawGraph();
        }

        void PlayAudioHere()
        {
            NAudio_JJ.NAudio_JJ.PlayAudio(file.Text,
                    y_time,
                    y_time+2);
                    //data.totaltime);


        }

        void PreProcess()
        {
            List_Silences();
            //Read text => make a list of titles
            TitlesMaker(txt.Text, data.titles, author.Text, album.Text);
            List_Titles();

            //représentations graphiques
            ReDrawGraph();
            _ready_to_Process = true;
        }

        void ReDrawGraph()
        {
            for (int i = 0; i < rectangles.Children.Count; i++)
            {
                var item = rectangles.Children[i];
                if (item is Pastille)
                    continue;
                rectangles.Children.RemoveAt(i);
                i--;
            }
            Draw_Titles();
            Draw_Peaks(Color.FromRgb(40, 40, 40));
            Draw_Silences(Colors.White);

            DrawOrUpdate_SilencesPastilles();
        }

        void Process()
        {
            //Extraction des titres du fichier original
            foreach (Title titre in data.titles)
            {
                //create file
                NAudio_JJ.NAudio_JJ.AudioExtractor(file.Text,
                    titre.fullFileName,
                    titre.start.TotalSeconds,
                    titre.end.TotalSeconds);

                //create ID3 Tag


                FileStream fileStream = new FileStream(titre.fullFileName, new FileStreamOptions() { Access = FileAccess.ReadWrite });
                var tagFile = TagLib.File.Create(new StreamFileAbstraction(titre.fullFileName, fileStream, fileStream));

                var tagsv2 = tagFile.GetTag(TagTypes.Id3v2);
                tagsv2.Album = titre.album;
                tagsv2.Artists = new string[] { titre.author };
                tagsv2.AlbumArtists = new string[] { titre.author };
                tagsv2.Track = (uint)titre.index;
                tagsv2.Title = titre.titleraw;
                tagsv2.Genres = new string[] { };


                var tagsv1 = tagFile.GetTag(TagTypes.Id3v1);
                tagsv1.Album = titre.album;
                tagsv1.Artists = new string[] { titre.author };
                tagsv1.AlbumArtists = new string[] { titre.author };
                tagsv1.Track = (uint)titre.index;
                tagsv1.Title = titre.titleraw;
                tagsv1.Genres = new string[] { };

                tagFile.Save();
                //Id3Tag tag = new Id3Tag();// mp3.GetTag(Id3TagFamily.Version2X);

                //using (var mp3 = new Mp3File(titre.fullFileName, Mp3Permissions.ReadWrite))
                //{
                //    mp3.WriteTag(tag, WriteConflictAction.Replace);
                //}
            }

            //open folder
            OpenFolder(folder.Text);
        }

        static void TracksFinder(List<Silence> silences, ref List<Title> titles)
        {
            titles = new List<Title>();
            for (int i = 0; i < silences.Count - 1; i++)
            {
                Silence silence = silences[i];

                //first title
                if (i == 0)
                {
                    titles.Add(new Title(TimeSpan.Zero, TimeSpan.FromSeconds(silence.debut))
                    {
                        index = titles.Count + 1,
                        brush = new SolidColorBrush(GetNextColor(titles.Count + 1))
                    });
                }
                titles.Add(new Title(TimeSpan.FromSeconds((double)silence.fin),
                                     TimeSpan.FromSeconds(silences[i + 1].debut))
                {
                    index = titles.Count + 1,
                    brush = new SolidColorBrush(GetNextColor(titles.Count + 1)),
                });
            }
        }

        void TitlesMaker(string text, List<Title> titles, string author, string album)
        {
            text = text.Replace("\r\n", "\n");
            string[] lignes = text.Split("\n");

            //texte → titre
            for (int i = 0; i < lignes.Length; i++)
            {
                string ligne = lignes[i];

                Title title = titles[i];
                title.album = album;
                title.author = author;
                title.SetTitle(ligne, folder.Text);

                //string t = "";
                //if (title.author != null && title.author != "")
                //    t += title.author + " - ";

                //if (title.album != null && title.album != "")
                //    if (album.Text != null && album.Text != "")
                //        title.album = album.Text;

                //if (title.album != null && title.album != "")
                //    t += title.album + " - ";

                //t += title.index.ToString("00") + " - ";

                //t += title.titleraw;

                //t = t.Replace("\\", "_");
                //t = t.Replace("/", "_");
                //t = t.Replace(":", "_");
                //t = t.Replace("*", "_");
                //t = t.Replace("?", "_");
                //t = t.Replace("\"", "_");
                //t = t.Replace("<", "_");
                //t = t.Replace(">", "_");
                //t = t.Replace("|", "_");

                //title.fileName = t + ".mp3";

                //title.fullFileName = folder.Text + "\\" + title.fileName;
            }
        }

        #region To UI
        void List_Titles()
        {
            for (int i = 0; i < data.titles.Count; i++)
            {
                Title title = data.titles[i];
                Title_UC uc = new Title_UC();
                uc._Link(title);
            }

            lbox.Items.Clear();
            for (int i = 0; i < data.titles.Count; i++)
                lbox.Items.Add(data.titles[i].uc);
        }

        void List_Silences()
        {
            lbox_silence.Items.Clear();
            listitems_silence = new Dictionary<ListBoxItem, Silence>();

            ListBoxItem it = null;
            for (int i = 0; i < data.silences.Count; i++)
            {
                data.silences[i].index = i + 1;
                it = new ListBoxItem();
                it.Content = data.silences[i];
                it.MouseEnter += new System.Windows.Input.MouseEventHandler(SilenceOver);
                lbox_silence.Items.Add(it);
                listitems_silence.Add(it, data.silences[i]);
            }
        }

        private void SilenceOver(object? sender, System.Windows.Input.MouseEventArgs e)
        {
            previousPastilleSelected?._FocusLost();
            ListBoxItem it = (ListBoxItem)sender;

            if (listitems_silence.ContainsKey(it))
                if (silences_pastille.ContainsKey(listitems_silence[it]))
                {
                    previousPastilleSelected = silences_pastille[listitems_silence[it]];
                    previousPastilleSelected._Focus();
                }
        }

        void Draw_Titles()
        {
            if (data.titles == null)
                return;

            foreach (Title titre in data.titles)
            {
                // Create the rectangle
                System.Windows.Shapes.Rectangle rec = new System.Windows.Shapes.Rectangle()
                {
                    Width = rectangles.Width,
                    Height = titre.end.TotalSeconds - titre.start.TotalSeconds,
                    Fill = titre.brush,
                    Stroke = System.Windows.Media.Brushes.Black,
                    StrokeThickness = 0,
                };
                titre.rectangle = rec;

                //Add to canvas
                Dispatcher.Invoke(new Action(() =>
                {

                    rectangles.Children.Add(rec);
                    System.Windows.Controls.Panel.SetZIndex(rec, (int)ZLevelOnCanvas.tracks);
                    Canvas.SetTop(rec, titre.start.TotalSeconds);
                    Canvas.SetLeft(rec, 0);
                }));
            }
        }

        void Draw_Peaks(System.Windows.Media.Color color)
        {
            //dessine le niveau sonore = f(temps)
            sound_peaks = new Polygon();
            sound_peaks.Fill = new SolidColorBrush(color);
            sound_peaks.Points.Add(new System.Windows.Point(0, 0));

            for (int i = 0; i < data.peaks.Count; i++)
                sound_peaks.Points.Add(new System.Windows.Point(data.peaks[i].amplitude, data.peaks[i].temps));

            sound_peaks.Points.Add(new System.Windows.Point(0, data.totaltime));
            sound_peaks.Points.Add(new System.Windows.Point(0, 0));

            //positionnement du dessin
            rectangles.Children.Add(sound_peaks);
            System.Windows.Controls.Panel.SetZIndex(sound_peaks, (int)ZLevelOnCanvas.peaks);
            Canvas.SetTop(sound_peaks, 0);
            Canvas.SetLeft(sound_peaks, 0);

            rectangles.Height = data.totaltime;
        }

        void Draw_Silences(System.Windows.Media.Color color)
        {
            //dessine des traits à chaque silence = f(temps)
            sound_silences = new Polygon();
            sound_silences.Fill = new SolidColorBrush(color);
            sound_silences.Points.Add(new System.Windows.Point(1, 0));

            double X, Y;
            foreach (Silence silence in data.silences)
            {
                X = 1;
                Y = silence.debut;
                sound_silences.Points.Add(new System.Windows.Point(X, Y));
                X = 0.1;
                sound_silences.Points.Add(new System.Windows.Point(X, Y));
                Y = (double)silence.fin;
                sound_silences.Points.Add(new System.Windows.Point(X, Y));
                X = 1;
                sound_silences.Points.Add(new System.Windows.Point(X, Y));
            }
            sound_silences.Points.Add(new System.Windows.Point(1, 0));

            //positionnement du dessin
            rectangles.Children.Add(sound_silences);
            System.Windows.Controls.Panel.SetZIndex(sound_silences, (int)ZLevelOnCanvas.silences);
            Canvas.SetTop(sound_silences, 0);
            Canvas.SetLeft(sound_silences, 0);

            rectangles.Height = data.totaltime;
        }

        void DrawOrUpdate_SilencesPastilles()
        {
            if (data == null) { return; }

            TranslateTransform st = zoomBorder._GetTranslateTransform();
            ScaleTransform sc = zoomBorder._GetScaleTransform();

            if (st == null || sc == null)
                return;

            if (silences_pastille == null)
            {
                silences_pastille = new Dictionary<Silence, Pastille>();
                foreach (Silence silence in data.silences)
                {
                    Pastille pastille = new Pastille();
                    pastille.Set(silence.index.ToString("00"),
                        stroke_color: Brushes.Black,
                        fill_color: Brushes.White,
                        stroke_thickness: 1,
                        silence,
                        (int)ZLevelOnCanvas.pastilles - silence.index
                        );
                    rectangles.Children.Add(pastille);
                    silences_pastille.Add(silence, pastille);
                }
            }

            double rectangles_W_abs = rectangles.ActualWidth / sc.ScaleX;
            //double rectangles_H_abs = rectangles.ActualHeight / sc.ScaleY;
            double rectangles_H_abs = data.totaltime / sc.ScaleY;
            double fixedwidth_prct = 0.03 * zoomBorder.ActualHeight / zoomBorder.ActualWidth;
            double fixedheight_prct = fixedwidth_prct * zoomBorder.ActualWidth / zoomBorder.ActualHeight;

            //mis à jour du positionnement des pastilles
            foreach (var item in silences_pastille)
            {
                Silence silence = item.Key;
                Pastille pastille = item.Value;

                pastille.Width = rectangles_W_abs * fixedwidth_prct;
                pastille.Height = rectangles_H_abs * fixedheight_prct;

                double top = silence.milieu - pastille.Height / 2;
                double left = rectangles.Width - pastille.Width;

                //met les nouvelles pastilles derrières les anciennes;
                System.Windows.Controls.Panel.SetZIndex(pastille, pastille._zindex);
                Canvas.SetTop(pastille, top);
                Canvas.SetLeft(pastille, left);
            }
        }

        void Title_selected(Title title)
        {
            double relativeStart = title.start.TotalSeconds / data.totaltime;
            double relativeEnd = title.end.TotalSeconds / data.totaltime;
            zoomBorder.SetRange(relativeStart, relativeEnd);
        }

        void Silence_selected(Silence? silence)
        {
            if (silence_selected != null)
                rectangles.Children.Remove(silence_selected);
            if (silence == null)
                silence_selected = null;
            else
            {
                silence_selected = new Polygon();
                silence_selected.Fill = new SolidColorBrush(System.Windows.Media.Colors.Red);
                silence_selected.Points.Add(new System.Windows.Point(0.1, silence.debut));
                silence_selected.Points.Add(new System.Windows.Point(0.9, silence.debut));
                silence_selected.Points.Add(new System.Windows.Point(0.9, (double)silence.fin));
                silence_selected.Points.Add(new System.Windows.Point(0.1, (double)silence.fin));
                rectangles.Children.Add(silence_selected);
                Canvas.SetTop(silence_selected, 0);
                Canvas.SetLeft(silence_selected, 0);

                //zoom in
                double y_moyen = ((double)silence.fin + silence.debut) / 2;
                double y_relative = y_moyen / data.totaltime;
                zoomBorder.SetZoom(y_relative, aboluteZoom: 500);
            }
        }

        #endregion

        #region TOOLS
        static List<System.Windows.Media.Color> colors = new List<System.Windows.Media.Color>(){
            System.Windows.Media.Colors.CadetBlue,
            System.Windows.Media.Colors.DarkKhaki,
            System.Windows.Media.Colors.DarkTurquoise,
            System.Windows.Media.Colors.LightBlue,
            System.Windows.Media.Colors.LightCoral,
            System.Windows.Media.Colors.LightGreen,
            System.Windows.Media.Colors.LightPink,
            System.Windows.Media.Colors.LightSalmon,
            System.Windows.Media.Colors.LightSkyBlue,
            System.Windows.Media.Colors.LimeGreen,
            System.Windows.Media.Colors.MediumOrchid,
            System.Windows.Media.Colors.Plum,
            System.Windows.Media.Colors.SandyBrown,
            System.Windows.Media.Colors.Thistle
            };

        static System.Windows.Media.Color GetNextColor(int index)
        {
            System.Windows.Media.Color c;
            while (index > colors.Count - 1) { index -= colors.Count; }
            return colors[index];
        }

        void OpenFolder(string folderPath)
        {
            if (Directory.Exists(folderPath))
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    Arguments = folderPath,
                    FileName = "explorer.exe"
                };

                System.Diagnostics.Process.Start(startInfo);
            }
        }

        #endregion
    }
}
