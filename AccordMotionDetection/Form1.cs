using Accord.Video.DirectShow;
using Accord.Video.FFMPEG;
using Accord.Video.VFW;
using Accord.Imaging;
using System;
using System.Drawing;
using System.Windows.Forms;
using Accord.Imaging.Filters;
using Accord.Video;
using System.Collections.Generic;
using Accord.Vision.Motion;
using AccordMotionDetection.Models;

namespace AccordMotionDetection
{
    public partial class Form1 : Form
    {
        private FilterInfoCollection videoDevices;
        private VideoCaptureDevice videoSource;
        private Bitmap currentFrame;
        private Bitmap previousFrame;
        private bool motionDetected;
        private LinkedList<NoMotionItem> noMotionAreas;
        private float minMotionLevel = 0.01f;
        private VideoFileReader videoReader;
        private Timer timer;
        private bool IsInMotion = false;
        private TimeSpan? noMotionStartTime = null;
        private int minSecondsToIgnore = 0;

        private bool isDemoMode = false;
        private int currentFrameIndex = -1;


        private MotionDetector detector = new MotionDetector(
            new TwoFramesDifferenceDetector(),
            new MotionAreaHighlighting());

        public Form1()
        {
            InitializeComponent();
            motionDetected = false;
            noMotionAreas = new LinkedList<NoMotionItem>();
        }

        private void btnUpload_Click(object sender, EventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Video Files|*.mp4;*.avi;*.wmv;*.mkv";
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                var dtStart = DateTime.Now;

                videoReader = new VideoFileReader();
                videoReader.Open(openFileDialog.FileName);
                if (!isDemoMode)
                {
                    bool inMotion = false;
                    TimeSpan? startTime = null;
                    // Iterate over video frames
                    for (int frameNumber = 0; frameNumber < videoReader.FrameCount; frameNumber++)
                    {
                        // Read the next frame
                        var frame = videoReader.ReadVideoFrame();

                        if (frame == null)
                            continue;

                        // Perform processing operations on the frame
                        // ...
                        var motionLevel = detector.ProcessFrame(frame);
                        if (motionLevel > minMotionLevel)
                        {
                            //Motion has started or No-motion has stopped
                            if (!inMotion)
                            {
                                if (!startTime.HasValue)
                                {
                                    if (frameNumber < 10)
                                    {
                                        startTime = new TimeSpan(0);
                                    }
                                    else
                                    {
                                        throw new InvalidOperationException("Start Time could not be null");
                                    }
                                }
                                var endTime = TimeSpan.FromSeconds(frameNumber / videoReader.FrameRate.Value);
                                var node = new NoMotionItem(startTime.Value, endTime);
                                AddNodeToLinkedList(node);
                                startTime = null;
                                inMotion = true;
                            }
                        }
                        else
                        {
                            //No Motion
                            if (frameNumber == 1)
                            {
                                startTime = TimeSpan.FromSeconds(frameNumber / videoReader.FrameRate.Value);
                            }
                            if (inMotion)
                            {
                                //Add no motion item in LL
                                startTime = TimeSpan.FromSeconds(frameNumber / videoReader.FrameRate.Value);
                                inMotion = false;
                            }
                            if (frameNumber == videoReader.FrameCount - 1)
                            {
                                if (!inMotion && startTime.HasValue)
                                {
                                    var endTime = TimeSpan.FromSeconds(frameNumber / videoReader.FrameRate.Value);
                                    var node = new NoMotionItem(startTime.Value, endTime);
                                    AddNodeToLinkedList(node);
                                }
                            }
                        }
                        // Dispose the frame after processing
                        frame.Dispose();

                    }
                    MessageBox.Show($"{dtStart} till {DateTime.Now}");
                }
                else
                {
                    // Create a timer to advance frames
                    timer = new Timer();
                    timer.Tick += new EventHandler(ShowNextFrame);

                    // Sets the timer interval to 5 seconds.
                    timer.Interval = (int)Math.Round(1000.0 / videoReader.FrameRate.Value);
                    timer.Start();
                }
            }
        }


        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            isDemoMode = chkDemoMode.Checked;
        }




        // Method to show the next frame
        private void ShowNextFrame(object state, EventArgs args)
        {
            // Read the next frame
            currentFrame = videoReader.ReadVideoFrame();
            currentFrameIndex++;

            lblFrame.Text = $"{currentFrameIndex}/{videoReader.FrameCount}";

            // Perform processing operations on the frame
            // ...
            var motionLevel = currentFrame != null ? detector.ProcessFrame(currentFrame): 0;
            if (motionLevel > minMotionLevel)
            {
                if (!IsInMotion)
                {
                    if (!noMotionStartTime.HasValue)
                    {
                        if (currentFrameIndex < 10)
                        {
                            noMotionStartTime = new TimeSpan(0);
                        }
                        else
                        {
                            throw new InvalidOperationException("Start Time could not be null");
                        }
                    }
                    var endTime = TimeSpan.FromSeconds(currentFrameIndex / videoReader.FrameRate.Value);
                    var node = new NoMotionItem(noMotionStartTime.Value, endTime);
                    AddNodeToLinkedList(node);
                    noMotionStartTime = null;
                    IsInMotion = true;
                }
            }
            else
            {
                if (currentFrameIndex == 1)
                {
                    noMotionStartTime = TimeSpan.FromSeconds(currentFrameIndex / videoReader.FrameRate.Value);
                }
                if (IsInMotion)
                {
                    //Add no motion item in LL
                    noMotionStartTime = TimeSpan.FromSeconds(currentFrameIndex / videoReader.FrameRate.Value);
                    IsInMotion = false;
                }
                if (currentFrameIndex == videoReader.FrameCount - 1)
                {
                    if (!IsInMotion && noMotionStartTime.HasValue)
                    {
                        var endTime = TimeSpan.FromSeconds(currentFrameIndex / videoReader.FrameRate.Value);
                        var node = new NoMotionItem(noMotionStartTime.Value, endTime);
                        AddNodeToLinkedList(node);
                    }
                }
            }

            // Update the PictureBox with the current frame
            lbl.Text = IsInMotion ? "MOTION" : "NO MOTION";
            pictureBox1.Image = this.currentFrame;
           
            // Check if we reached the end of the video
            if (currentFrameIndex + 1 >= videoReader.FrameCount)
            {
                // Stop the timer and reset the current frame index
                timer.Dispose();
                currentFrameIndex = -1;
                return;
            }

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

  
        private void AddNodeToLinkedList(NoMotionItem node)
        {
            if (node.IsValid(minSecondsToIgnore))
            {
                noMotionAreas.AddLast(node);
                textBox1.Text += node.ToString();
            }
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            minMotionLevel = float.Parse(txtSensitivity.Text);
            minSecondsToIgnore = int.Parse(txtIgnoreInSecs.Text);
        }

        private void txtSensitivity_TextChanged(object sender, EventArgs e)
        {
            minMotionLevel = float.Parse(txtSensitivity.Text);
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            minSecondsToIgnore = int.Parse(txtIgnoreInSecs.Text);
        }
    }
}