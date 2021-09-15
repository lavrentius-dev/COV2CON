using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;


namespace COV2CON
{
    public partial class MainWindow : Window
    {
        // 
        // Works with gradient-related extensions, see at bottom
        //
        public static GradientStopCollection[] CreateGSC()
        {
            GradientStopCollection[] gscArr = new GradientStopCollection[4];

            // LightBlue-White-LightOrange gradient
            GradientStopCollection grsc = new GradientStopCollection();
            GradientStop gs = new GradientStop()
            {
                Color = Color.FromArgb(255, 242, 133, 0),
                Offset = 1
            };
            grsc.Add(gs);
            gs = new GradientStop()
            {
                Color = Color.FromArgb(255, 221, 204, 204),
                Offset = 0.5
            };
            grsc.Add(gs);
            gs = new GradientStop()
            {
                Color = Color.FromArgb(255, 34, 151, 204),
                Offset = 0
            };
            grsc.Add(gs);
            gscArr[0] = grsc;

            // Rainbow gradient
            grsc = new GradientStopCollection();
            gs = new GradientStop();
            gs.Color = Colors.Navy;
            gs.Offset = 0;
            grsc.Add(gs);
            gs = new GradientStop();
            gs.Color = Colors.Cyan;
            gs.Offset = 0.2;
            grsc.Add(gs);
            gs = new GradientStop();
            gs.Color = Colors.LimeGreen;
            gs.Offset = 0.4;
            grsc.Add(gs);
            gs = new GradientStop();
            gs.Color = Colors.Yellow;
            gs.Offset = 0.6;
            grsc.Add(gs);
            gs = new GradientStop();
            gs.Color = Colors.OrangeRed;
            gs.Offset = 0.8;
            grsc.Add(gs);
            gs = new GradientStop();
            gs.Color = Colors.Red;
            gs.Offset = 1;
            grsc.Add(gs);
            gscArr[1] = grsc;

            // Black-Orange-Red gradient
            grsc = new GradientStopCollection();
            gs = new GradientStop();
            gs.Color = Colors.Black;
            gs.Offset = 0;
            grsc.Add(gs);
            gs = new GradientStop();
            gs.Color = Colors.OrangeRed;
            gs.Offset = 0.5;
            grsc.Add(gs);
            gs = new GradientStop();
            gs.Color = Colors.Yellow;
            gs.Offset = 1;
            grsc.Add(gs);
            gscArr[2] = grsc;

            // Grayscale gradient
            grsc = new GradientStopCollection();
            gs = new GradientStop();
            gs.Color = Colors.Black;
            gs.Offset = 0;
            grsc.Add(gs);
            gs = new GradientStop();
            gs.Color = Colors.White;
            gs.Offset = 1;
            grsc.Add(gs);
            gscArr[3] = grsc;

            return gscArr;
        }


        // Test animation
        public void Flicker(UIElement item)
        {
            BounceEase easing = new BounceEase()  // or whatever easing class you want
            {
                EasingMode = EasingMode.EaseOut
            };
            DoubleAnimation opacityAnim = new DoubleAnimation()

            {
                From = 1,
                To = 0,
                Duration = new Duration(TimeSpan.FromSeconds(0.4)),
                EasingFunction = easing,
                AutoReverse = true
            };
            item.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
        }


        //
        // DateTime-related utilities
        //

        public bool IsWorkDay(DateTime date)
        {
            if (date == null)
            {
                Debug.WriteLine("** Got null date in function IsWorkDay");
                return false;
            }

            switch (date.DayOfWeek)
            {
                case DayOfWeek.Sunday:
                    return false;
                default:
                    return true;
            }
        }


        public int GetWorkDayCount(DateTime from, DateTime to)
        {
            var dayDifference = (int)to.Subtract(from).TotalDays;
            return Enumerable
                .Range(1, dayDifference)
                .Select(x => from.AddDays(x))
                .Count(x => x.DayOfWeek != DayOfWeek.Saturday && x.DayOfWeek != DayOfWeek.Sunday);
        }


        public IEnumerable<DateTime> EachDay(DateTime from, DateTime thru)
        {
            for (var day = from.Date; day.Date <= thru.Date; day = day.AddDays(1))
                yield return day;
        }


        public IEnumerable<DateTime> EachWorkDay(DateTime from, DateTime thru)
        {
            for (var day = from.Date; day.Date <= thru.Date; day = day.AddDays(1))
            {
                if (IsWorkDay(day))
                {
                    yield return day;
                }
            }
        }

        // Always uses Monday-to-Sunday weeks
        public static DateTime GetStartOfWeek(DateTime input)
        {
            // Using +6 here leaves Monday as 0, Tuesday as 1 etc.
            int dayOfWeek = (((int)input.DayOfWeek) + 6) % 7;
            return input.Date.AddDays(-dayOfWeek);
        }


        public static int GetWeeks(DateTime start, DateTime end)
        {
            start = GetStartOfWeek(start);
            end = GetStartOfWeek(end);
            int days = (int)(end - start).TotalDays;
            return (days / 7) + 1; // Adding 1 to be inclusive
        }
    }


    // 
    // Gradient-related extensions
    // http://stackoverflow.com/questions/9650049/get-color-in-specific-location-on-gradient
    //
    public static class GradientStopCollectionExtensions
    {
        public static Color GetRelativeColor(this GradientStopCollection gsc, double offset)
        {
            GradientStop before = gsc.Where(w => w.Offset == gsc.Min(m => m.Offset)).First();
            GradientStop after = gsc.Where(w => w.Offset == gsc.Max(m => m.Offset)).First();

            foreach (var gs in gsc)
            {
                if (gs.Offset < offset && gs.Offset > before.Offset)
                {
                    before = gs;
                }
                if (gs.Offset > offset && gs.Offset < after.Offset)
                {
                    after = gs;
                }
            }

            var color = new Color();

            color.ScA = (float)((offset - before.Offset) * (after.Color.ScA - before.Color.ScA) / (after.Offset - before.Offset) + before.Color.ScA);
            color.ScR = (float)((offset - before.Offset) * (after.Color.ScR - before.Color.ScR) / (after.Offset - before.Offset) + before.Color.ScR);
            color.ScG = (float)((offset - before.Offset) * (after.Color.ScG - before.Color.ScG) / (after.Offset - before.Offset) + before.Color.ScG);
            color.ScB = (float)((offset - before.Offset) * (after.Color.ScB - before.Color.ScB) / (after.Offset - before.Offset) + before.Color.ScB);

            return color;
        }
    }


    public partial class TextBoxWithReturn : TextBox
    {
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Return)
            {
                this.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
        }
    }
}
