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
using System.Threading;
using System.Globalization;
using System.Diagnostics;
using System.Timers;
using System.Runtime.InteropServices;
using ComUtils;
using YarpManagerCS;
using FACELibrary;
using System.Configuration;
using System.Reflection;

using Leap;



namespace LeapFrameSender
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, ILeapEventDelegate
    {
        // ----- Leap ----
        private Controller controller = new Controller();

        private LeapEventListener listener;
        private Boolean isClosing = false;

        private volatile bool send = false;

        // ---------------
        private YarpPort yarpPort;

        private Stopwatch senderTimer;

        private long frameCounter = 0;
        private long skippedFrame = 0;

        private long timeThreshold = 100; //ms

        private System.Timers.Timer checkStatus;

        public MainWindow()
        {
            var dllDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/lib";
            Environment.SetEnvironmentVariable("PATH", Environment.GetEnvironmentVariable("PATH") + ";" + dllDirectory);

            InitializeComponent();

            // ----- Leap ----
            this.controller = new Controller();
            this.controller.SetPolicyFlags(Controller.PolicyFlag.POLICY_BACKGROUND_FRAMES);
            this.listener = new LeapEventListener(this);
            this.controller.AddListener(listener);
            // ----- --------

            InitYarp();

            senderTimer = new Stopwatch();
            senderTimer.Start();



        }

        void CheckYarpLeap(object source, ElapsedEventArgs e)
        {
            #region check yarp server
            if (yarpPort != null && yarpPort.NetworkExists())
            {
                this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(delegate()
                    {
                        if (YarpServerStatus.Fill == Brushes.Red)
                            YarpServerStatus.Fill = Brushes.Green;
                    }));
            }
            else if (yarpPort != null && !yarpPort.NetworkExists())
            {
                this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(delegate()
                    {
                        if (YarpServerStatus.Fill == Brushes.Green)
                            YarpServerStatus.Fill = Brushes.Red;
                    }));
            }
            #endregion

            #region check leap sensor
            if (controller != null && controller.IsConnected)
            {
                this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(delegate()
                    {
                        if (LeapSensorStatus.Fill == Brushes.Red)
                            LeapSensorStatus.Fill = Brushes.Green;
                    }));
            }
            else if (controller != null && !controller.IsConnected)
            {
                this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(delegate()
                    {
                        if (LeapSensorStatus.Fill == Brushes.Green)
                            LeapSensorStatus.Fill = Brushes.Red;
                    }));
            }
            #endregion

            #region check leap sensor focus
            if (controller != null && controller.HasFocus)
            {
                this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(delegate()
                    {
                        if (LeapFocusStatus.Fill == Brushes.Red)
                            LeapFocusStatus.Fill = Brushes.Green;
                    }));
            }
            else if (controller != null && !controller.HasFocus)
            {
                this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(delegate()
                    {
                        if (LeapFocusStatus.Fill == Brushes.Green)
                            LeapFocusStatus.Fill = Brushes.Red;
                    }));
            }
            #endregion

        }

        delegate void LeapEventDelegate(string EventName);

        public void LeapEventNotification(string EventName)
        {
            if (this.CheckAccess())
            {
                switch (EventName)
                {
                    case "onInit":
                        Debug.WriteLine("Init");
                        break;
                    case "onConnect":
                        this.connectHandler();
                        break;
                    case "onFrame":
                        if (!this.isClosing)
                            this.newFrameHandler(this.controller.Frame());
                        break;
                    case "onDisconnect":
                        break;
                }
            }
            else
            {
                Dispatcher.Invoke(new LeapEventDelegate(LeapEventNotification), new object[] { EventName });
            }
        }

        void connectHandler()
        {
            //this.controller.SetPolicy(Controller.PolicyFlag.POLICY_BACKGROUND_FRAMES);
            //this.controller.SetPolicy(Controller.PolicyFlag.POLICYBACKGROUNDFRAMES);
            //Controller.PolicyFlag current = controller.PolicyFlags;
            //Controller.PolicyFlag augmented = (Controller.PolicyFlag)((int)current & (1 << 15)); test with other leap app !!!!!
            //this.controller.SetPolicyFlags(augmented);
            //augmented = (Controller.PolicyFlag)((int)augmented & (1 << 23));                     test with other leap app !!!!!
            //this.controller.SetPolicyFlags(augmented);
            ////this.controller.SetPolicy(Controller.PolicyFlag.POLICY_IMAGES);
            ////this.controller.EnableGesture(Gesture.GestureType.TYPE_SWIPE);
            ////this.controller.Config.SetFloat("Gesture.Swipe.MinLength", 100.0f);
        }

        private class LeapFrameYarpSender
        {
            private Leap.Frame _frame;
            private ManualResetEvent _doneEvent;

            public LeapFrameYarpSender(Leap.Frame frame, ManualResetEvent doneEvent)
            {
                _frame = frame;
                _doneEvent = doneEvent;
            }

            public void ThreadPoolCallback(Object outputYarpPort)
            {
                YarpPort outYarpPort = (YarpPort)outputYarpPort;
                byte[] serializedFrameByte = _frame.Serialize;
                string serialiedFrameString = Convert.ToBase64String(serializedFrameByte);
                outYarpPort.sendData(serialiedFrameString);
                _doneEvent.Set();
            }
        }


        void newFrameHandler(Leap.Frame frameAv)
        {
            if (send)
            {
                long id = -1;
                long lHands = -1;
                long rHands = -1;

                if (senderTimer.ElapsedMilliseconds > timeThreshold)
                {
                    id = frameAv.Id;
                    lHands = 0;
                    rHands = 0;

                    if (frameAv.Hands.Count > 0)
                    {
                        foreach (Leap.Hand hand in frameAv.Hands)
                        {
                            if (hand.IsLeft)
                                lHands += 1;
                            else
                                rHands += 1;
                        }
                        LeapFrameYarpSender frameSender = new LeapFrameYarpSender(frameAv, new ManualResetEvent(false));
                        ThreadPool.QueueUserWorkItem(frameSender.ThreadPoolCallback, yarpPort);

                        frameCounter += 1;
                        // reset timer, start again
                        senderTimer.Reset();
                        senderTimer.Start();
                    }
                    else
                    {
                        skippedFrame += 1;// frame without hands += 1
                    }
                }
                else
                {
                    skippedFrame += 1;
                }

                ThreadPool.QueueUserWorkItem(UpdateLabels, new long[] { id, lHands, rHands });
            }
        }


        private void UpdateLabels(Object values)
        {
            long[] vals = (long[])values;
            if (vals[0] != -1)
            {
                this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background,
                    new Action(() => lblInfo.Content = String.Format("Frame Id = {0} \n  left Hands = {1} \n  right hands = {2}", vals[0], vals[1], vals[2])));

                this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background,
                    new Action(() => lblSentFrame.Content = frameCounter));
            }
            this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => lblSkippedFrame.Content = skippedFrame));
        }


        void MainWindow_Closing(object sender, EventArgs e)
        {
            this.isClosing = true;
            this.controller.RemoveListener(this.listener);
            this.controller.Dispose();
        }

        private void InitYarp()
        {
            yarpPort = new YarpPort();
            yarpPort.openSender(ConfigurationManager.AppSettings["LeapSender"].ToString());

            checkStatus = new System.Timers.Timer();
            checkStatus.Elapsed += new ElapsedEventHandler(CheckYarpLeap);
            checkStatus.Interval = 5000;
            checkStatus.Start();

        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            if (send)
            {
                send = false;
                button1.Content = "Start send";
            }
            else
            {
                send = true;
                button1.Content = "Stop send";
                skippedFrame = 0;
                frameCounter = 0;
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            yarpPort.Close();
        }

        private void Slider_ValueChanged_1(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            timeThreshold = (long)slider.Value;
            if (tbTimeThreshold != null)
            {
                tbTimeThreshold.Text = timeThreshold.ToString();
            }
        }

        private void TextBox_TextChanged_1(object sender, TextChangedEventArgs e)
        {
            try
            {
                long input = Convert.ToInt64(tbTimeThreshold.Text);
                if ((input >= (long)slider.Minimum) && (input <= (long)slider.Maximum))
                {
                    slider.Value = input;
                    tbTimeThreshold.Text = input.ToString();
                    timeThreshold = input;
                }
                else
                {
                    slider.Value = timeThreshold;
                    tbTimeThreshold.Text = timeThreshold.ToString();
                }
            }
            catch (Exception)
            {
                tbTimeThreshold.Text = timeThreshold.ToString();
            }
        }

    }


    public interface ILeapEventDelegate
    {
        void LeapEventNotification(string EventName);
    }

    public class LeapEventListener : Listener
    {
        ILeapEventDelegate eventDelegate;

        public LeapEventListener(ILeapEventDelegate delegateObject)
        {
            this.eventDelegate = delegateObject;
        }
        public override void OnInit(Controller controller)
        {
            this.eventDelegate.LeapEventNotification("onInit");
        }
        public override void OnConnect(Controller controller)
        {
            this.eventDelegate.LeapEventNotification("onConnect");
        }

        public override void OnFrame(Controller controller)
        {
            this.eventDelegate.LeapEventNotification("onFrame");
        }
        public override void OnExit(Controller controller)
        {
            this.eventDelegate.LeapEventNotification("onExit");
        }
        public override void OnDisconnect(Controller controller)
        {
            this.eventDelegate.LeapEventNotification("onDisconnect");
        }
    }
}
