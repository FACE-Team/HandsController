using FACELibrary;
using System;
using System.Threading;
using System.Windows;
using System.Timers;
using System.Configuration;
using System.Text;
using YarpManagerCS;
using System.Diagnostics;
using Leap;
using System.Windows.Media;
using System.Windows.Controls;
namespace LeapFrameReceiver
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    [Serializable]
    public class LRValues
    {
        private long time;
        private float[] rightValues;
        private float[] leftValues;
        public long Time
        {
            set { time = value; }
            get { return time; }
        }
        public float[] RightValues
        {
            set { rightValues = value; }
            get { return rightValues; }
        }
        public float[] LeftValues
        {
            set { leftValues = value; }
            get { return leftValues; }
        }
        public LRValues() { }
        public LRValues(long t, float[] r, float[] l)
        {
            time = t;
            rightValues = r;
            leftValues = l;
        }
    }


    public partial class MainWindow : Window
    {
        private YarpPort yarpPortReceive, yarpPortRx, yarpPortLx;
        private YarpPort yarpPortSend;

        private volatile bool receive = false;

        private LRValues handsValues = new LRValues();

        private Controller controller;
        private Leap.Frame frame;
        private long frameCounter = 0;
        private long LHSent = 0;
        private long RHSent = 0;

        private EventWaitHandle _wh = new AutoResetEvent(false);
        private System.Threading.Thread _worker;

        private float DEG_TO_RAD = 57.2957795131F;

        private Finger.FingerType[] ftype = { Finger.FingerType.TYPE_THUMB, Finger.FingerType.TYPE_INDEX, Finger.FingerType.TYPE_MIDDLE, Finger.FingerType.TYPE_RING, Finger.FingerType.TYPE_PINKY };
        private Bone.BoneType[] btype = { Bone.BoneType.TYPE_METACARPAL, Bone.BoneType.TYPE_PROXIMAL, Bone.BoneType.TYPE_INTERMEDIATE, Bone.BoneType.TYPE_DISTAL };

        private System.Timers.Timer checkStatus;

        public MainWindow()
        {
            var dllDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            Environment.SetEnvironmentVariable("PATH", Environment.GetEnvironmentVariable("PATH") + ";" + dllDirectory);

            InitializeComponent();

            //An instance of leap controller must exist to use frame class
            this.controller = new Controller();
            
            Controller.PolicyFlag current = this.controller.PolicyFlags;
            Controller.PolicyFlag augmented = (Controller.PolicyFlag)((int)current & (1 << 23));
            this.controller.SetPolicy(augmented);

            this.frame = new Leap.Frame();

            InitYarp();

            _worker = new System.Threading.Thread(Work);
            _worker.IsBackground = true;
            _worker.Start();
        }

        void CheckYarp(object source, ElapsedEventArgs e)
        {
            #region check yarp server
            if (yarpPortReceive != null && yarpPortReceive.NetworkExists())
            {
                this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(delegate()
                    {
                        if (YarpServerStatus.Fill == Brushes.Red)
                            YarpServerStatus.Fill = Brushes.Green;
                    }));
            }
            else if (yarpPortReceive != null && !yarpPortReceive.NetworkExists())
            {
                this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(delegate()
                    {
                        if (YarpServerStatus.Fill == Brushes.Green)
                            YarpServerStatus.Fill = Brushes.Red;
                    }));
            }
            #endregion

            #region check leap frame sender
            if (yarpPortReceive != null && yarpPortReceive.PortExists(ConfigurationManager.AppSettings["LeapSender"]))
            {
                this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(delegate()
                    {
                        if (FrameSenderStatus.Fill == Brushes.Red)
                            FrameSenderStatus.Fill = Brushes.Green;
                    }));
            }
            else if (yarpPortReceive != null && !yarpPortReceive.PortExists(ConfigurationManager.AppSettings["LeapSender"]))
            {
                this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(delegate()
                    {
                        if (FrameSenderStatus.Fill == Brushes.Green)
                            FrameSenderStatus.Fill = Brushes.Red;
                    }));
            }
            #endregion
        }

        private void InitYarp()
        {
            yarpPortReceive = new YarpPort();
            yarpPortReceive.openReceiver(ConfigurationManager.AppSettings["LeapSender"].ToString(), ConfigurationManager.AppSettings["LeapReceiver"].ToString());

            yarpPortSend = new YarpPort();
            yarpPortSend.openSender(ConfigurationManager.AppSettings["HandsSender"].ToString());

            checkStatus = new System.Timers.Timer();
            checkStatus.Elapsed += new ElapsedEventHandler(CheckYarp);
            checkStatus.Interval = 5000;
            checkStatus.Start();

        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            if (receive)
            {
                receive = false;
                button1.Content = "Start receive";
            }
            else
            {
                receive = true;
                button1.Content = "Stop receive";
                frameCounter = 0;
                LHSent = 0;
                RHSent = 0;
            }

        }
     
        private void ProcessData(Object data)
        {
            string receivedData = (string)data;
            byte[] serialiedFrame = Convert.FromBase64String(receivedData);
            frame.Deserialize(serialiedFrame);
            // frame contains at least one hand 
            bool right = false;
            bool left = false;
            handsValues.Time = frame.Timestamp;
            handsValues.LeftValues = new float[9] { -1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f };
            handsValues.RightValues = new float[9] { -1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f };
            foreach (Leap.Hand hand in frame.Hands)
            {
                if (hand.IsValid)
                {
                    float[] anglesArray = new float[9];
                    for (int finger = 0; finger < ftype.Length; finger++)
                    {
                        Leap.Matrix[] basis = new Leap.Matrix[3];
                        Leap.Vector[] positions = new Leap.Vector[4];

                        for (int bone = 0; bone < btype.Length; bone++)
                        {
                            if (bone < 3)
                                basis[bone] = hand.Fingers.FingerType(ftype[finger])[0].Bone(btype[bone]).Basis.RigidInverse();
                            positions[bone] = hand.Fingers.FingerType(ftype[finger])[0].Bone(btype[bone]).NextJoint;
                        }

                        // angleFlex sums 3 flexion angles per finger (metacarpal to proximal - proximal to intermediate - intermediate to distal)
                        float angleFlex = 0; 
                        for (int joint = 0; joint < 3; joint++)
                        {
                            angleFlex -= basis[joint].TransformPoint(positions[joint + 1] - positions[joint]).Pitch;
                        }
                        angleFlex = angleFlex * DEG_TO_RAD;
                        anglesArray[finger] = angleFlex;

                        if (ftype[finger] == Finger.FingerType.TYPE_INDEX || ftype[finger] == Finger.FingerType.TYPE_MIDDLE || ftype[finger] == Finger.FingerType.TYPE_THUMB)
                        {
                            float angleAbd = basis[0].TransformPoint(positions[1] - positions[0]).Yaw * DEG_TO_RAD;
                            anglesArray[finger + 5] = angleAbd;
                        }

                    }

                    anglesArray[8] = -1; // wrist angle

                    float[] vettoreAngoliNorm = normalize(anglesArray);

                    #region leftHand
                    if (hand.IsLeft && !left)
                    {
                        handsValues.LeftValues = vettoreAngoliNorm;
                        left = true;

                        for (int i = 1; i < 10; i++)
                        {
                            string labelName = String.Format("lblLH{0:D1}", i);
                            this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background,
                                new Action(() => ((Label)this.FindName(labelName)).Content = String.Format("{0:F2}", anglesArray[i - 1])));

                            string sliderName = String.Format("sldLH{0:D1}", i);
                            this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background,
                               new Action(() => ((Slider)this.FindName(sliderName)).Value = vettoreAngoliNorm[i - 1]));
                        }

                        LHSent += 1;
                        this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background,
                                new Action(() => lblLHSent.Content = LHSent));
                    }
                    #endregion

                    #region rightHand
                    if (hand.IsRight && !right)
                    {
                        handsValues.RightValues = vettoreAngoliNorm;
                        right = true;

                        for (int i = 1; i < 10; i++)
                        {
                            string labelName = String.Format("lblRH{0:D1}", i);
                            this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background,
                                new Action(() => ((Label)this.FindName(labelName)).Content = String.Format("{0:F2}", anglesArray[i - 1])));

                            string sliderName = String.Format("sldRH{0:D1}", i);
                            this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background,
                               new Action(() => ((Slider)this.FindName(sliderName)).Value = vettoreAngoliNorm[i - 1]));
                        }

                        RHSent += 1;
                        this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background,
                                new Action(() => lblRHSent.Content = RHSent));
                    }
                    #endregion

                    #region send to yarp
                    string toSend = ComUtils.XmlUtils.Serialize<LRValues>(handsValues) + " ";
                    yarpPortSend.sendData(toSend);
                    #endregion
                }
            }

        }

        private void Work()
        {
            while (true)
            {
                if (receive)
                {
                    string data = "";
                    yarpPortReceive.receivedData(out data);
                    data = data.Substring(1, data.Length - 2); // to remove quotes at beginning and end from received string
                    if (data != null && data != "")
                    {
                        frameCounter += 1;
                        this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background,
                                            new Action(() => lblFrameReceived.Content = frameCounter));
                        
                        ThreadPool.QueueUserWorkItem(ProcessData, data);
                    }
                }
            }
        }

        // normalize flexion and abduction angles between 0 and 1
        private float[] normalize(float[] array)
        {
            
            float[] arrayMod = new float[9];
            array.CopyTo(arrayMod, 0);

            arrayMod[0] = (arrayMod[0] + 10) / 110;

            for (int i = 1; i < 5; i++)
                arrayMod[i] = (arrayMod[i] - 10) / 170;

            arrayMod[5] = (arrayMod[5] + 20) / 20;
            arrayMod[6] = (arrayMod[6] + 10) / 20;
            arrayMod[7] = (arrayMod[7] + 5) / 15;
            for (int i = 0; i < 8; i++)
            {
                if (arrayMod[i] < 0) arrayMod[i] = 0;
                if (arrayMod[i] > 1) arrayMod[i] = 1;
            }
            return arrayMod;
        }

        public void EnqueueTask(string task)
        {
            _wh.Set();
        }

    }
}
