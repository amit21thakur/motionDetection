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


        private MotionDetector detector = new MotionDetector(
            new TwoFramesDifferenceDetector(),
            new MotionAreaHighlighting());

        public Form1()
        {
            InitializeComponent();
            motionDetected = false;
            noMotionAreas = new LinkedList<NoMotionItem>();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //return;
            //videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            //if (videoDevices.Count == 0)
            //{
            //    MessageBox.Show("No video devices found.");
            //    return;
            //}

            //// Select the first video device
            //videoSource = new VideoCaptureDevice(videoDevices[0].MonikerString);
            //videoSource.NewFrame += VideoSource_NewFrame;

            //// Start capturing
            //videoSource.Start();
        }

        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            videoSource.NewFrame -= VideoSource_NewFrame;

            try
            {
                // Update the current frame
                if (currentFrame != null)
                    currentFrame.Dispose();
                currentFrame = (Bitmap)eventArgs.Frame.Clone();

                // Convert the current frame to grayscale
                Bitmap grayFrame = Grayscale.CommonAlgorithms.BT709.Apply(currentFrame);

                // Check if it's the first frame
                if (previousFrame == null)
                {
                    // Initialize the previous frame with the current frame
                    previousFrame = (Bitmap)grayFrame.Clone();
                    return;
                }

                // Apply frame differencing
                Bitmap diffFrame = new Bitmap(grayFrame.Width, grayFrame.Height);
                for (int x = 0; x < grayFrame.Width; x++)
                {
                    for (int y = 0; y < grayFrame.Height; y++)
                    {
                        Color previousPixel = previousFrame.GetPixel(x, y);
                        Color currentPixel = grayFrame.GetPixel(x, y);
                        int diff = Math.Abs(currentPixel.R - previousPixel.R);
                        diffFrame.SetPixel(x, y, Color.FromArgb(diff, diff, diff));
                    }
                }

                // Update the PictureBox controls
                pictureBox1.Image = currentFrame;
                pictureBox2.Image = diffFrame;

                // Update the previous frame
                previousFrame.Dispose();
                previousFrame = (Bitmap)grayFrame.Clone();

                // Check for motion detection
                if (motionDetected)
                {
                    // TBD
                }
                //DetectNoMotionAreas(diffFrame);
            }
            finally
            {
                videoSource.NewFrame += VideoSource_NewFrame;
            }
        }

        private void btnUpload_Click(object sender, EventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Video Files|*.mp4;*.avi;*.wmv;*.mkv";
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                videoReader = new VideoFileReader();
                videoReader.Open(openFileDialog.FileName);
                bool inMotion = false;
                TimeSpan? startTime = null ;
                // Iterate over video frames
                for (int frameNumber = 0; frameNumber < videoReader.FrameCount; frameNumber++)
                {
                    // Read the next frame
                    var frame = videoReader.ReadVideoFrame();


                    // Perform processing operations on the frame
                    // ...
                    var motionLevel = detector.ProcessFrame(frame);
                    if(motionLevel > minMotionLevel) 
                    {
                        if(!inMotion)
                        {
                            if (!startTime.HasValue)
                            {
                                throw new InvalidOperationException("Start Time could not be null");
                            }
                            var endTime = TimeSpan.FromSeconds(frameNumber / videoReader.FrameRate.Value);
                            noMotionAreas.AddLast(new NoMotionItem(startTime.Value, endTime));
                            startTime = null;
                            inMotion = true;
                        }
                    }
                    else
                    {
                        if(frameNumber == 1)
                        {
                            startTime = TimeSpan.FromSeconds(frameNumber / videoReader.FrameRate.Value);
                        }
                        if(inMotion)
                        {
                            //Add no motion item in LL
                            startTime = TimeSpan.FromSeconds(frameNumber / videoReader.FrameRate.Value);
                            inMotion = false;
                        }
                        if(frameNumber == videoReader.FrameCount - 1)
                        {
                            if(!inMotion && startTime.HasValue)
                            {
                                var endTime = TimeSpan.FromSeconds(frameNumber / videoReader.FrameRate.Value);
                                noMotionAreas.AddLast(new NoMotionItem(startTime.Value, endTime));
                            }
                        }
                    }
                    // Dispose the frame after processing
                    frame.Dispose();

                }

            }
        }


        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Stop capturing and release resources
            if (videoSource != null && videoSource.IsRunning)
            {
                videoSource.SignalToStop();
                videoSource.WaitForStop();
                videoSource.NewFrame -= VideoSource_NewFrame;
                videoSource = null;
            }

            if (currentFrame != null)
            {
                currentFrame.Dispose();
                currentFrame = null;
            }
            if (previousFrame != null)
            {
                previousFrame.Dispose();
                previousFrame = null;
            }

            base.OnFormClosing(e);
        }

        private void btnPlay_Click(object sender, EventArgs e)
        {

        }
    }
}