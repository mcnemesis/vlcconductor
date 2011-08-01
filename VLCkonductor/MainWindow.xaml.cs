using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Research.Kinect.Nui;
using Microsoft.Research.Kinect.Audio;
//speech features currently disabled due to lack of speech recognition sdk for testing
using Microsoft.Speech.Recognition; 
using System.Threading;
using System.IO;
using Microsoft.Speech.AudioFormat;
using System.Diagnostics;

namespace VLCkonductor
{
    public partial class MainWindow : Window
    {  

        bool isForwardGestureActive = false;
        bool isBackGestureActive = false;
        bool isForwardUpGestureActive = false;
        bool isBackUpGestureActive = false;
        bool isBowGesture = false;

        //Instantiate the Kinect runtime. Required to initialize the device.
        //IMPORTANT NOTE: You can pass the device ID here, in case more than one Kinect device is connected.
        Runtime runtime = new Runtime();

        bool isCirclesVisible = true;

        SolidColorBrush activeBrush = new SolidColorBrush(Colors.Orange);
        SolidColorBrush inactiveBrush = new SolidColorBrush(Colors.Gray);

        private const string RecognizerId = "SR_MS_en-US_Kinect_10.0";
        bool shouldContinue = true;

        public MainWindow()
        {
            InitializeComponent();

            //Runtime initialization is handled when the window is opened. When the window
            //is closed, the runtime MUST be unitialized.
            this.Loaded += new RoutedEventHandler(MainWindow_Loaded);
            this.Closed += new EventHandler(MainWindow_Closed);
            //Handle the content obtained from the video camera, once received.

            this.KeyDown += new KeyEventHandler(MainWindow_KeyDown);
        }

