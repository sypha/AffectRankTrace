/***
/* This code was based on the RankTrace project by Phil Lopes
/* Source: https://github.com/WorshipCookies/RankTrace
***/


using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Timers;
using System.IO;
using Vlc.DotNet.Core;
using System.Windows.Forms.DataVisualization.Charting;
using System.Diagnostics;
using System.Resources;
using System.Reflection;

namespace AffectRankTraceProject
{
    public partial class MainForm : Form
    {
        // Contains the dictionary of emotional terms and the associated pleasure arousal dominance values
        // Sources: Russell, J. A., & Mehrabian, A. (1977). Evidence for a three-factor theory of emotions. Journal of research in Personality, 11(3), 273-294.
        private string EMOTION_TAGS_FILENAME= "emotionList.csv";

        // Annotation data location
        private string RLT_ANNOT_FILENAME = "\\realtime_data.csv";
        private string DISCRET_ANNOT_FILENAME = "\\discrete_data.csv";
        private string ANNOTATION_DIR_NAME = "Annotation_data";

        // keys to raise and grop the curve during realtime annotation (customizable) 
        private Keys CURVE_RAISE_KEY = Keys.Q;
        private Keys CURVE_DROP_KEY = Keys.W;

        private static double KEYBOARD_STEP = 1;
        private static double KEYBOARD_PRESS_CONTROL = 8;

        private static bool raiseKey_IsPressed = false;
        private static bool dropKey_IsPressed = false;

        private static bool chart_IsUpdating = false;
        private static bool annotatingDetails = false;

        private Thread plotUpdater;
        private Thread eventFrameManager;
        private System.Timers.Timer myTimer;

        private long annotationEventTimeSpan;
        private double currentAnnotationValue;
        private double previousAnnotationValue;

        private ParseEmotion emotionList;
        private List<string> tagList;

        private List<EventAnnotation> myEvents;

        private int annotationIndex;
        private long eventFrameEnd;
        private double currentVidTime;

        private string annotionDataPath;
        private string emotionListPath;

        private double mediaLength;
        private string fileInfo;

        public MainForm()
        {
            InitializeComponent();
            emotionListPath = getEmotionListPath();

            if (emotionListPath == null)
            {
                Environment.Exit(0);
            }

            emotionList = new ParseEmotion(emotionListPath);

            myEvents = new List<EventAnnotation>();

            annotionDataPath = CreateDirectory(ANNOTATION_DIR_NAME);

            currentAnnotationValue = 0;
            previousAnnotationValue = 0;
            currentVidTime = 0;

            vlcPlayer.MediaChanged += changeMedia;
            vlcPlayer.EndReached += mediaStopped;

            this.KeyPreview = true;
            this.KeyDown += new KeyEventHandler(Keyboard_KeyPress);
            this.KeyUp += new KeyEventHandler(Keyboard_KeyUp);
            this.FormClosing += new FormClosingEventHandler(exitForm);
        }
 

        private void Form1_Load(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Maximized;
            startChartThread();
        }

        private void loadVideoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            loadVideoFile();
        }

        private void loadVideoFile()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();

            // Set filter options and filter index.
            openFileDialog.Filter = "Video Files (.avi, .mp4)|*.avi;*.mp4";
            openFileDialog.FilterIndex = 1;

            openFileDialog.Multiselect = false;

