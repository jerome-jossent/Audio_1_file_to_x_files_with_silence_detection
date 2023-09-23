using NAudio.Utils;
using NAudio.Wave;
using NAudio.WaveFormRenderer;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Timers;

namespace NAudio_JJ
{
    public class Peak
    {
        public double temps { get; set; }
        public double amplitude { get; set; }

        public Peak(double temps, double amplitude)
        {
            this.temps = temps;
            this.amplitude = amplitude;
        }

        public static List<Peak> Get_Peaks_FromJson(string path)
        {
            string json = System.IO.File.ReadAllText(path);
            List<Peak> peaks = JsonSerializer.Deserialize<List<Peak>>(json);
            return peaks;
        }


        public delegate void PeakAnalysingHandler(object sender, PeakAnalysingEventArgs e);
        public static event PeakAnalysingHandler peakAnalysingEvent;

        public class PeakAnalysingEventArgs
        {
            public PeakAnalysingEventArgs(int val) { Val = val; }
            public int Val { get; }
        }

        public static List<Peak> Get_Peaks(string path, int samplessize = 1000)
        {
            List<Peak> peaks = new List<Peak>();

            peaks = new List<Peak>();

            MediaFoundationReader inputStream = new MediaFoundationReader(path);
            WaveStream waveReader = WaveFormatConversionStream.CreatePcmStream(inputStream);
            MaxPeakProvider peakProvider = new MaxPeakProvider();

            int bytesPerSample = (waveReader.WaveFormat.BitsPerSample / 8);
            long samples = waveReader.Length / bytesPerSample;
            peakProvider.Init(waveReader.ToSampleProvider(), samplessize);

            int iterrations = (int)(samples / samplessize);
            double pas = waveReader.TotalTime.TotalSeconds / iterrations;
            double tps = 0;// pas / 2;
            double amplitude;
            int p_last = 0;
            int p;
            for (int i = 0; i < iterrations; i++)
            {
                PeakInfo currentPeak = peakProvider.GetNextPeak();
                amplitude = Math.Abs(currentPeak.Max); // 0 à 1
                peaks.Add(new Peak(tps, amplitude));
                tps += pas;
                p = (100 * i / iterrations);
                if (p > p_last)
                {
                    peakAnalysingEvent?.Invoke(null, new PeakAnalysingEventArgs(p));
                    p_last = p;
                }
            }
            peakAnalysingEvent?.Invoke(null, new PeakAnalysingEventArgs(100));
            return peaks;
        }

        #region ARCHIVE : FFT
        //public class Frequency
        //{
        //    public float Hz { get; set; }
        //    public float Amplitude { get; set; }
        //    public float Phase { get; set; }
        //}


        ////AnalyzeAudio(samples,audioWaveProvider.WaveFormat.SampleRate);
        //public static Frequency[] AnalyzeAudio(float[] samples, int sampleRate)
        //{
        //    // size must be a power of 2
        //    int size = 1;
        //    int m = 0;
        //    while (size < samples.Length)
        //    {
        //        size <<= 1;
        //        ++m;
        //    }

        //    // create complex numbers from floats (X is real, Y is imag)
        //    Complex[] fftResults = new Complex[size];
        //    for (int i = samples.Length - 1; i >= 0; --i)
        //        fftResults[i] = new Complex() { X = samples[i], Y = 0 };

        //    // fourier transform
        //    FastFourierTransform.FFT(true, m, fftResults);

        //    float frequencyStep = sampleRate / (float)size;

        //    Frequency[] frequencies = new Frequency[size / 2];
        //    for (int i = 0; i < frequencies.Length; ++i)
        //    {
        //        Complex v = fftResults[i];

        //        Frequency f = new Frequency();
        //        f.Hz = (i + 1) * frequencyStep;
        //        f.Amplitude = (float)Math.Sqrt(Math.Pow(v.X, 2) + Math.Pow(v.Y, 2));
        //        f.Phase = (float)Math.Atan(v.Y / v.X);

        //        frequencies[i] = f;
        //    }

        //    return frequencies;
        //}
        #endregion

    }

    public class Silence
    {
        public double debut;
        public double? fin
        {
            get { return _fin; }
            set
            {
                _fin = value;
                if (value != null)
                {
                    duree = (double)value - debut;
                    milieu = ((double)value + debut) / 2;
                }
            }
        }
        double? _fin;

        public double milieu;
        public double duree;
        internal int index;

        public Silence(double debut, double? fin)
        {
            this.debut = debut;
            this.fin = fin;
        }

        public override string ToString()
        {
            return $"[{index:00}] " + TimeSpan.FromSeconds(debut).ToString("hh\\:mm\\:ss") + " (" + duree.ToString("0.00") + "s)";
            //return $"[{index:00}] " + TimeSpan.FromSeconds(debut).ToString("G") + "   →   " + TimeSpan.FromSeconds((double)fin).ToString("G");
        }

