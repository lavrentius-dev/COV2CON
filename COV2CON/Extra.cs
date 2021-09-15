using System;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;


namespace COV2CON
{
    public partial class MainWindow : Window
    {
        // Generate random Brush
        private Brush PickBrush()
        {
            Brush result = Brushes.Transparent;

            Random rnd = new Random();
            Type brushesType = typeof(Brushes);
            PropertyInfo[] properties = brushesType.GetProperties();
            int random = rnd.Next(properties.Length);
            result = (Brush)properties[random].GetValue(null, null);

            return result;
        }


        private void Window0_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            ZoomViewBox((e.Delta > 0) ? 50 : -50);
        }


        private void ZoomViewBox(int newValue)
        {
            if ((ViewBoxWorldMap .Width >= 150) && ViewBoxWorldMap.Height >= 150)
            {
                ViewBoxWorldMap.Width += newValue;
                ViewBoxWorldMap.Height += newValue;
            }
        }


        private void Window0_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                //PanViewBox(deltaX, deltaY)
            }
        }


        private void PanViewBox(double deltaX, double deltaY)
        {
            if ((ViewBoxWorldMap.Width >= 100) && ViewBoxWorldMap.Height >= 100)
            {

            }
        }
    }
}