            if (chart_IsUpdating || annotatingDetails)
            {
                MessageBox.Show("Please wait the end of annotation.",
                    "Error Loading new video",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            else
            {
                // Call the ShowDialog method to show the dialog box.
                DialogResult userClickedOK = openFileDialog.ShowDialog();

                // Process input if the user clicked OK.
                if (userClickedOK == DialogResult.OK)
                {
                    // Open the selected file to read.
                    fileInfo = openFileDialog.FileName;
                    vlcPlayer.SetMedia(new FileInfo(fileInfo));
                    DialogResult startVideo = MessageBox.Show("Video is now ready to be played.\nPress ENTER to start video.",
                    "Video Loaded Successfully.",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);   
                    
                    if(startVideo == DialogResult.OK)
                    {
                        checkIfPlaying();
                    }
                }
                else if (userClickedOK == DialogResult.Cancel)
                {
                    // Do Nothing
                }
                else
                {
                    MessageBox.Show("The System was unable to load the selected video.",
                    "Error Loading the Video",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                }
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult exit = MessageBox.Show("Are you sure you want to exit?",
              "Exit Application.",
              MessageBoxButtons.YesNo,
              MessageBoxIcon.Question);

            if (exit == DialogResult.Yes)
            {
                ExitAnnotation();
            }
            else
            {
                // Do Nothing
            }
        }

        private void exitForm(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                DialogResult result = MessageBox.Show("Are you sure you want to exit?",
                "Exit Application.",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    ExitAnnotation();
                }
                else
                {
                    e.Cancel = true;
                }
            }
            else
            {
                e.Cancel = true;
            }
        }


        private void initializePlotting()
        {
            // Create a timer
            myTimer = new System.Timers.Timer();
            // Tell the timer what to do when it elapses
            myTimer.Elapsed += new ElapsedEventHandler(plotEvent);
            // Set it to go off every x milliseconds (Each 33.3 Seconds = 1 Frame).
            //myTimer.Interval = 33.3;
            myTimer.Interval = 10;
            // And start it        
            myTimer.Enabled = true;
        }

        private void plotEvent(object sender, EventArgs e)
        {
            if (plot1.IsHandleCreated)
            {
                this.Invoke((MethodInvoker)delegate { updateChart(); });
            }
        }

        private void updateChart()
        {
            if (vlcPlayer.GetCurrentMedia() != null )
            {
                // Even though media is in play state there is latency time before it trully starts 
                // Mark the start of the media and annotation
                if (!chart_IsUpdating)
                {
                    if (vlcPlayer.IsPlaying && vlcPlayer.Time > 0)
                    {
                        chart_IsUpdating = true;
                        mediaLength = vlcPlayer.Length;
                    }
                    else
                    {
                        // do not update
                        return;
                    }
                }

                /* Track down the end of the media and Pause 2 sec before
                * Avoid that media reaches the end and freezes
                * (vlcControl becomes inaccessible: unfixed bug in vlc lib) 
                */
                if (vlcPlayer.Time > mediaLength - 2000)
                {
                    vlcPlayer.Pause();

                    // Stop updating chart
                    myTimer.Stop();
                    myTimer.Enabled = false;

                    //stop updating
                    chart_IsUpdating = false;

                    //start annotation detail
                    annotatingDetails = true;

                    /*
                     * Message boxes are used to trigger VLC events(e.g.Play(), Pause();)
                     * since those need to be handled outside the main thread
                    */
                    DialogResult result = MessageBox.Show("You reached the end of the video. Please proceed to the next step.",
                    "Video Annotation Complete.",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                    if (result == DialogResult.OK)
                    {
                        AnnotationDetails();
                    }
                    return;
                }

                double yVal = currentAnnotationValue;
                //Debug.Write("\n vlcTime : " + vlcPlayer.Time);
                TimeSpan tSpan = TimeSpan.FromMilliseconds(vlcPlayer.Time);
                //Debug.Write("\n timespan : " + tSpan);
                double xVal = tSpan.TotalMilliseconds;
                //Debug.Write("\n xVal : " + xVal);

                if (xVal > currentVidTime)
                {
                    plot1.Series["timeLine"].Points.AddXY(xVal, yVal);
                    currentVidTime = xVal;

                    // LOG IT IN FILE
                    LogData(annotionDataPath, xVal * 0.001 + "," + yVal , RLT_ANNOT_FILENAME);
                }

                // Reference Point (This is what the player "controls")
                plot1.Series["refPoint"].Points.Clear();
                plot1.Series["refPoint"].Points.AddXY(xVal, yVal);
                //Debug.Write("\n yVal : " + yVal);
            }
        }


        private void Keyboard_KeyUp(object sender, KeyEventArgs e)
        {
            if (chart_IsUpdating)
            {
                if (e.KeyCode == CURVE_RAISE_KEY)
                {
                    raiseKey_IsPressed = false;
                    MyActionKeyUp();
                }
                else if (e.KeyCode == CURVE_DROP_KEY)
                {
                    dropKey_IsPressed = false;
                    MyActionKeyUp();
                }
            }
        }

        private void MyActionKeyUp()
        {
            // control on the press length
            if (Math.Abs(Math.Abs(currentAnnotationValue) - previousAnnotationValue) >= KEYBOARD_PRESS_CONTROL)
            {
                MarkEvent();               
            }

            previousAnnotationValue = Math.Abs(currentAnnotationValue);
        }

        void Keyboard_KeyPress(object sender, KeyEventArgs e)
        {
            // lock keys (disable changes before annotation start)
            if (chart_IsUpdating)
            {
                if (e.KeyCode == CURVE_RAISE_KEY)
                {
                    // get annotation event time and yVal (first key down)
                    if (!raiseKey_IsPressed)
                    {
                        raiseKey_IsPressed = true;
                        annotationEventTimeSpan = vlcPlayer.Time;
                    }

                    currentAnnotationValue += KEYBOARD_STEP;
                }
                else if (e.KeyCode == CURVE_DROP_KEY)
                {
                    if (!dropKey_IsPressed)
                    {
                        dropKey_IsPressed = true;
                        annotationEventTimeSpan = vlcPlayer.Time;
                    }

                    currentAnnotationValue -= KEYBOARD_STEP;
                }
            }

            // Could add control for media Pause
        }

        /// <summary>
        /// Mark the annotation event dynamically (draw vertical line) 
        /// </summary>
        private void MarkEvent()
        {
            //create evend 
            myEvents.Add(new EventAnnotation(annotationEventTimeSpan));

            StripLine eventMarker = new StripLine
            {
                StripWidth = 0,
                BorderColor = Color.Crimson,
                BorderWidth = 1,
                IntervalOffset = annotationEventTimeSpan,
            };

            plot1.ChartAreas["realtimeChart"].AxisX.StripLines.Add(eventMarker);
        }

        private void startChartThread()
        {
            if (plotUpdater == null)
            {
                // This is what needs to be done on Start-Up!
                plotUpdater = new Thread(new ThreadStart(this.initializePlotting));
                plotUpdater.IsBackground = true;
                plotUpdater.Start();
            }
        }

        private void checkIfPlaying()
        {
            if (vlcPlayer.GetCurrentMedia() == null)
            {
                MessageBox.Show("Please load a video before play.",
                "Error: No Video Loaded",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
                loadVideoFile();
            }
            else
            {
                if (!chart_IsUpdating)
                {
                    if(!vlcPlayer.IsPlaying)
                    {
                        vlcPlayer.Play();
                    }
                }
                else
                {
                    // Do something 
                }
            }
        }


        public void CreateAnnotationFiles(string annotionDataPath, string annotFilename)
        {
            string filePath = annotionDataPath + annotFilename;

            if (filePath != null)
            {
                bool fileExists = File.Exists(filePath);
                bool headerExists = false;

                if (fileExists)
                {
                    using (StreamReader sr = new StreamReader(filePath))
                    {
                        string firstLine = sr.ReadLine();
                        headerExists = firstLine != null;
                    }
                }

                if (!headerExists)
                {
                    string[] headers = getFileHeader(annotFilename);

                    using (StreamWriter sw = new StreamWriter(filePath, true))
                    {
                        sw.WriteLine(string.Join(",", headers));
                    }
                }
            }
        }
        public string CreateDirectory(string annotPath)
        {
            string prjPath = getProjectPath();

            try
            {
                // Create if it doesn't exist
                if (!Directory.Exists(annotPath))
                {
                    DirectoryInfo di = Directory.CreateDirectory(Path.Combine(prjPath, annotPath));

                    string annotionDataPath = di.FullName;

                    CreateAnnotationFiles(annotionDataPath, DISCRET_ANNOT_FILENAME);
                    CreateAnnotationFiles(annotionDataPath, RLT_ANNOT_FILENAME);

                    return annotionDataPath;
                }
            }
            catch
            {
                Debug.Write(annotPath + " directory could not be created");
            }

            return null;
        }

        private string[] getFileHeader(string filename)
        {
            return filename.Equals(DISCRET_ANNOT_FILENAME)
                ? new[] {"Timesatamp",
                         "Arousal_value", "Arousal_confidence",
                         "Pleasure_value", "Pleasure_confidence",
                         "Dominance_value", "Dominance_confidence",
                         "Tag_suggested_by_user","User_tag_suggestion", "Tag_confidence"}

                : new[] {"Timestamp", "Annot_value"};
        }

        private void LogData(string annotionDataPath, string data, string filename)
        {

            if (annotionDataPath + filename != null)
            {
                using (StreamWriter sw = File.AppendText(annotionDataPath + filename))
                {
                    sw.WriteLine(data);
                }
            }
        }

        public static String GetTimestamp(DateTime value)
        {
            return value.ToString("yyyyMMddHHmmssffff");
        }

        public void resetChart()
        {
            myTimer.Stop();

            plot1.Series["timeLine"].Points.Clear();
            plot1.Series["refPoint"].Points.Clear();
            plot1.Series["refPoint"].Points.AddXY(1, 0);

            currentAnnotationValue = 0;
            previousAnnotationValue = 0;
            currentVidTime = 0;

            myTimer.Start();
        }

        private void changeMedia(object sender, VlcMediaPlayerMediaChangedEventArgs e)
        {
            resetChart();
        }

        private void mediaStopped(object sender, VlcMediaPlayerEndReachedEventArgs e)
        {
        }

        private void AnnotPanelShowBtn_Click(object sender, EventArgs e)
        {
            AnnotPanelShowBtn.Image.RotateFlip(RotateFlipType.Rotate180FlipY);
            AnnotationPanel.Visible = !AnnotationPanel.Visible;
        }

        private void AnnotationDetails()
        {
            if (myEvents.Count > 0)
            {
                //init
                annotationIndex = 0;
                eventFrameEnd = 0;

                // if there is only one event
                if (myEvents.Count == 1)
                {
                    nextEventBtn.Enabled = false;
                }
                
                // start manager for event video sequence  
                if (eventFrameManager == null)
                {
                    eventFrameManager = new Thread(new ThreadStart(this.StartEventFrameManagerThread));
                    eventFrameManager.IsBackground = true;
                    eventFrameManager.Start();
                }

                //display details panels
                panel2.Visible = true;
                AnnotationPanel.Visible = true;

                //display first event details
                ShowEvent();

                // display SAM image
                SAMPanel.BackgroundImage = GetImage(GetsamName());
                AddEmotionTagBtn();
            }
            else // if no event marked, exit tool
            {
                DialogResult result = MessageBox.Show("There are no events to annotate. Please exit annotation tool.",
                "Video Annotation Complete.",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

                if (result == DialogResult.OK)
                {
                    ExitAnnotation();
                }
            }
        }

        private void StartEventFrameManagerThread()
        {
            while (annotatingDetails)
            {
                if (vlcPlayer.IsPlaying)
                {
                    //reached end of the playback
                    if (vlcPlayer.Time > eventFrameEnd)
                    {
                        //reset event frame
                        SetEventFrame();

                        vlcPlayer.Pause();
                        playFrameBtn.Invoke(new Action(() => playFrameBtn.Text = "Play"));
                    }
                }
            }
        }

        private void prevEventBtn_Click(object sender, EventArgs e)
        {
            nextEventBtn.Enabled = true;           
            SaveEventDetails();

            // for safety
            if (annotationIndex > 0)
            {
                annotationIndex -= 1;
                ShowEvent();
            }

            if (annotationIndex <= 0)
            {
                prevEventBtn.Enabled = false;
            }
        }
        

        private void nextEventBtn_Click(object sender, EventArgs e)
        {
            prevEventBtn.Enabled = true;
            SaveEventDetails();    

            // for safety
            if (annotationIndex < myEvents.Count - 1)
            {
                annotationIndex += 1;
                ShowEvent();
            }
            
            if(annotationIndex >= myEvents.Count - 1)
            {
                nextEventBtn.Enabled = false;
            }
        }

        private void SaveEventDetails()
        {
            if (myEvents.Count > 0)
            {
                var currentEvent = myEvents[annotationIndex];

                //PAD values
                currentEvent.SetArousal(arousalTB.Value);
                currentEvent.SetPleasure(pleasureTB.Value);
                currentEvent.SetDominance(DominanceTB.Value);

                //tags
                currentEvent.SetTagIsOther(otherTagCB.Checked);
                currentEvent.SetTag(otherTagTxt.Text);

                //rates
                currentEvent.SetArousalConfRate(Convert.ToInt16(ArousalStar.Tag));
                currentEvent.SetPleasureConfRate(Convert.ToInt16(PleasureStar.Tag));
                currentEvent.SetDominanceConfRate(Convert.ToInt16(DominanceStar.Tag));
                currentEvent.SetTagConfRate(Convert.ToInt16(TagStar.Tag));
            }
        }

        private void ShowEvent()
        {
            if(myEvents.Count > 0)
            {
                var currentEvent = myEvents[annotationIndex];

                //move marker
                plot1.Series["refPoint"].Points.Clear();
                plot1.Series["refPoint"].Points.AddXY(currentEvent.GetTime(), 0);

                //set frame
                SetEventFrame();

                //PAD values
                arousalTB.Value = currentEvent.GetArousal();
                pleasureTB.Value = currentEvent.GetPleasure();
                DominanceTB.Value = currentEvent.GetDominance();

                //tags
                otherTagCB.Checked = currentEvent.GetTagIsOther();
                otherTagTxt.Text = currentEvent.GetTag();

                //rates
                ArousalStar.BackgroundImage = GetImage("star"+ currentEvent.GetArousalConfRate());
                ArousalStar.Tag = currentEvent.GetArousalConfRate();
                PleasureStar.BackgroundImage = GetImage("star" + currentEvent.GetPleasureConfRate());
                PleasureStar.Tag = currentEvent.GetPleasureConfRate();
                DominanceStar.BackgroundImage = GetImage("star" + currentEvent.GetDominanceConfRate());
                DominanceStar.Tag = currentEvent.GetDominanceConfRate();
                TagStar.BackgroundImage = GetImage("star" + currentEvent.GetTagConfRate());
                TagStar.Tag = currentEvent.GetTagConfRate();
            }
        }

        private void arousalTB_ValueChanged(object sender, EventArgs e)
        {
            SAMPanel.BackgroundImage = GetImage(GetsamName());
            AddEmotionTagBtn();
        }

        private void DominanceTB_ValueChanged(object sender, EventArgs e)
        {
            SAMPanel.BackgroundImage = GetImage(GetsamName());
            AddEmotionTagBtn();
        }

        private void pleasureTB_ValueChanged(object sender, EventArgs e)
        {
            SAMPanel.BackgroundImage = GetImage(GetsamName());
            AddEmotionTagBtn();
        }

        private void otherTagCB_CheckedChanged(object sender, EventArgs e)
        {
            emotionTagPanel.Enabled = !otherTagCB.Checked;
            otherTagTxt.Enabled = otherTagCB.Checked;
            otherTagTxt.Text = "";
        }

        private void playFrameBtn_Click(object sender, EventArgs e)
        {
            if(annotatingDetails)
            {
                if (vlcPlayer.IsPlaying)
                {
                    vlcPlayer.Pause();
                    playFrameBtn.Text = "Play";

                    //reset event frame
                    SetEventFrame();
                }
                else
                {
                    //start playing 5 sec before for 10 sec
                    SetEventFramePlayStart();
                    SetEventFramePlayEnd();

                    //play
                    vlcPlayer.Play();

                    playFrameBtn.Text = "Stop";
                }
            }
        }

        /// <summary>
        /// Display the frame when the event was marked
        /// </summary>
        private void SetEventFrame()
        {
            vlcPlayer.Time = myEvents[annotationIndex].GetTime();
        }

        /// <summary>
        /// Set the beginning of the event window (5s before marker)
        /// Note: Event frame window is 10s (5s before and after marker)
        /// </summary>
        private void SetEventFramePlayStart()
        {
            if (myEvents[annotationIndex].GetTime() - 5000 < 0)
                vlcPlayer.Time = 0;
            else
                vlcPlayer.Time = myEvents[annotationIndex].GetTime() - 5000;
        }

        /// <summary>
        /// Set the end of the event window (5s after marker)
        /// Note: Event frame window is 10s (5s before and after marker)
        /// </summary>
        private void SetEventFramePlayEnd()
        {
            if (myEvents[annotationIndex].GetTime() + 5000 > mediaLength - 2000)
                eventFrameEnd = (long)(mediaLength - 2000);
            else
                eventFrameEnd = myEvents[annotationIndex].GetTime() + 5000;
        }

        private void StopEventFrameManagerThread()
        {
            annotatingDetails = false;
            if (eventFrameManager != null)
            {
                while (eventFrameManager.IsAlive)
                {
                    //wait for end
                }
            }
        }

        private void eventAnnotFinishBtn_Click(object sender, EventArgs e)
        {
            DialogResult exit = MessageBox.Show("Are you sure you want to exit?",
              "Exit Application.",
              MessageBoxButtons.YesNo,
              MessageBoxIcon.Question);

            if (exit == DialogResult.Yes)
            {
                ExitAnnotation();
            }
            else
            {
                // Do Nothing
            }
        }

        private void ExitAnnotation()
        {
            // Stop updating chart
            if (myTimer.Enabled)
            {
                myTimer.Stop();
                myTimer.Enabled = false;
            }

            //log event details
            if (myEvents.Count > 0)
            {
                if(annotatingDetails)
                {
                    SaveEventDetails();

                    StopEventFrameManagerThread();
                }

                foreach (EventAnnotation evt in myEvents)
                {
                    LogData(annotionDataPath, evt.EventToString(), DISCRET_ANNOT_FILENAME);
                }
            }

            Environment.Exit(0);
        }

        #region Info Bubbles
        private void arousalInfo_MouseHover(object sender, EventArgs e)
        {
            infoBubbleTT.SetToolTip(arousalInfo,
                "Level of body activation. Ranges from calm (almost sleeping)\n" +
                "to agitated (bursting with arousal).");
        }

        private void PleasureInfo_MouseHover(object sender, EventArgs e)
        {
            infoBubbleTT.SetToolTip(PleasureInfo,
                "Describes states of pleasantness (positive emotions) and \n" +
                "unpleasantness (negative emotions).");
        }

        private void DominanceInfo_MouseHover(object sender, EventArgs e)
        {
            infoBubbleTT.SetToolTip(DominanceInfo,
                "Reflects one’s level of control over a situation. Feeling small, \n" +
                "intimidated, resigned and not in control reflects states of submission.\n" +
                "Emotions over which one has more control, such as being recognized, \n" +
                "dominant and decisive describe high-dominance.");
        }

        private void emotionTagInfo_MouseHover(object sender, EventArgs e)
        {
            infoBubbleTT.SetToolTip(emotionTagInfo,
                "The following tags are suggestions defined by Arousal, Pleasure and Dominance levels.\n" +
                "Select the Tag that best describe your current emotional state.");
        }

        private void otherTagInfo_MouseHover(object sender, EventArgs e)
        {
            infoBubbleTT.SetToolTip(otherTagInfo,
                "If none of the suggestions are suitable, please check other \n" +
                "and write the appropriate tag.");
        }
        #endregion

        private Image GetImage(string samRef)
        {
            ResourceManager rm = new ResourceManager("AffectRankTrace.Properties.Resources", Assembly.GetExecutingAssembly());

            Image img = rm.GetObject(samRef) as Image;
            rm.ReleaseAllResources();

            return img;
        }

        private string GetsamName()
        {
            string samName = "d" + DominanceTB.Value.ToString().Replace('-', '_') +
                             "v" + pleasureTB.Value.ToString().Replace('-', '_') +
                             "a" + arousalTB.Value.ToString().Replace('-', '_');

            return samName;
        }

        private void AddEmotionTagBtn()
        {
            ReinitTagButtons();

            tagList = GetEmotionTagsParsedBorne();

            if (tagList.Count > 0)
            {
                NoSuggPnl.Visible = false;
                NoSuggPnl.SendToBack();

                int i = 0;
                string btnName;
                while (i < tagList.Count)
                {
                    btnName = "tagBtn" + i.ToString();

                    foreach (Control c in emotionTagPanel.Controls)
                    {
                        if (c.Name == btnName)
                        {
                            c.Visible = true;
                            c.Text = tagList[i];
                            break;
                        }
                    }

                    i++;
                }
            }
            else
            {
                NoSuggPnl.Visible = true;
                NoSuggPnl.BringToFront();
            }
        }

        private List<string> GetEmotionTagsParsedBorne()
        {
            return emotionList.GetEmotionTags(pleasureTB.Value, arousalTB.Value, DominanceTB.Value);
        }

        private void ReinitTagButtons()
        {
            foreach (Control c in emotionTagPanel.Controls)
            {
                c.Visible = false;
                c.Text = "";
            }
        }

        private void TagBtn_Click(object sender, EventArgs e)
        {
            Button btn = (Button)sender;
            if (btn != null)
            {
                otherTagTxt.Text = btn.Text;
            }
        }

        private void StarRate_MouseDown(object sender, MouseEventArgs e)
        {
            Panel currentRatingControl = (Panel)sender;

            //get star panel current Y position
            var starPnlPosition = StarRatePnl.Location.Y + StarRatePnl.Height;

            // if called by the current Rating Control show/hide
            if (starPnlPosition == (currentRatingControl.Parent.Location.Y + currentRatingControl.Location.Y))
            {
                StarRatePnl.Visible = !StarRatePnl.Visible;
            }
            else
            {
                // change location
                var newX = currentRatingControl.Parent.Location.X + currentRatingControl.Location.X;
                var newY = (currentRatingControl.Parent.Location.Y + currentRatingControl.Location.Y) - StarRatePnl.Height;
                
                StarRatePnl.Location = new Point(
                    newX,
                    newY
                );

               StarRatePnl.Visible = true;
            }
        }

        private void Star_Click(object sender, EventArgs e)
        {
            Panel pnl = (Panel)sender;

            var position = StarRatePnl.Location.Y + StarRatePnl.Height;

            if (position == (ArousalStar.Parent.Location.Y + ArousalStar.Location.Y))
            {
                ArousalStar.BackgroundImage = GetImage(pnl.Name);
                ArousalStar.Tag = pnl.Tag;
            }
            else if (position == (PleasureStar.Parent.Location.Y + PleasureStar.Location.Y))
            {
                PleasureStar.BackgroundImage = GetImage(pnl.Name);
                PleasureStar.Tag = pnl.Tag;
            }
            else if (position == (DominanceStar.Parent.Location.Y + DominanceStar.Location.Y))
            {
                DominanceStar.BackgroundImage = GetImage(pnl.Name);
                DominanceStar.Tag = pnl.Tag;
            }
            else if (position == (TagStar.Parent.Location.Y + TagStar.Location.Y))
            {
                TagStar.BackgroundImage = GetImage(pnl.Name);
                TagStar.Tag = pnl.Tag;
            }
        }

        private string getProjectPath()
        {
            // Get app base directory
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // Get parent directory
            DirectoryInfo parentDirectory = Directory.GetParent(baseDirectory).Parent.Parent;

            return parentDirectory.FullName;
        }

        private string getEmotionListPath()
        {

            string prjPath = getProjectPath();

            // Directory should be same level as bin
            string emotionListFile = Path.Combine(prjPath, EMOTION_TAGS_FILENAME);

            if (File.Exists(emotionListFile))
            {
                return emotionListFile;
            }
            else
            {
                MessageBox.Show("\""+ emotionListFile + "\" is not be found. Download the file from github and place it under: " + prjPath);
                return null;
            }
        }
    }
}
