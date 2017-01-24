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
using System.Windows.Shapes;

using System.Configuration;
namespace HandsControllerGui
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class FileListDialog : Window
    {
        public FileListDialog(int number)
        {
            
            InitializeComponent();

            string directory;
            switch (number)
            {
                case 0:
                    directory = ConfigurationManager.AppSettings["postureDir"];
                    this.Title = "Load Posture";
                    label.Content = "Posture list";
                    break;
                case 1:
                    directory = ConfigurationManager.AppSettings["gestureDir"];
                    this.Title = "Load Gesture";
                    label.Content = "Gesture list";
                    break;
                default:
                    directory = "";
                    break;

            }

            string[] files = System.IO.Directory.GetFiles(directory, "*.xml");

            foreach (string s in files)
                lbAnimations.Items.Add(System.IO.Path.GetFileNameWithoutExtension(s));

          
        }

        public string ResponseText
        {

            get { return (string)lbAnimations.SelectedItem; }
            //set { ResponseTextBox.Text = value; }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        //private void lbAnimations_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    Console.WriteLine(lbAnimations.SelectedValue);
        //    Console.WriteLine(lbAnimations.SelectedItem);
        //    Console.WriteLine(lbAnimations.SelectedIndex);
        //}
    }
}
