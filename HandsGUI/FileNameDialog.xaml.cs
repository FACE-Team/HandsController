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
    public partial class FileNameDialog : Window
    {
        public FileNameDialog(int number)
        {
            InitializeComponent();
            string labelContent;

            switch (number)
            {
                case 0:
                    labelContent = "Posture name: ";
                    break;
                case 1:
                    labelContent = "Gesture name: ";
                    break;
                default:
                    labelContent = " ??? :";
                    break;
            }


            label.Content = labelContent;
           
        }

        public string ResponseText
        {
            get { return ResponseTextBox.Text; }
            set { ResponseTextBox.Text = value; }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
