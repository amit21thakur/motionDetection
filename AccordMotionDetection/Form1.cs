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

namespace AccordMotionDetection
{
    public partial class Form1 : Form
    {
        private FilterInfoCollection videoDevices;
        private VideoCaptureDevice videoSource;
        private Bitmap currentFrame;
        private Bitmap previousFrame;
        private bool motionDetected;
        private LinkedList<Rectangle> noMotionAreas;

        public Form1()
        {
            InitializeComponent();
            motionDetected = false;
            noMotionAreas = new LinkedList<Rectangle>();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (videoDevices.Count == 0)
            {
                MessageBox.Show("No video devices found.");
                return;
            }

            // Select the first video device
            videoSource = new VideoCaptureDevice(videoDevices[0].MonikerString);
            videoSource.NewFrame += VideoSource_NewFrame;

            // Start capturing
            videoSource.Start();
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

        private void DetectNoMotionAreas(Bitmap diffFrame)
        {
            const int motionThreshold = 30; // Adjust this threshold as needed

            // Clear the previous no motion areas
            noMotionAreas.Clear();

            // Identify areas of no motion
            for (int x = 0; x < diffFrame.Width; x++)
            {
                for (int y = 0; y < diffFrame.Height; y++)
                {
                    Color pixel = diffFrame.GetPixel(x, y);
                    if (pixel.R <= motionThreshold && pixel.G <= motionThreshold && pixel.B <= motionThreshold)
                    {
                        // Check if the current pixel is part of an existing no motion area
                        bool foundArea = false;
                        foreach (Rectangle area in noMotionAreas)
                        {
                            if (area.Contains(x, y))
                            {
                                foundArea = true;
                                break;
                            }
                        }

                        if (!foundArea)
                        {
                            // Expand the no motion area and add it to the linked list
                            Rectangle noMotionArea = ExpandNoMotionArea(diffFrame, x, y, motionThreshold);
                            noMotionAreas.AddLast(noMotionArea);
                        }
                    }
                }
            }
        }

        private Rectangle ExpandNoMotionArea(Bitmap diffFrame, int startX, int startY, int motionThreshold)
        {
            int maxX = diffFrame.Width - 1;
            int maxY = diffFrame.Height - 1;

            int minX = startX;
            int minY = startY;
            int maxXExpanded = startX;
            int maxYExpanded = startY;

            Queue<Point> queue = new Queue<Point>();
            queue.Enqueue(new Point(startX, startY));

            while (queue.Count > 0)
            {
                Point currentPoint = queue.Dequeue();
                int x = currentPoint.X;
                int y = currentPoint.Y;

                if (x < minX) minX = x;
                if (x > maxXExpanded) maxXExpanded = x;
                if (y < minY) minY = y;
                if (y > maxYExpanded) maxYExpanded = y;

                // Check the neighboring pixels in a 3x3 window
                for (int i = -1; i <= 1; i++)
                {
                    for (int j = -1; j <= 1; j++)
                    {
                        int neighborX = x + i;
                        int neighborY = y + j;

                        // Skip out-of-bounds pixels
                        if (neighborX < 0 || neighborX > maxX || neighborY < 0 || neighborY > maxY)
                            continue;

                        // Check if the neighbor is part of the no motion area
                        Color neighborPixel = diffFrame.GetPixel(neighborX, neighborY);
                        if (neighborPixel.R <= motionThreshold && neighborPixel.G <= motionThreshold && neighborPixel.B <= motionThreshold)
                        {
                            diffFrame.SetPixel(neighborX, neighborY, Color.White); // Optional: Mark the expanded area as white in the difference frame
                            queue.Enqueue(new Point(neighborX, neighborY));
                        }
                    }
                }
            }

            int widthExpanded = maxXExpanded - minX + 1;
            int heightExpanded = maxYExpanded - minY + 1;
            return new Rectangle(minX, minY, widthExpanded, heightExpanded);
        }

        private void btnUpload_Click(object sender, EventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Video Files|*.mp4;*.avi;*.wmv";
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                // Stop the current video source
                if (videoSource != null && videoSource.IsRunning)
                {
                    videoSource.SignalToStop();
                    videoSource.WaitForStop();
                    videoSource.NewFrame -= VideoSource_NewFrame;
                    videoSource = null;
                }

                // Open the selected video file
                videoSource = new VideoCaptureDevice(openFileDialog.FileName);
                videoSource.NewFrame += VideoSource_NewFrame;

                // Start capturing
                videoSource.Start();

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