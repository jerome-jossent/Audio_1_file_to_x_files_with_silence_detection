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
using System.Diagnostics;
using TagLib;
using System.Windows.Threading;

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

        Dictionary<Silence, Pastille> silences_pastille_H, silences_pastille_V;
        Dictionary<ListBoxItem, Silence> listitems_silence;
        Pastille previousPastilleSelected;
        Polygon sound_peaks_H, sound_peaks_V;
        Polygon sound_silences_H, sound_silences_V;
        Polygon silence_selected;
        Polyline play_cursor_playing_H, play_cursor_playing_V;
        System.Windows.Media.Color play_cursor_playing_color = System.Windows.Media.Colors.White;

        double current_time, current_playing_time;

        public enum ZLevelOnCanvas { tracks = 0, peaks = 3, silences = 5, pastilles = 1000, cursor = 2000 }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        void Window_Loaded(object sender, RoutedEventArgs e)
        {
            INITS();
            //ZoomBorder.MoveEvent += ZoomBorder_MoveEvent;
            //ZoomBorder.ZoomChangeEvent += ZoomBorder_ZoomChangeEvent;
            //ZoomBorder.MouseLeftButtonWithoutMoveEvent += ZoomBorder_MouseLeftButtonWithoutMoveEvent;
            zoomBorder_H.MoveEvent += ZoomBorder_MoveEvent;
            zoomBorder_H.ZoomChangeEvent += ZoomBorder_ZoomChangeEvent;
            zoomBorder_H.MouseLeftButtonWithoutMoveEvent += ZoomBorder_MouseLeftButtonWithoutMoveEvent;

            zoomBorder_V.MoveEvent += ZoomBorder_V_MoveEvent;
            zoomBorder_V.ZoomChangeEvent += ZoomBorder_V_ZoomChangeEvent;
            zoomBorder_V.MouseLeftButtonWithoutMoveEvent += ZoomBorder_V_MouseLeftButtonWithoutMoveEvent;
        }

        void INITS()
        {
            _Title = Title;



            //file.Text = @"D:\Videos\Download Videos\MOBY - Amiga Days (Remasters Vol.1) [[FULL ALBUM]].mp3";
            //file.Text = @"D:\Ma musique\Musique\_CHIPTUNE\Best of Chiptune [8 bit music, retro visuals].mp3";
            //folder.Text = @"D:\Videos\Download Videos\TEST";
            txt.Text = "Progressive Funk (Impact Inc. - Vectorball)\r\nPapoornoo2 (Apology - Demodisk 1)\r\nThe Last Knight (Alcatraz - Megademo IV)\r\nDragonsfunk (Angels - Copper Master)\r\nPelforth Blues (Alcatraz - Music Disk 1)\r\nThe Knight is Back (Alcatraz - Music Disk 1)\r\nKnulla Kuk (Quartex - Substance)\r\nLet there be Funk (Dreamdealers - Tales Of A Dream \r\nGroovy Thing (Dreamdealers - Innervision)\r\n88, Funky Avenue\r\nP.A.T.A.O.P.A.\r\nDrink My Pain Away (The Special Brothers - Live #1)\r\nKanyenamaryamalabar\r\nCortouchka !\r\nHeads Up (Alliance Design/DRD - Arkham Asylum)\r\nRaging Fire (Dreamdealers - Raging Fire)\r\nLivin' Insanity (Sanity - Arte)\r\nElekfunk (Sanity - Arte)\r\nMobyle (Sanity - Arte)\r\nMore Than Music (Alcatraz - More Than Music)";
            //author.Text = "MOBY";
        }

        #region From UI
        void SelectFile_btn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                file.Text = openFileDialog.FileName;
                GetInfo(false);
            }
        }


        private void _silence_detection_sensitivity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            GetInfo_2();

        }

        private void _silence_detection_mintime_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            GetInfo_2();

        }

        void SilenceDetectionCompute_Click(object sender, RoutedEventArgs e)
        {
            GetInfo_2();
        }

        void CreateFolder_btn_Click(object sender, RoutedEventArgs e)
        {
            if (file.Text == "") return;
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

            if (e.AddedItems[0] is Silence)
            {
                Silence_selected((Silence)e.AddedItems[0]);
            }
            //if (e.AddedItems[0] is Pastille)
            //{
            //    Silence_selected((Silence)e.AddedItems[0]);
            //}


        }

        void lbox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return;

            Title_UC selection = (Title_UC)e.AddedItems[0];
            Title_selected(selection.title);
        }

        void ZoomBorder_MoveEvent(object sender, PanAndZoom.ZoomBorderEventArgs e)
        {
            if (data == null) return;
            double x_relative = (e.mouseRelativeX - e.relativeoffsetX) / e.scaleX;
            current_time = x_relative * data.totaltime;
            TimeSpan t = TimeSpan.FromSeconds(current_time);
            string titre = _Title + " - " + t.ToString("G");
            Dispatcher.BeginInvoke(() => (Title = titre));
        }

        void ZoomBorder_ZoomChangeEvent(object sender, ZoomBorderEventArgs args)
        {
            DrawOrUpdate_SilencesPastilles_H();
            UpdateCursorThickness();
        }

        void ZoomBorder_MouseLeftButtonWithoutMoveEvent(object sender, ZoomBorderEventArgs e)
        {
            if(e==null) return;
            double x_relative = (e.mouseRelativeX - e.relativeoffsetX) / e.scaleX;
            current_playing_time = x_relative * data.totaltime;
            if (NAudio_JJ.NAudio_JJ.isPlaying)
                PlayAudioHere();
            else
                DrawOrUpdate_PlayCursor_H(current_playing_time);
        }




        void GridZoom_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawOrUpdate_SilencesPastilles_H();
            UpdateCursorThickness();
        }





        void ZoomBorder_V_MoveEvent(object sender, PanAndZoom.ZoomBorderEventArgs e)
        {
            if (data == null) return;
            double y_relative = (e.mouseRelativeY - e.relativeoffsetY) / e.scaleY;
            current_time = y_relative * data.totaltime;
            TimeSpan t = TimeSpan.FromSeconds(current_time);
            string titre = _Title + " - " + t.ToString("G");
            Dispatcher.BeginInvoke(() => (Title = titre));
        }

        void ZoomBorder_V_ZoomChangeEvent(object sender, ZoomBorderEventArgs args)
        {
            DrawOrUpdate_SilencesPastilles_V();
            UpdateCursorThickness_V();
        }

        void ZoomBorder_V_MouseLeftButtonWithoutMoveEvent(object sender, ZoomBorderEventArgs e)
        {
            if (e == null) return;
            double y_relative = (e.mouseRelativeY - e.relativeoffsetY) / e.scaleY;
            current_playing_time = y_relative * data.totaltime;
            if (NAudio_JJ.NAudio_JJ.isPlaying)
                PlayAudioHere();
            else
                DrawOrUpdate_PlayCursor_V(current_playing_time);
        }
        private void GridZoom_V_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawOrUpdate_SilencesPastilles_V();
            UpdateCursorThickness_V();
        }

        private void UpdateCursorThickness()
        {
            if (sc_H != null && play_cursor_playing_H != null)
                play_cursor_playing_H.StrokeThickness = rectangles_H.ActualWidth * 0.2 / (100 * sc_H.ScaleX);
        }


        private void UpdateCursorThickness_V()
        {
            if (sc_V != null && play_cursor_playing_V != null)
                play_cursor_playing_V.StrokeThickness = rectangles_V.ActualHeight * 0.2 / (100 * sc_V.ScaleY);
        }


        void Silence_selected_delete(object sender, RoutedEventArgs e)
        {
            data.silences.Remove((Silence)lbox_silence.SelectedItem);
            List_Silences();
            ReDrawGraph();
        }


        void AudioPlayer_PLAY_Click(object sender, RoutedEventArgs e)
        {
            PlayAudioHere();
        }

        void AudioPlayer_PAUSE_Click(object sender, RoutedEventArgs e)
        {
            NAudio_JJ.NAudio_JJ.AudioPlayer_Pause();
        }

        void AudioPlayer_STOP_Click(object sender, RoutedEventArgs e)
        {
            NAudio_JJ.NAudio_JJ.playerPlayingEvent -= NAudio_JJ_playerPlayingEvent; ;
            NAudio_JJ.NAudio_JJ.AudioPlayer_Stop();
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
            }
            else
            {
                Peak.peakAnalysingEvent += Peak_peakAnalysingEvent;
                data.peaks = Peak.Get_Peaks(path);
                Peak.peakAnalysingEvent -= Peak_peakAnalysingEvent;
                string jsonString = JsonSerializer.Serialize(data.peaks);
                System.IO.File.WriteAllText(jsonfile, jsonString);
            }
            GetInfo_2();
        }

        void Peak_peakAnalysingEvent(object sender, Peak.PeakAnalysingEventArgs e)
        {
            Dispatcher.Invoke(new Action(() => { _progressbar.Value = e.Val; }), DispatcherPriority.Background, null);
        }

        void GetInfo_2()
        {
            if (data == null) return;
            GetInfo_2(data.peaks, ref data.silences, ref data.titles, data.totaltime);
        }

        void GetInfo_2(List<Peak> peaks, ref List<Silence> silences, ref List<Title> titles, double totaltime_sec)
        {
            //get silences
            silences = Silence.Get_Silences(peaks,
                                            (double)_silence_detection_sensitivity.Value,
                                            (double)_silence_detection_mintime.Value);

            TracksFinder(silences, ref titles);

            //TRIM data
            //silences 1 et n
            if (silences.Count>0 && silences[0].debut == TimeSpan.Zero.TotalSeconds)
            {
                //change piste 1
                titles[0].start = TimeSpan.FromSeconds((double)silences[0].fin);
                //delete silence 1
                silences.RemoveAt(0);
            }
            if (silences.Count > 0 && titles.Count > 1 && (double)silences[silences.Count - 1].fin >= totaltime_sec)
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
            NAudio_JJ.NAudio_JJ.playerPlayingEvent += NAudio_JJ_playerPlayingEvent; ;
            NAudio_JJ.NAudio_JJ.AudioPlayer_Play(file.Text, current_playing_time, data.totaltime);
        }

        private void NAudio_JJ_playerPlayingEvent(object sender, NAudio_JJ.NAudio_JJ.PlayerPlayingEventArgs e)
        {
            current_playing_time = e.Val;
            try
            {
                Dispatcher.Invoke(() =>
                {
                    DrawOrUpdate_PlayCursor_H(e.Val);
                    DrawOrUpdate_PlayCursor_V(e.Val);
                });
            }
            catch (Exception ex)
            {
            }
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
            for (int i = 0; i < rectangles_H.Children.Count; i++)
            {
                var item = rectangles_H.Children[i];
                if (item is Pastille)
                    continue;

                if (item == play_cursor_playing_H)
                    continue;

                rectangles_H.Children.RemoveAt(i);
                i--;
            }
            Draw_Titles_H();
            Draw_Peaks_H(System.Windows.Media.Color.FromRgb(40, 40, 40));
            Draw_Silences_H(Colors.White);
            DrawOrUpdate_SilencesPastilles_H();




            for (int i = 0; i < rectangles_V.Children.Count; i++)
            {
                var item = rectangles_V.Children[i];
                if (item is Pastille)
                    continue;

                if (item == play_cursor_playing_H)
                    continue;

                rectangles_V.Children.RemoveAt(i);
                i--;
            }
            Draw_Titles_V();
            Draw_Peaks_V(System.Windows.Media.Color.FromRgb(40, 40, 40));
            Draw_Silences_V(Colors.White);
            DrawOrUpdate_SilencesPastilles_V();
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
                if (silences_pastille_H.ContainsKey(listitems_silence[it]))
                {
                    previousPastilleSelected = silences_pastille_H[listitems_silence[it]];
                    previousPastilleSelected._Focus();
                }
        }





        void Draw_Titles_H()
        {
            if (data.titles == null)
                return;

            foreach (Title titre in data.titles)
            {
                // Create the rectangle
                System.Windows.Shapes.Rectangle rec = new System.Windows.Shapes.Rectangle()
                {
                    Width = titre.end.TotalSeconds - titre.start.TotalSeconds,// rectangles.Width,
                    //Width = 1,// rectangles.Width,
                    //Height = titre.end.TotalSeconds - titre.start.TotalSeconds,
                    Height = 1,
                    Fill = titre.brush,
                    Stroke = System.Windows.Media.Brushes.Black,
                    StrokeThickness = 0,
                };
                titre.rectangle = rec;

                //Add to canvas
                Dispatcher.Invoke(new Action(() =>
                {
                    rectangles_H.Children.Add(rec);
                    System.Windows.Controls.Panel.SetZIndex(rec, (int)ZLevelOnCanvas.tracks);
                    Canvas.SetTop(rec, 0);
                    Canvas.SetLeft(rec, titre.start.TotalSeconds);
                }));
            }
        }
        void Draw_Peaks_H(System.Windows.Media.Color color)
        {
            //dessine le niveau sonore = f(temps)
            sound_peaks_H = new Polygon();
            sound_peaks_H.Fill = new SolidColorBrush(color);
            sound_peaks_H.Points.Add(new System.Windows.Point(0, 1));

            for (int i = 0; i < data.peaks.Count; i++)
                sound_peaks_H.Points.Add(new System.Windows.Point(data.peaks[i].temps, 1 - data.peaks[i].amplitude));

            sound_peaks_H.Points.Add(new System.Windows.Point(data.totaltime, 1));
            //sound_peaks.Points.Add(new System.Windows.Point(0, 1));

            //positionnement du dessin
            rectangles_H.Children.Add(sound_peaks_H);
            System.Windows.Controls.Panel.SetZIndex(sound_peaks_H, (int)ZLevelOnCanvas.peaks);
            Canvas.SetTop(sound_peaks_H, 0);
            Canvas.SetLeft(sound_peaks_H, 0);

            rectangles_H.Width = data.totaltime;
            //rectangles.Height = data.totaltime;
        }
        void Draw_Silences_H(System.Windows.Media.Color color)
        {
            //dessine des traits à chaque silence = f(temps)
            sound_silences_H = new Polygon();
            sound_silences_H.Fill = new SolidColorBrush(color);
            //sound_silences.Points.Add(new System.Windows.Point(1, 0));
            sound_silences_H.Points.Add(new System.Windows.Point(0, 0));

            double X, Y;
            foreach (Silence silence in data.silences)
            {
                X = silence.debut;
                Y = 0;
                sound_silences_H.Points.Add(new System.Windows.Point(X, Y));
                Y = 0.9;
                sound_silences_H.Points.Add(new System.Windows.Point(X, Y));
                X = (double)silence.fin;
                sound_silences_H.Points.Add(new System.Windows.Point(X, Y));
                Y = 0;
                sound_silences_H.Points.Add(new System.Windows.Point(X, Y));
                //X = 1;
                //Y = silence.debut;
                //sound_silences.Points.Add(new System.Windows.Point(X, Y));
                //X = 0.1;
                //sound_silences.Points.Add(new System.Windows.Point(X, Y));
                //Y = (double)silence.fin;
                //sound_silences.Points.Add(new System.Windows.Point(X, Y));
                //X = 1;
                //sound_silences.Points.Add(new System.Windows.Point(X, Y));
            }
            //sound_silences.Points.Add(new System.Windows.Point(1, 0));
            sound_silences_H.Points.Add(new System.Windows.Point(0, 0));

            //positionnement du dessin
            rectangles_H.Children.Add(sound_silences_H);
            System.Windows.Controls.Panel.SetZIndex(sound_silences_H, (int)ZLevelOnCanvas.silences);
            Canvas.SetTop(sound_silences_H, 0);
            Canvas.SetLeft(sound_silences_H, 0);

            rectangles_H.Width = data.totaltime;
            //rectangles.Height = data.totaltime;
        }
        TranslateTransform st_H;
        ScaleTransform sc_H;
        void DrawOrUpdate_SilencesPastilles_H()
        {
            if (data == null) return;

            if (st_H == null) st_H = zoomBorder_H._GetTranslateTransform();
            if (st_H == null) return;

            if (sc_H == null) sc_H = zoomBorder_H._GetScaleTransform();
            if (sc_H == null) return;

            if (silences_pastille_H == null)
            {
                silences_pastille_H = new Dictionary<Silence, Pastille>();
                foreach (Silence silence in data.silences)
                {
                    Pastille pastille = new Pastille();
                    pastille.Set(silence.index.ToString("00"),
                        stroke_color: System.Windows.Media.Brushes.Black,
                        fill_color: System.Windows.Media.Brushes.White,
                        stroke_thickness: 1,
                        silence,
                        (int)ZLevelOnCanvas.pastilles - silence.index
                        );
                    rectangles_H.Children.Add(pastille);
                    silences_pastille_H.Add(silence, pastille);
                }
            }

            double rectangles_W_abs = data.totaltime / sc_H.ScaleX;
            double rectangles_H_abs = rectangles_H.ActualHeight / sc_H.ScaleY;
            double fixedheight_prct = 0.03 * zoomBorder_H.ActualWidth / zoomBorder_H.ActualHeight;
            double fixedwidth_prct = fixedheight_prct * zoomBorder_H.ActualHeight / zoomBorder_H.ActualWidth;

            //mis à jour du positionnement des pastilles
            foreach (var item in silences_pastille_H)
            {
                Silence silence = item.Key;
                Pastille pastille = item.Value;

                pastille.Width = rectangles_W_abs * fixedwidth_prct;
                pastille.Height = rectangles_H_abs * fixedheight_prct;
                double top = 0; // en haut
                double left = silence.milieu - pastille.Width / 2;

                //met les nouvelles pastilles derrières les anciennes;
                System.Windows.Controls.Panel.SetZIndex(pastille, pastille._zindex);
                Canvas.SetTop(pastille, top);
                Canvas.SetLeft(pastille, left);
            }
        }

        void DrawOrUpdate_PlayCursor_H(double val)
        {
            if (data == null) return;

            if (st_H == null) st_H = zoomBorder_H._GetTranslateTransform();
            if (st_H == null) return;

            if (sc_H == null) sc_H = zoomBorder_H._GetScaleTransform();
            if (sc_H == null) return;

            if (play_cursor_playing_H == null)
            {
                play_cursor_playing_H = new Polyline();
                play_cursor_playing_H.Points.Add(new System.Windows.Point(val, 1));
                play_cursor_playing_H.Points.Add(new System.Windows.Point(val, 0));
                play_cursor_playing_H.Stroke = new SolidColorBrush(play_cursor_playing_color);
                rectangles_H.Children.Add(play_cursor_playing_H);
                Canvas.SetTop(play_cursor_playing_H, 0);
                Canvas.SetLeft(play_cursor_playing_H, 0);
                System.Windows.Controls.Panel.SetZIndex(play_cursor_playing_H, (int)ZLevelOnCanvas.cursor);
            }
            else
            {
                play_cursor_playing_H.Points[0] = new System.Windows.Point(val, 1);
                play_cursor_playing_H.Points[1] = new System.Windows.Point(val, 0);
            }
            UpdateCursorThickness();
        }


        //---------------------------------
        void Draw_Titles_V()
        {
            if (data.titles == null)
                return;

            foreach (Title titre in data.titles)
            {
                // Create the rectangle
                System.Windows.Shapes.Rectangle rec = new System.Windows.Shapes.Rectangle()
                {
                    //Width = titre.end.TotalSeconds - titre.start.TotalSeconds,// rectangles.Width,
                    Width = 1,// rectangles.Width,
                    Height = titre.end.TotalSeconds - titre.start.TotalSeconds,
                    //Height = 1,
                    Fill = titre.brush,
                    Stroke = System.Windows.Media.Brushes.Black,
                    StrokeThickness = 0,
                };
                titre.rectangle = rec;

                //Add to canvas
                Dispatcher.Invoke(new Action(() =>
                {
                    rectangles_V.Children.Add(rec);
                    System.Windows.Controls.Panel.SetZIndex(rec, (int)ZLevelOnCanvas.tracks);
                    Canvas.SetTop(rec, titre.start.TotalSeconds);
                    Canvas.SetLeft(rec, 0);
                }));
            }
        }
        void Draw_Peaks_V(System.Windows.Media.Color color)
        {
            //dessine le niveau sonore = f(temps)
            sound_peaks_V = new Polygon();
            sound_peaks_V.Fill = new SolidColorBrush(color);
            sound_peaks_V.Points.Add(new System.Windows.Point(0, 0));

            for (int i = 0; i < data.peaks.Count; i++)
                sound_peaks_V.Points.Add(new System.Windows.Point(data.peaks[i].amplitude, data.peaks[i].temps));

            sound_peaks_V.Points.Add(new System.Windows.Point(0, data.totaltime));
            sound_peaks_V.Points.Add(new System.Windows.Point(0, 0));

            //positionnement du dessin
            rectangles_V.Children.Add(sound_peaks_V);
            System.Windows.Controls.Panel.SetZIndex(sound_peaks_V, (int)ZLevelOnCanvas.peaks);
            Canvas.SetTop(sound_peaks_V, 0);
            Canvas.SetLeft(sound_peaks_V, 0);

            rectangles_V.Height = data.totaltime;
        }
        void Draw_Silences_V(System.Windows.Media.Color color)
        {
            //dessine des traits à chaque silence = f(temps)
            sound_silences_V = new Polygon();
            sound_silences_V.Fill = new SolidColorBrush(color);
            sound_silences_V.Points.Add(new System.Windows.Point(1, 0));

            double X, Y;
            foreach (Silence silence in data.silences)
            {
                X = 1;
                Y = silence.debut;
                sound_silences_V.Points.Add(new System.Windows.Point(X, Y));
                X = 0.1;
                sound_silences_V.Points.Add(new System.Windows.Point(X, Y));
                Y = (double)silence.fin;
                sound_silences_V.Points.Add(new System.Windows.Point(X, Y));
                X = 1;
                sound_silences_V.Points.Add(new System.Windows.Point(X, Y));
            }
            sound_silences_V.Points.Add(new System.Windows.Point(1, 0));

            //positionnement du dessin
            rectangles_V.Children.Add(sound_silences_V);
            System.Windows.Controls.Panel.SetZIndex(sound_silences_V, (int)ZLevelOnCanvas.silences);
            Canvas.SetTop(sound_silences_V, 0);
            Canvas.SetLeft(sound_silences_V, 0);

            rectangles_V.Height = data.totaltime;
        }

        TranslateTransform st_V;
        ScaleTransform sc_V;
        void DrawOrUpdate_SilencesPastilles_V()
        {
            if (data == null) return;

            if (st_V == null) st_V = zoomBorder_V._GetTranslateTransform();
            if (st_V == null) return;

            if (sc_V == null) sc_V = zoomBorder_V._GetScaleTransform();
            if (sc_V == null) return;

            if (silences_pastille_V == null)
            {
                silences_pastille_V = new Dictionary<Silence, Pastille>();
                foreach (Silence silence in data.silences)
                {
                    Pastille pastille = new Pastille();
                    pastille.Set(silence.index.ToString("00"),
                        stroke_color: System.Windows.Media.Brushes.Black,
                        fill_color: System.Windows.Media.Brushes.White,
                        stroke_thickness: 1,
                        silence,
                        (int)ZLevelOnCanvas.pastilles - silence.index
                        );
                    rectangles_V.Children.Add(pastille);
                    silences_pastille_V.Add(silence, pastille);
                }
            }

            double rectangles_W_abs = rectangles_V.ActualWidth / sc_V.ScaleX;
            double rectangles_H_abs = data.totaltime / sc_V.ScaleY;
            double fixedwidth_prct = 0.03 * zoomBorder_V.ActualHeight / zoomBorder_V.ActualWidth;
            double fixedheight_prct = fixedwidth_prct * zoomBorder_V.ActualWidth / zoomBorder_V.ActualHeight;

            //mis à jour du positionnement des pastilles
            foreach (var item in silences_pastille_V)
            {
                Silence silence = item.Key;
                Pastille pastille = item.Value;

                pastille.Width = rectangles_W_abs * fixedwidth_prct;
                pastille.Height = rectangles_H_abs * fixedheight_prct;

                double top = silence.milieu - pastille.Height / 2;
                double left = rectangles_V.Width - pastille.Width;//à droite

                //met les nouvelles pastilles derrières les anciennes;
                System.Windows.Controls.Panel.SetZIndex(pastille, pastille._zindex);
                Canvas.SetTop(pastille, top);
                Canvas.SetLeft(pastille, left);
            }
        }

        void DrawOrUpdate_PlayCursor_V(double val)
        {
            if (data == null) return;

            if (st_V == null) st_V = zoomBorder_V._GetTranslateTransform();
            if (st_V == null) return;

            if (sc_V == null) sc_V = zoomBorder_V._GetScaleTransform();
            if (sc_V == null) return;

            if (play_cursor_playing_V == null)
            {
                play_cursor_playing_V = new Polyline();
                play_cursor_playing_V.Points.Add(new System.Windows.Point(1, val));
                play_cursor_playing_V.Points.Add(new System.Windows.Point(0, val));
                play_cursor_playing_V.Stroke = new SolidColorBrush(play_cursor_playing_color);
                rectangles_V.Children.Add(play_cursor_playing_V);
                Canvas.SetTop(play_cursor_playing_V, 0);
                Canvas.SetLeft(play_cursor_playing_V, 0);
                System.Windows.Controls.Panel.SetZIndex(play_cursor_playing_V, (int)ZLevelOnCanvas.cursor);
            }
            else
            {
                play_cursor_playing_V.Points[0] = new System.Windows.Point(1, val);
                play_cursor_playing_V.Points[1] = new System.Windows.Point(0, val);
            }
            UpdateCursorThickness_V();
        }











        void Title_selected(Title title)
        {
            double relativeStart = title.start.TotalSeconds / data.totaltime;
            double relativeEnd = title.end.TotalSeconds / data.totaltime;
            zoomBorder_H.SetRangeX(relativeStart, relativeEnd);
            zoomBorder_V.SetRangeY(relativeStart, relativeEnd);
        }

        void Silence_selected(Silence? silence)
        {
            if (silence_selected != null)
                rectangles_H.Children.Remove(silence_selected);
            if (silence == null)
                silence_selected = null;
            else
            {
                silence_selected = new Polygon();
                silence_selected.Fill = new SolidColorBrush(System.Windows.Media.Colors.Red);
                silence_selected.Points.Add(new System.Windows.Point(silence.debut, 0.9));
                silence_selected.Points.Add(new System.Windows.Point(silence.debut, 0.1));
                silence_selected.Points.Add(new System.Windows.Point((double)silence.fin, 0.1));
                silence_selected.Points.Add(new System.Windows.Point((double)silence.fin, 0.9));
                rectangles_H.Children.Add(silence_selected);
                Canvas.SetTop(silence_selected, 0);
                Canvas.SetLeft(silence_selected, 0);

                //zoom in
                double y_moyen = ((double)silence.fin + silence.debut) / 2;
                double y_relative = y_moyen / data.totaltime;
                zoomBorder_H.SetZoomX(y_relative, aboluteZoom: 500);
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
