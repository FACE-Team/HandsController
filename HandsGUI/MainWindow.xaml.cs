using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Timers;
using YarpManagerCS;
using FACELibrary;
using System.Configuration;
using System.Threading;
using System.IO.Ports;
using System.Reflection;
using System.Diagnostics;
using System.IO;

namespace HandsControllerGui
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
        private YarpPort yarpPortHands, yarpPortPosture, yarpPortGesture;

        private System.Timers.Timer checkStatus;

        private Stopwatch gestureTimer;
        
        int timeDelta = 100; //time span (in ms) between two consecutive gesture step (for both record and play)

        
        private System.Threading.Thread threadReceiveHands, threadReceivePosture, threadReceiveGesture, threadSerial, threadRecGesture;

        private bool writeSerialLx = false;
        private bool writeSerialRx = false;

        private bool leapControl = false;
        private bool manualControl = true;
        private bool iclipsControl = false;

        private bool closingWindow = false;

        private bool rec = false; // flag for rec thread
        private bool toSerial = false; // flag for serial communication thread 
        private volatile bool handsAvailable = true; // set to false during gesture play

        
        private string gestureString; // gestureString used during gesture record
        private string gestureName; //

        private volatile float[] rightValues = new float[] { .5f, .5f, .5f, .5f, .5f, .5f, .5f, .5f, .5f }; //current state of sliders - hands
        private volatile float[] leftValues = new float[] { .5f, .5f, .5f, .5f, .5f, .5f, .5f, .5f, .5f };

        private SerialPort serialPortLx, serialPortRx;

        private string serialNameRx, serialNameLx; // serial ports names



        private string gestureDir = ConfigurationManager.AppSettings["gestureDir"];
        private string postureDir = ConfigurationManager.AppSettings["postureDir"];

        private EventWaitHandle _wh = new AutoResetEvent(false);
        
        public MainWindow()
        {
            InitializeComponent();

            InitYarp();
            
            RefreshSerialList(this, new RoutedEventArgs());

            #region threads init
            threadReceiveHands = new System.Threading.Thread(ReceiveHands);
            threadReceiveHands.IsBackground = true;
            threadReceiveHands.Start();

            threadSerial = new System.Threading.Thread(WriteToSerial);
            threadSerial.IsBackground = true;
            threadSerial.Start();

            threadReceivePosture = new System.Threading.Thread(ReceivePosture);
            threadReceivePosture.IsBackground = true;
            threadReceivePosture.Start();

            threadReceiveGesture = new System.Threading.Thread(ReceiveGesture);
            threadReceiveGesture.IsBackground = true;
            threadReceiveGesture.Start();

            threadRecGesture = new System.Threading.Thread(RecGesture);
            threadRecGesture.IsBackground = true;
            threadRecGesture.Start();
            #endregion

            gestureTimer = new Stopwatch();

            Directory.CreateDirectory(ConfigurationManager.AppSettings["postureDir"]);
            Directory.CreateDirectory(ConfigurationManager.AppSettings["gestureDir"]);

        }


        private void InitYarp()
        {
            yarpPortHands = new YarpPort();
            yarpPortHands.openReceiver(ConfigurationManager.AppSettings["HandsSender"].ToString(), ConfigurationManager.AppSettings["HandsReceiver"]);

            yarpPortPosture = new YarpPort();
            yarpPortPosture.openReceiver(ConfigurationManager.AppSettings["PostureSender"].ToString(), ConfigurationManager.AppSettings["PostureReceiver"]);

            yarpPortGesture = new YarpPort();
            yarpPortGesture.openReceiver(ConfigurationManager.AppSettings["GestureSender"].ToString(), ConfigurationManager.AppSettings["GestureReceiver"]);

            checkStatus = new System.Timers.Timer();
            checkStatus.Elapsed += new ElapsedEventHandler(CheckYarp);
            checkStatus.Interval = 5000;
            checkStatus.Start();

        }

        void CheckYarp(object source, ElapsedEventArgs e)
        {
            #region check yarp server
            if (yarpPortHands != null && yarpPortHands.NetworkExists())
            {
                this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(delegate()
                    {
                        if (YarpServerStatus.Fill == Brushes.Red)
                            YarpServerStatus.Fill = Brushes.Green;
                    }));
            }
            else if (yarpPortHands != null && !yarpPortHands.NetworkExists())
            {
                this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(delegate()
                    {
                        if (YarpServerStatus.Fill == Brushes.Green)
                            YarpServerStatus.Fill = Brushes.Red;
                    }));
            }
            #endregion

            #region check hands sender
            if (yarpPortHands != null && yarpPortHands.PortExists(ConfigurationManager.AppSettings["HandsSender"]))
            {
                this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(delegate()
                    {
                        if (HandsStatus.Fill == Brushes.Red)
                            HandsStatus.Fill = Brushes.Green;
                    }));
            }
            else if (yarpPortHands != null && !yarpPortHands.PortExists(ConfigurationManager.AppSettings["HandsSender"]))
            {
                this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(delegate()
                    {
                        if (HandsStatus.Fill == Brushes.Green)
                            HandsStatus.Fill = Brushes.Red;
                    }));
            }
            #endregion

            #region check posture
            if (yarpPortPosture != null && yarpPortPosture.PortExists(ConfigurationManager.AppSettings["PostureSender"]))
            {
                this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(delegate()
                    {
                        if (PostureStatus.Fill == Brushes.Red)
                            PostureStatus.Fill = Brushes.Green;
                    }));
            }
            else if (yarpPortPosture != null && !yarpPortPosture.PortExists(ConfigurationManager.AppSettings["PostureSender"]))
            {
                this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(delegate()
                    {
                        if (PostureStatus.Fill == Brushes.Green)
                            PostureStatus.Fill = Brushes.Red;
                    }));
            }
            #endregion

            #region check gesture
            if (yarpPortGesture != null && yarpPortGesture.PortExists(ConfigurationManager.AppSettings["GestureSender"]))
            {
                this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(delegate()
                    {
                        if (GestureStatus.Fill == Brushes.Red)
                            GestureStatus.Fill = Brushes.Green;
                    }));
            }
            else if (yarpPortGesture != null && !yarpPortGesture.PortExists(ConfigurationManager.AppSettings["GestureSender"]))
            {
                this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(delegate()
                    {
                        if (GestureStatus.Fill == Brushes.Green)
                            GestureStatus.Fill = Brushes.Red;
                    }));
            }
            #endregion

        }


        private void SerialCommunication(object sender, RoutedEventArgs e)
        {

            if (toSerial)
            {
                toSerial = false;
                writeSerialLx = false;
                writeSerialRx = false;

                btnSerial.Content = "Start serial";
                cbLeft.IsEnabled = true;
                cbRight.IsEnabled = true;

                bool flagClose = false;
                if (serialPortRx != null && serialPortRx.IsOpen)
                {
                    serialPortRx.Close();
                    serialPortRx.Dispose();
                    flagClose = true;
                }

                if (serialPortLx != null && serialPortLx.IsOpen)
                {
                    serialPortLx.Close();
                    serialPortLx.Dispose();
                    flagClose = true;
                }
                if (flagClose)
                    System.Threading.Thread.Sleep(1000);
            }

            else
            {
                bool flagOpen = false;
                if (this.serialNameRx != null)
                {
                    serialPortRx = new SerialPort(this.serialNameRx, 9600, Parity.None, 8, StopBits.One);
                    serialPortRx.Open();
                    flagOpen = true;
                    writeSerialRx = true;
                }

                if (this.serialNameLx != null)
                {
                    serialPortLx = new SerialPort(this.serialNameLx, 9600, Parity.None, 8, StopBits.One);
                    serialPortLx.Open();
                    flagOpen = true;
                    writeSerialLx = true;
                }

                if (flagOpen)
                    System.Threading.Thread.Sleep(1000);

                toSerial = true;
                btnSerial.Content = "Stop serial";
                cbLeft.IsEnabled = false;
                cbRight.IsEnabled = false;
            }
        }

        private void ReceiveHands()
        {
            while (true)
            {
                if (leapControl)
                {
                    string data = "";
                    yarpPortHands.receivedData(out data);

                    if (data.Length > 2)
                        data = data.Substring(1, data.Length - 2); // remove first and last char from data, yarp puts the message in quotes ""

                    if (leapControl && data != null && data != "")
                    {
                        LRValues handsConfiguration = ComUtils.XmlUtils.Deserialize<LRValues>(data);
                        setHands(handsConfiguration);
                    }

                }
                else
                {
                    System.Threading.Thread.Sleep(500);
                }
            }
        }

        private void ReceivePosture()
        {
            while (true)
            {
                if (iclipsControl)
                {
                    string data = "";
                    yarpPortPosture.receivedData(out data);
                    Console.WriteLine(data);
                    if (data.Length > 2)
                        data = data.Substring(1, data.Length - 2); // remove first and last char from data, yarp puts the message in quotes ""

                    if (iclipsControl && data != null && data != "") //
                    {
                        if (File.Exists(System.IO.Path.Combine(postureDir, data + ".xml")))
                        {
                            if (handsAvailable)
                            {
                                string handsConfXML = File.ReadAllText(System.IO.Path.Combine(postureDir, data + ".xml"), System.Text.Encoding.UTF8);
                                LRValues posture = ComUtils.XmlUtils.Deserialize<LRValues>(handsConfXML);

                                handsAvailable = false;
                                setHands(posture);
                                handsAvailable = true;
                            }
                        }
                    }
                }
                else
                {
                    System.Threading.Thread.Sleep(500);
                }
            }
        }

        private void ReceiveGesture()
        {
            while (true)
            {
                if (iclipsControl)
                {
                    string data = "";
                    yarpPortGesture.receivedData(out data);
                    Console.WriteLine(data);
                    if (data.Length > 2)
                        data = data.Substring(1, data.Length - 2); // remove first and last char from data, yarp puts the message in quotes ""

                    if (iclipsControl && data != null && data != "")
                    {
                        Console.WriteLine(data);
                        Console.WriteLine("control");
                        if (File.Exists(System.IO.Path.Combine(gestureDir, data + ".xml")))
                        {
                            Console.WriteLine("trovato file");
                            if (handsAvailable)
                            {
                                
                                string readText = File.ReadAllText(System.IO.Path.Combine(gestureDir, data + ".xml"), System.Text.Encoding.UTF8);
                                string[] lines = readText.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);

                                int line = 0;
                                handsAvailable = false;
                                while (line < lines.Length - 3)
                                {
                                    Console.WriteLine("linea");
                                    LRValues gestFrame = ComUtils.XmlUtils.Deserialize<LRValues>(lines[line]);
                                    setHands(gestFrame);
                                    System.Threading.Thread.Sleep(timeDelta);
                                    line += 1;
                                }
                                handsAvailable = true;
                                Console.WriteLine("finito");
                            }
                        }
                    }
                }
                else
                {
                    System.Threading.Thread.Sleep(500);
                }
            }
        }



        private void setHands(LRValues handsConf)
        {
            for (int i = 0; i < 9; i++)
            {
                float leftValue = handsConf.LeftValues[i];
                float rightValue = handsConf.RightValues[i];

                if (leftValue >= 0 && leftValue <= 1)
                {
                    this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background,
                        new Action(() => ((Slider)this.FindName(String.Format("sldLH{0}", i + 1))).Value = leftValue));
                }

                if (rightValue >= 0 && rightValue <= 1)
                {
                    this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background,
                                              new Action(() => ((Slider)this.FindName(String.Format("sldRH{0}", i + 1))).Value = rightValue));
                }
            }
        }

        private LRValues getHands()
        {
            LRValues ret;
            lock (rightValues)
            {
                lock (leftValues)
                {
                    ret = new LRValues(0, rightValues, leftValues);
                }
            }
            return ret;
        }

        private string getHandsXML()
        {
            LRValues handsConf = getHands();
            string handsConfXML = ComUtils.XmlUtils.Serialize<LRValues>(handsConf);
            return handsConfXML;
        }


        private void SavePosture(object sender, RoutedEventArgs e)
        {
            string handsConfXML = getHandsXML() + " ";

            var dialog = new FileNameDialog(0);
            if (dialog.ShowDialog() == true)
            {
                string postureName = dialog.ResponseText;

                File.WriteAllText(System.IO.Path.Combine(postureDir, postureName + ".xml"), handsConfXML, System.Text.Encoding.UTF8);
            }
        }

        private void LoadPosture(object sender, RoutedEventArgs e)
        {
            var dialog = new FileListDialog(0);
            if (dialog.ShowDialog() == true)
            {
                string postureName = dialog.ResponseText;
                string handsConfXML = File.ReadAllText(System.IO.Path.Combine(postureDir, postureName + ".xml"), System.Text.Encoding.UTF8);
                LRValues posture = ComUtils.XmlUtils.Deserialize<LRValues>(handsConfXML);
                setHands(posture);
            }

        }

        private void SaveGesture(object sender, RoutedEventArgs e)
        {
            if (rec)
            {
                rec = false;
                btnRecGesture.Content = "Save Gesture";
                File.WriteAllText(System.IO.Path.Combine(gestureDir, gestureName + ".xml"), gestureString, System.Text.Encoding.UTF8);
            }
            else
            {
                var dialog = new FileNameDialog(1);
                if (dialog.ShowDialog() == true)
                {
                    gestureName = dialog.ResponseText;
                    btnRecGesture.Content = "Stop Record";
                    gestureString = "";
                    rec = true;
                }
            }
        }

        private void LoadGesture(object sender, RoutedEventArgs e)
        {
            var dialog = new FileListDialog(1);
            if (dialog.ShowDialog() == true)
            {
                spControl.IsEnabled = false;
                string gestureName = dialog.ResponseText;
                string readText = File.ReadAllText(System.IO.Path.Combine(gestureDir, gestureName + ".xml"), System.Text.Encoding.UTF8);
                string[] lines = readText.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);

                int line = 0;

                while (line < lines.Length - 3)
                {

                    LRValues gestFrame = ComUtils.XmlUtils.Deserialize<LRValues>(lines[line]);
                    setHands(gestFrame);
                    System.Threading.Thread.Sleep(timeDelta);
                    line += 1;
                }
                spControl.IsEnabled = true;
            }
        }

        private void RecGesture()
        {
            while (true)
            {
                if (rec)
                {
                    string gestureFrame = getHandsXML();
                    gestureString = gestureString + gestureFrame + " " + Environment.NewLine;
                    System.Threading.Thread.Sleep(timeDelta);
                }
                else
                    System.Threading.Thread.Sleep(500);
            }
        }


        
        private void WriteToSerial()
        {
            while (true)
            {
                if (toSerial && !closingWindow)
                {
                    #region write to left serial
                    if (writeSerialLx && serialPortLx != null && serialPortLx.IsOpen)
                    {
                        lock (leftValues)
                        {
                            int[] intLxAngles = new int[9];
                            for (int i = 0; i < 8; i++)
                                intLxAngles[i] = (int)(180 - leftValues[i] * 180);  //TODO cambiare versi su firmware e non fare 180 - blabla
                            intLxAngles[8] = (int)(leftValues[8] * 180);

                            for (int motor = 0; motor < 9; motor++)
                            {
                                if (toSerial && !closingWindow)
                                    serialPortLx.Write(new byte[] { (byte)242, (byte)motor, (byte)intLxAngles[motor] }, 0, 3);
                            }
                        }
                    }
                    #endregion

                    #region write to right serial
                    if (writeSerialRx && serialPortRx != null && serialPortRx.IsOpen)
                    {
                        lock (rightValues)
                        {
                            int[] intRxAngles = new int[9];
                            for (int i = 0; i < 8; i++)
                                intRxAngles[i] = (int)(180 - rightValues[i] * 180);  //TODO cambiare versi su firmware e non fare 180 - blabla
                            intRxAngles[8] = (int)(rightValues[8] * 180);

                            for (int motor = 0; motor < 9; motor++)
                                if (toSerial && !closingWindow)
                                    serialPortRx.Write(new byte[] { (byte)242, (byte)motor, (byte)intRxAngles[motor] }, 0, 3);                           
                        }
                    }
                    #endregion
                }
                System.Threading.Thread.Sleep(50);
            }
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            closingWindow = true;
            if (serialPortRx != null)
            {
                serialPortRx.Close();
                serialPortRx.Dispose();
            }
            if (serialPortLx != null)
            {
                serialPortLx.Close();
                serialPortLx.Dispose();
            }
        }

        // update value of serialNameXx 
        private void cbSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // same method called by both combobox, switch on sender.name 
            if ((((ComboBox)sender).SelectedValue) != null)
            {
                switch (((ComboBox)sender).Name)
                {
                    case "cbLeft":
                        this.serialNameLx = ((ComboBox)sender).SelectedValue.ToString();
                        if (this.serialNameLx == this.serialNameRx)
                        {
                            serialNameLx = null;
                            cbLeft.SelectedIndex = -1;
                        }
                        break;

                    case "cbRight":
                        this.serialNameRx = ((ComboBox)sender).SelectedValue.ToString();
                        if (this.serialNameLx == this.serialNameRx)
                        {
                            serialNameRx = null;
                            cbRight.SelectedIndex = -1;
                        }
                        break;

                    default:
                        // 
                        break;
                }
            }
        }


        private void RefreshSerialList(object sender, RoutedEventArgs e)
        {
           
            cbLeft.Items.Clear();
            cbRight.Items.Clear();
            foreach (string p in SerialPort.GetPortNames())
            {
                cbLeft.Items.Add(p);
                cbRight.Items.Add(p);
            }
            
            if (serialNameLx != null)
            {
                cbLeft.SelectedIndex = cbLeft.Items.IndexOf(serialNameLx);
            }
            if (serialNameRx != null)
            {
                cbRight.SelectedIndex = cbRight.Items.IndexOf(serialNameRx);
            }
            
        }

        //control mode selection
        private void ControlRadioButtonChecked(object sender, RoutedEventArgs e)
        {
            switch (((RadioButton)sender).Name)
            {
                case "radbtnLeap":
                    if (spControl != null)
                        spControl.IsEnabled = false;

                    leapControl = true;
                    manualControl = false;
                    iclipsControl = false;
                    break;

                case "radbtnManual":
                    if (spControl != null)
                        spControl.IsEnabled = true;

                    leapControl = false;
                    manualControl = true;
                    iclipsControl = false;
                    break;

                case "radbtnIclips":
                    if (spControl != null)
                        spControl.IsEnabled = false;

                    leapControl = false;
                    manualControl = false;
                    iclipsControl = true;
                    break;

                default:
                    break;
            }
        }

        // update left/rightValues on slider value change
        private void sld_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            float modValue = (float)((Slider)sender).Value;
            string name = ((Slider)sender).Name;
            int number = Convert.ToInt32(name.Substring(5, 1));

            switch (name.Substring(3, 1))
            {
                case "L":
                    leftValues[number - 1] = modValue;
                    break;

                case "R":
                    rightValues[number - 1] = modValue;
                    break;
            }
        }
   
    
    }
}