        void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.C)
            {
                ToggleCircles();
            }
            else if (e.Key == Key.S)
            {
                shouldContinue = false;
            }
        }

        void runtime_VideoFrameReady(object sender, Microsoft.Research.Kinect.Nui.ImageFrameReadyEventArgs e)
        {
            PlanarImage image = e.ImageFrame.Image;

            BitmapSource source = BitmapSource.Create(image.Width, image.Height, 96, 96,
                PixelFormats.Bgr32, null, image.Bits, image.Width * image.BytesPerPixel);
            videoImage.Source = source;
        }

        void runtime_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            SkeletonFrame skeletonSet = e.SkeletonFrame;

            SkeletonData data = (from s in skeletonSet.Skeletons
                                 where s.TrackingState == SkeletonTrackingState.Tracked
                                 select s).FirstOrDefault();

            vlcHipCentre.Visibility = data.TrackingState == SkeletonTrackingState.NotTracked ?
                System.Windows.Visibility.Hidden : System.Windows.Visibility.Visible;

            var head = data.Joints[JointID.Head];
            var rightHand = data.Joints[JointID.HandRight];
            var leftHand = data.Joints[JointID.HandLeft];
            var shoulderCentre = data.Joints[JointID.ShoulderCenter];            
            var hipcentre = data.Joints[JointID.HipCenter];

            SetEllipsePosition(ellipseHead, head, isBowGesture);

            SetEllipsePosition(ellipseShoulderCentre, shoulderCentre, false);

            ShowVLCHipCentre(vlcHipCentre, hipcentre);

            SetEllipsePosition(ellipseLeftHand, leftHand, isForwardGestureActive || isBackGestureActive || isBackUpGestureActive);
            SetEllipsePosition(ellipseRightHand, rightHand, isBackGestureActive || isForwardGestureActive || isForwardUpGestureActive);
            

            ProcessGestures(head,shoulderCentre, hipcentre, rightHand, leftHand);
        }

        private void ShowVLCHipCentre(Image vlcHipCentre, Joint joint)
        {
            float x, y;

            runtime.SkeletonEngine.SkeletonToDepthImage(joint.Position, out x, out y);

            Canvas.SetLeft(vlcHipCentre, x * 640 - vlcHipCentre.ActualWidth / 2);
            Canvas.SetTop(vlcHipCentre, y * 480 - vlcHipCentre.ActualHeight / 2);          
        }

        /// <summary>
        /// detect and set flags for currently active gestures
        /// </summary>
        /// <param name="head"></param>
        /// <param name="rightHand"></param>
        /// <param name="leftHand"></param>
        private void ProcessGestures(Joint head, Joint shoulderCentre, Joint hipCentre, Joint rightHand, Joint leftHand)
        { 
            //volume up
            if ((rightHand.Position.X > head.Position.X)
               &&(rightHand.Position.Y > head.Position.Y))
            {
                if ((!isBackGestureActive && !isForwardGestureActive)
                    && (!isBackUpGestureActive && !isForwardUpGestureActive)
                    && !isBowGesture)
                {
                    isForwardUpGestureActive = true;
                    System.Windows.Forms.SendKeys.SendWait("{Up}");
                }
            }
            else
            {
                isForwardUpGestureActive = false;
            }

            //volume down
            if ((leftHand.Position.X < head.Position.X)
                && (leftHand.Position.Y > head.Position.Y))
            {
                if ((!isBackGestureActive && !isForwardGestureActive)
                    && (!isBackUpGestureActive && !isForwardUpGestureActive)
                    && !isBowGesture)
                {
                    isBackUpGestureActive = true;
                    System.Windows.Forms.SendKeys.SendWait("{Down}");
                }
            }
            else
            {
                isBackUpGestureActive = false;
            }

            //play next
            if ((rightHand.Position.X > head.Position.X + 0.45) && (leftHand.Position.X > head.Position.X))
              // && (rightHand.Position.Y > head.Position.Y + 0.45))
            {
                if ((!isBackGestureActive && !isForwardGestureActive)
                    && (!isBackUpGestureActive && !isForwardUpGestureActive)
                    && !isBowGesture)
                {
                    isForwardGestureActive = true;
                    System.Windows.Forms.SendKeys.SendWait("{n}");
                    System.Windows.Forms.SendKeys.SendWait("{Right}");
                }
            }
            else
            {
                isForwardGestureActive = false;
            }

            //play previous
            if ((leftHand.Position.X < head.Position.X - 0.45) && (rightHand.Position.X < head.Position.X))
              //  && (leftHand.Position.Y > head.Position.Y + 0.45))
            {
                if ((!isBackGestureActive && !isForwardGestureActive)
                    && (!isBackUpGestureActive && !isForwardUpGestureActive)
                    && !isBowGesture)
                {
                    isBackGestureActive = true;
                    System.Windows.Forms.SendKeys.SendWait("{p}");
                    System.Windows.Forms.SendKeys.SendWait("{Left}");
                }
            }
            else
            {
                isBackGestureActive = false;
            }

            //toggle stop start
            if ((Math.Abs(head.Position.Y - shoulderCentre.Position.Y) > Math.Abs(leftHand.Position.Y - shoulderCentre.Position.Y))
                && (Math.Abs(head.Position.Y - shoulderCentre.Position.Y) > Math.Abs(rightHand.Position.Y - shoulderCentre.Position.Y)))
            {
                if ((!isBackGestureActive && !isForwardGestureActive)
                    && (!isBackUpGestureActive && !isForwardUpGestureActive)
                    && !isBowGesture)
                {
                    System.Windows.Forms.SendKeys.SendWait("{y}");
                    isBowGesture = true;
                }
            }
            else
            {
                isBowGesture = false;
            }
        }

        //This method is used to position the ellipses on the canvas
        //according to correct movements of the tracked joints.
        private void SetEllipsePosition(Ellipse ellipse, Joint joint, bool isHighlighted)
        {
            float x, y;
            runtime.SkeletonEngine.SkeletonToDepthImage(joint.Position, out x, out y);

            if (isHighlighted)
            {
                ellipse.Width = 60;
                ellipse.Height = 60;
                ellipse.Fill = activeBrush;
            }
            else
            {
                ellipse.Width = 20;
                ellipse.Height = 20;
                ellipse.Fill = inactiveBrush;
            }

            Canvas.SetLeft(ellipse, x * 640 - ellipse.ActualWidth / 2);
            Canvas.SetTop(ellipse, y * 480 - ellipse.ActualHeight / 2);
        }

        void ToggleCircles()
        {
            if (isCirclesVisible)
                HideCircles();
            else
                ShowCircles();
        }

        void HideCircles()
        {
            isCirclesVisible = false;
            ellipseHead.Visibility = System.Windows.Visibility.Collapsed;
            ellipseLeftHand.Visibility = System.Windows.Visibility.Collapsed;
            ellipseRightHand.Visibility = System.Windows.Visibility.Collapsed;
        }

        void ShowCircles()
        {
            isCirclesVisible = true;
            ellipseHead.Visibility = System.Windows.Visibility.Visible;
            ellipseLeftHand.Visibility = System.Windows.Visibility.Visible;
            ellipseRightHand.Visibility = System.Windows.Visibility.Visible;
        }

        void MainWindow_Closed(object sender, EventArgs e)
        {
            runtime.Uninitialize();
            shouldContinue = false;
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //Since only a color video stream is needed, RuntimeOptions.UseColor is used.
            runtime.Initialize(Microsoft.Research.Kinect.Nui.RuntimeOptions.UseColor | RuntimeOptions.UseSkeletalTracking);

            runtime.VideoFrameReady += new EventHandler<Microsoft.Research.Kinect.Nui.ImageFrameReadyEventArgs>(runtime_VideoFrameReady);
            
            runtime.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(runtime_SkeletonFrameReady);

            //You can adjust the resolution here.
            runtime.VideoStream.Open(ImageStreamType.Video, 2, ImageResolution.Resolution640x480, ImageType.Color);

            //runtime.NuiCamera.ElevationAngle = -27;
            runtime.NuiCamera.ElevationAngle = 0;

            //Uncomment this line to try out speech recognition
            //StartSpeechRecognition();
        }

       /* private void StartSpeechRecognition()
        {
            var t = new Thread(new ThreadStart(RecognizeAudio));
            t.SetApartmentState(ApartmentState.MTA);
            t.Start();
        }

        private void RecognizeAudio()
        {
            using (var source = new KinectAudioSource())
            {
                source.FeatureMode = true;
                source.AutomaticGainControl = false; //Important to turn this off for speech recognition
                source.SystemMode = SystemMode.OptibeamArrayOnly; //No AEC for this sample
                source.MicArrayMode = MicArrayMode.MicArrayAdaptiveBeam;
                
                RecognizerInfo ri = SpeechRecognitionEngine.InstalledRecognizers().Where(r => r.Id == RecognizerId).FirstOrDefault();

                if (ri == null)
                {
                    Console.WriteLine("Could not find speech recognizer: {0}. Please refer to the sample requirements.", RecognizerId);
                    return;
                }

                Console.WriteLine("Using: {0}", ri.Name);

                using (var sre = new SpeechRecognitionEngine(ri.Id))
                {
                    var phrases = new Choices();
                    phrases.Add("computer show window");
                    phrases.Add("computer hide window");
                    phrases.Add("computer show circles");
                    phrases.Add("computer hide circles");

                    var gb = new GrammarBuilder();
                    //Specify the culture to match the recognizer in case we are running in a different culture.                                 
                    gb.Culture = ri.Culture;
                    gb.Append(phrases);

                    // Create the actual Grammar instance, and then load it into the speech recognizer.
                    var g = new Grammar(gb);

                    sre.LoadGrammar(g);
                    sre.SpeechRecognized += SreSpeechRecognized;
                    sre.SpeechHypothesized += SreSpeechHypothesized;
                    sre.SpeechRecognitionRejected += SreSpeechRecognitionRejected;

                    using (Stream s = source.Start())
                    {
                        sre.SetInputToAudioStream(s,
                                                  new SpeechAudioFormatInfo(
                                                      EncodingFormat.Pcm, 16000, 16, 1,
                                                      32000, 2, null));

                        //sre.RecognizeAsync(RecognizeMode.Multiple);
                        while (shouldContinue)
                        {
                            sre.Recognize();
                        }
                        //sre.RecognizeAsyncStop();
                    }
                }
            }
        }

        void SreSpeechRecognitionRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            Trace.WriteLine("\nSpeech Rejected, confidence: " + e.Result.Confidence);
        }

        void SreSpeechHypothesized(object sender, SpeechHypothesizedEventArgs e)
        {
            Trace.Write("\rSpeech Hypothesized: \t{0}", e.Result.Text);
        }

        void SreSpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            //This first release of the Kinect language pack doesn't have a reliable confidence model, so 
            //we don't use e.Result.Confidence here.
            if (e.Result.Confidence < 0.90)
            {
                Trace.WriteLine("\nSpeech Rejected filtered, confidence: " + e.Result.Confidence);
                return;
            }

            Trace.WriteLine("\nSpeech Recognized, confidence: " + e.Result.Confidence + ": \t{0}", e.Result.Text);
            
            if (e.Result.Text == "computer show window")
            {
                this.Dispatcher.BeginInvoke((Action)delegate
                    {
                        this.Topmost = true;
                        this.WindowState = System.Windows.WindowState.Normal;
                    });
            }
            else if (e.Result.Text == "computer hide window")
            {
                this.Dispatcher.BeginInvoke((Action)delegate
                {
                    this.Topmost = false;
                    this.WindowState = System.Windows.WindowState.Minimized;
                });
            }
            else if (e.Result.Text == "computer hide circles")
            {
                this.Dispatcher.BeginInvoke((Action)delegate
                {
                    this.HideCircles();
                });
            }
            else if (e.Result.Text == "computer show circles")
            {
                this.Dispatcher.BeginInvoke((Action)delegate
                {
                    this.ShowCircles();
                });
            }
        }*/

    }
}