        public static List<Silence> Get_Silences(List<Peak> peaks, double sensibility, double silence_dureemini_seconde = 0)
        {
            List<Silence> silences = new List<Silence>();

            #region détection de silences
            bool newSilence = true;
            for (int i = 0; i < peaks.Count; i++)
            {
                if (peaks[i].amplitude < sensibility)
                {
                    if (newSilence)
                    {
                        silences.Add(new Silence(peaks[i].temps, null));
                        newSilence = false;
                    }
                    else
                        silences[silences.Count - 1].fin = peaks[i].temps;
                }
                else
                {
                    newSilence = true;
                    if (silences.Count > 0 && silences[silences.Count - 1].fin == null)
                        silences.RemoveAt(silences.Count - 1);
                }
            }
            #endregion

            #region supprime les silences de moins de X secondes
            if (silence_dureemini_seconde > 0)
            {
                //(vu qu'on supprime dans une itération → pas de foreach)
                for (int i = 0; i < silences.Count; i++)
                    if (silences[i].duree < silence_dureemini_seconde)
                    {
                        silences.RemoveAt(i);
                        i--;
                    }
            }
            #endregion

            return silences;
        }

    }

    public static class NAudio_JJ
    {
        public static double MusicTotalSeconds(string path)
        {
            if (!System.IO.File.Exists(path)) return 0;
            TimeSpan time;
            //using (Mp3FileReader reader = new Mp3FileReader(path))
            using (MediaFoundationReader reader = new MediaFoundationReader(path))
                time = reader.TotalTime;
            return time.TotalSeconds;
        }

        public static void AudioExtractor(string path_in, string path_out, double start_secondes, double end_secondes)
        {
            //https://stackoverflow.com/questions/6094287/naudio-to-split-mp3-file
            using (Mp3FileReader reader = new Mp3FileReader(path_in))
            {
                //create folder if necessary
                System.IO.FileInfo fi = new System.IO.FileInfo(path_out);
                System.IO.Directory.CreateDirectory(fi.DirectoryName);

                //writing data
                System.IO.FileStream _fs = new System.IO.FileStream(path_out, System.IO.FileMode.Create, System.IO.FileAccess.Write);

                //read data
                Mp3Frame mp3Frame = reader.ReadNextFrame();
                while (mp3Frame != null)
                {
                    if (reader.CurrentTime.TotalSeconds > start_secondes)
                        _fs.Write(mp3Frame.RawData, 0, mp3Frame.RawData.Length);

                    mp3Frame = reader.ReadNextFrame();

                    if (reader.CurrentTime.TotalSeconds > end_secondes)
                        break;
                }
                _fs.Close();
            }
        }


        static WaveOut audioPlayer;
        //static System.Timers.Timer audioPlayerTimer;
        static Thread threadCursorPositionUpdate;
        static DateTime endtime;
        static double _start_secondes;
        internal static bool isPlaying;

        public static void AudioPlayer_Play(string path, double start_secondes, double end_secondes)
        {
            Mp3FileReader reader = new Mp3FileReader(path);
            AudioPlayer_Stop();

            audioPlayer = new WaveOut();

            //passe les premières secondes
            Mp3Frame mp3Frame = reader.ReadNextFrame();
            while (mp3Frame != null)
            {
                if (reader.CurrentTime.TotalSeconds >= start_secondes)
                    break;
                mp3Frame = reader.ReadNextFrame();
            }
            audioPlayer.Init(reader);

            double secToWait = end_secondes - start_secondes;

            endtime = DateTime.Now + TimeSpan.FromSeconds(secToWait);
            _start_secondes = start_secondes;
            //// audioPlayerTimer = new System.Timers.Timer(100);
            ////  audioPlayerTimer.Enabled = true;
            ////  audioPlayerTimer.Elapsed += new System.Timers.ElapsedEventHandler(OnDummyTimerFired);
            //// audioPlayerTimer.AutoReset = true;


//   stop threadCursorPositionUpdate

            threadCursorPositionUpdate = new Thread(eeee);
            threadCursorPositionUpdate.Start();

            isPlaying = true;
            audioPlayer.Play();
            ////audioPlayerTimer.Start();
        }

        public delegate void PlayerPlayingHandler(object sender, PlayerPlayingEventArgs e);
        public static event PlayerPlayingHandler playerPlayingEvent;

        public class PlayerPlayingEventArgs
        {
            public PlayerPlayingEventArgs(double val) { Val = val; }
            public double Val { get; }
        }

        static void eeee()
        {
            //            threadCursorPositionUpdate


            while (DateTime.Now < endtime)
            {
                double val = 0;
                try
                {
                    val = audioPlayer.GetPositionTimeSpan().TotalSeconds + _start_secondes;
                }
                catch (Exception ex)
                {
                    //throw;
                }

                playerPlayingEvent?.Invoke(null, new PlayerPlayingEventArgs(val));
                Thread.Sleep(100);
            }
            audioPlayer.Stop();

        }

        private static void OnDummyTimerFired(object? sender, ElapsedEventArgs e)
        {
            if (DateTime.Now >= endtime)
            {
                audioPlayer.Stop();
                //// audioPlayerTimer.Elapsed -= new System.Timers.ElapsedEventHandler(OnDummyTimerFired);
            }

            double val = 0;
            try
            {
                val = audioPlayer.GetPositionTimeSpan().TotalSeconds + _start_secondes;
            }
            catch (Exception ex)
            {
                //throw;
            }

            playerPlayingEvent?.Invoke(null, new PlayerPlayingEventArgs(val));
        }

        internal static void AudioPlayer_Stop()
        {
            if (audioPlayer != null)
            {
                audioPlayer.Stop();
                isPlaying = false;
            }
        }

        internal static void AudioPlayer_Pause()
        {
            if (audioPlayer != null)
            {
                audioPlayer.Pause();
                isPlaying = false;
            }
        }
    }
}