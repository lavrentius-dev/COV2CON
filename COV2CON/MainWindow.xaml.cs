using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Path = System.Windows.Shapes.Path;


namespace COV2CON
{
    public partial class MainWindow : Window
    {
        readonly Dictionary<string, CountryMapInfo> SVGCountryMap = new Dictionary<string, CountryMapInfo>();  // only called once, in Windows_loaded, so we can init here
        List<string> DatasetCountryName;
        List<int> SelectedIdx;
        Dictionary<string, CountryGraphInfo> DatasetCountryGraph;
        Dictionary<string, double> CountryToPopulationDict;
        Brush defaultCountryFillBrush;
        bool WasNetAnomDetected, IsUpdateRunning, WasLocalDataSetFound, IsUpdateRequired, WasCurrentUpdateRead, CanTryManualUpdate, IsMouseOverSliderGraph;
        bool FoundExcessItems = false, KeepPreviousSelection = false, IterativelyUpdatingSelection = false;
        string LastName, filter = "";
        int FontSizeIncrement = 0, GraphNumDays;
        readonly int FontSizeIncrementMax = 2;
        readonly Dictionary<string, string> CountryItemToName = new Dictionary<string, string>();
        double GraphLeft, GraphBottom, GraphWidth, GraphHeight, GraphRatioWH,
            InitSlideGraphLeft, InitSliderGraphWidth, SliderGraphValuePerDay,
            InitGridGraphBackgroundWidth, InitGridGraphBackgroundHeight;
        double[] MaxYVar;
        DateTime MinDate, MaxDate, SliderDate;
        GradientStopCollection gsc = CreateGSC()[2];
        HelpDialogRtf HelpDlgRtf = null;
        readonly string AppDir = AppDomain.CurrentDomain.BaseDirectory;
        readonly string AppDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\COV2CON";
        static readonly List<Brush> TrueColourBrush = new List<Brush>
        {
            (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFf5f5f5")),  // off white
            (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFe5bf1d")),  // titan yellow
            (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFff3333")),  // red-orange
            (SolidColorBrush)(new BrushConverter().ConvertFrom("#FF20ea00")),  // light green
            (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFea00d3")),  // lila
            (SolidColorBrush)(new BrushConverter().ConvertFrom("#FF00b3ea")),  // light cyan
            (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFe67064"))   // light cognac
        };
        int MaxSelectedItems = TrueColourBrush.Count;
        List<string> PreviousSelection, CurrentSelection, TempSelection;


        public MainWindow()
        {
            InitializeComponent();
            SliderGraph.Visibility = Visibility.Hidden;
            LabelSliderGraph.Visibility = Visibility.Hidden;
            IsMouseOverSliderGraph = false;


            // set application font size to last saved value if available
            if ((Properties.Settings.Default.FontSizeIncrement > 0) && (Properties.Settings.Default.FontSizeIncrement <= FontSizeIncrementMax))
            {
                FontSizeIncrement = Properties.Settings.Default.FontSizeIncrement;
                for (int i = 0; i < FontSizeIncrement; i++)
                    this.Window0.FontSize++;
            }

            ListViewCountries.Focus();
        }


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Create map
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = assembly.GetManifestResourceNames().Single(str => str.EndsWith("world.svg"));

                string svgString = "";
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                using (StreamReader reader = new StreamReader(stream))
                    svgString = reader.ReadToEnd();

                InitGridGraphBackgroundWidth = GridGraphBackground.ActualWidth;
                InitGridGraphBackgroundHeight = GridGraphBackground.ActualHeight;

                //
                // Parse SVG string and add paths as Grid1 children
                //

                svgString = Regex.Replace(svgString, @"(\n|\r\n?)", " ");

                MatchCollection Match = Regex.Matches(svgString,
                                    @"<path.*?/>",
                                    RegexOptions.IgnoreCase | RegexOptions.Multiline);
                int cnt = Match.Count;

                Match = Regex.Matches(svgString,
                     @"<path.*?data-name=""([^""]+)"".*?data-id=""([^""]+)"".*?d=""(m\s[^""]+\sz)"".*?style=""fill:(#\w+);fill-rule:evenodd"".*?/>",
                     RegexOptions.IgnoreCase | RegexOptions.Multiline);

                if (Match.Count > 0)
                {
                    if (cnt == Match.Count)
                    {
                        GridWorldMap.Children.Clear();

                        Brush LightBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#686868"));
                        defaultCountryFillBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#a0a4a7"));

                        for (int i = 0; i < Match.Count; i++)
                        {
                            Geometry countryGeometry = Geometry.Parse(Match[i].Groups[3].Value);
                            Path countryPath = new Path
                            {
                                Stroke = LightBrush,
                                Fill = defaultCountryFillBrush,
                                StrokeThickness = 1,
                                Data = countryGeometry,
                                Name = "SVGMapItem" + i.ToString(),
                                ToolTip = Match[i].Groups[1].Value
                            };
                            countryPath.MouseDown += new MouseButtonEventHandler(Country_MouseDown);
                            GridWorldMap.Children.Add(countryPath);

                            // Add CountryMapInfo object to CountryMap dictionary
                            SVGCountryMap.Add(Match[i].Groups[1].Value,
                                        new CountryMapInfo(
                                            i,
                                            Match[i].Groups[1].Value,
                                            countryPath,
                                            defaultCountryFillBrush
                                            )
                                        );

                            //// DEBUG: print (X,Y) pairs for each CountryMap. Note: Spain contains one 'l' near end
                            //DebugParseSVGDataStr(i, Match); 

                            // Resize Grid1 view area by adding a one-point shape/path in the RB corner
                            Point[] pla = new Point[1];
                            pla[0] = new Point(2020, 950);
                            PolyLineSegment pls = new PolyLineSegment
                            {
                                Points = new PointCollection(pla)
                            };
                            PathFigure pfg = new PathFigure();
                            pfg.Segments.Add(pls);
                            PathGeometry pgm = new PathGeometry();
                            pgm.Figures.Add(pfg);
                            Path pth = new Path
                            {
                                Stroke = Brushes.Transparent,
                                Fill = Brushes.Transparent,
                                StrokeThickness = 1
                            };
                            pth.Data = pgm;
                            GridWorldMap.Children.Add(pth);
                        }
                    }
                    else
                    {
                        Debug.WriteLine("** Path vs path field pattern mismatch found");
                    }

                    // Gradient legend
                    LinearGradientBrush aBrush = new LinearGradientBrush(gsc, 90);
                    Rectangle RectangleGradient = new Rectangle()
                    {
                        Stroke = aBrush,
                        Fill = aBrush,
                        Height = 100,
                        Width = 14,
                        Name = "RectangleGradient"
                    };
                    RectangleGradient.HorizontalAlignment = HorizontalAlignment.Left;
                    RectangleGradient.VerticalAlignment = VerticalAlignment.Top;
                    Thickness mrg = new Thickness(20, 840, 0, 0);
                    RectangleGradient.Margin = mrg;
                    GridWorldMap.Children.Add(RectangleGradient);
                    GridWorldMap.RegisterName(RectangleGradient.Name, RectangleGradient);
                    RectangleGradient.Visibility = Visibility.Hidden;
                }
                else
                {
                    Debug.WriteLine("** No path field pattern match found");
                }

                UpdateDataSet();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("** Exception reading SVG data: " + ex.Message);
            }
        }


        private void Window0_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Properties.Settings.Default.Save();

            if (IsUpdateRunning == true)
                if (MessageBox.Show("Update is still running, close application now?", "COV2CON", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                    e.Cancel = true;
        }


        private void Window0_KeyUp(object sender, KeyEventArgs e)
        {
            // Ctrl & +/- : change font size
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if ((e.Key == Key.Add) || (e.Key == Key.OemPlus))
                {
                    if (FontSizeIncrement < FontSizeIncrementMax)
                    {
                        FontSizeIncrement++;
                        this.Window0.FontSize++;
                    }
                }
                else if ((e.Key == Key.Subtract) || (e.Key == Key.OemMinus))
                {
                    if (FontSizeIncrement > 0)
                    {
                        FontSizeIncrement--;
                        this.Window0.FontSize--;
                    }
                }

                // store FontSizeIncrement setting
                Properties.Settings.Default.FontSizeIncrement = FontSizeIncrement;
            }

            // Shift & Alt & X : toggle MaxSelectedItems from 7 to unlimited
            else if ((Keyboard.Modifiers & (ModifierKeys.Shift | ModifierKeys.Alt)) == (ModifierKeys.Shift | ModifierKeys.Alt))
            {
                if (e.SystemKey == Key.X)
                {
                    string msg = MaxSelectedItems == TrueColourBrush.Count ? $"{TrueColourBrush.Count} to 'unlimited'" : $"'unlimited' to {TrueColourBrush.Count}";
                    if (MessageBox.Show($"This will change the selection limit from {msg}. Current selection will be reset. Continue?",
                        "COV2CON",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question) == MessageBoxResult.Yes
                        )
                    {
                        MaxSelectedItems = MaxSelectedItems == TrueColourBrush.Count ? int.MaxValue : TrueColourBrush.Count;
                        if ((ListViewCountries != null) && (ListViewCountries.Items.Count > 0))
                        {
                            ListViewCountries.SelectedItems.Clear();
                            ListViewCountries.SelectedIndex = 0;
                        }

                        if ((bool)RadioButton1.IsChecked)
                            OnRadioButtonChecked(0);
                        else if ((bool)RadioButton2.IsChecked)
                            OnRadioButtonChecked(1);
                        else if ((bool)RadioButton3.IsChecked)
                            OnRadioButtonChecked(2);
                        else
                            Debug.WriteLine($"** Window0_KeyUp | Missing radio button?!");
                    }
                }
            }
        }


        // ref: https://stackoverflow.com/questions/1069577/select-item-programmatically-in-wpf-listview [thread comment #6]
        private void OnRadioButtonChecked(int idx)
        {
            Mouse.OverrideCursor = Cursors.Wait;

            if ((ListViewCountries != null) && (ListViewCountries.Items.Count > 0))
                LastName = CountryItemToName[ListViewCountries.SelectedItem.ToString()];  // the first selection item

            KeepPreviousSelection = true;
            UpdateCountryListAndMap(idx);

            if ((ListViewCountries != null) && (ListViewCountries.SelectedItems.Count > 0))
            {
                ListViewCountries.UpdateLayout();
                //((ListViewItem)ListViewCountries.ItemContainerGenerator.ContainerFromIndex(ListViewCountries.SelectedIndex)).Focus();
                foreach (var item in ListViewCountries.SelectedItems)
                    ((ListViewItem)ListViewCountries.ItemContainerGenerator.ContainerFromItem(item)).Focus();  // this needs disabling virtualization for ListViewCountries, check XML
            }
            KeepPreviousSelection = false;

            Mouse.OverrideCursor = null;
        }


        private void RadioButton1_Checked(object sender, RoutedEventArgs e)
        {
            OnRadioButtonChecked(0);
        }


        private void RadioButton2_Checked(object sender, RoutedEventArgs e)
        {
            OnRadioButtonChecked(1);
        }


        private void RadioButton3_Checked(object sender, RoutedEventArgs e)
        {
            OnRadioButtonChecked(2);
        }


        private void RadioButton4_Checked(object sender, RoutedEventArgs e)
        {
            OnRadioButtonChecked(3);
        }


        private void Window0_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.ActualWidth < 1272)
                this.Width = 1272;

            if (this.ActualHeight < 640)
                this.Height = 640;

            GridExpander.Width = Grid0.ActualWidth - 23;
        }


        private void TextBoxFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            filter = TextBoxFilter.Text;
            if ((ListViewCountries != null) && (ListViewCountries.Items.Count > 0))
                LastName = CountryItemToName[ListViewCountries.SelectedItem.ToString()];

            int idx = -1;
            if ((bool)RadioButton1.IsChecked)
                idx = 0;
            else if ((bool)RadioButton2.IsChecked)
                idx = 1;
            else if ((bool)RadioButton3.IsChecked)
                idx = 2;
            else if ((bool)RadioButton4.IsChecked)
                idx = 3;
            UpdateCountryListAndMap(idx);
        }


        private void TextBoxFilter_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if ((ListViewCountries != null) && (ListViewCountries.Items.Count > 0))
            {
                ListViewCountries.Items.SortDescriptions.Add(new SortDescription("", ListSortDirection.Ascending));
                ListViewCountries.SelectedIndex = 0;
            }
        }


        private void ListViewCountries_KeyUp(object sender, KeyEventArgs e)
        {
            //if ((ListViewCountries != null) && (ListViewCountries.Items.Count > 0))
            //    LastName = CountryItemToName[ListViewCountries.SelectedItem.ToString()];

            //if (SliderGraphValuePerDay > 0)
            //{
            //    if (e.Key == Key.Left)
            //    {
            //        if (SliderGraph.Value <= SliderGraph.Maximum - SliderGraphValuePerDay)
            //            SliderGraph.Value += SliderGraphValuePerDay;
            //        else
            //            SliderGraph.Value = SliderGraph.Maximum;
            //        SliderGraph_PreviewMouseUp(null, null);
            //    }
            //    else if (e.Key == Key.Right)
            //    {
            //        if (SliderGraph.Value >= SliderGraph.Minimum + SliderGraphValuePerDay)
            //            SliderGraph.Value -= SliderGraphValuePerDay;
            //        else
            //            SliderGraph.Value = SliderGraph.Minimum;
            //        SliderGraph_PreviewMouseUp(null, null);
            //    }
            //}
        }


        private void SliderGraph_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if ((ListViewCountries != null) && (ListViewCountries.Items.Count > 0))
                LastName = CountryItemToName[ListViewCountries.SelectedItem.ToString()];

            int idx = -1;
            if ((bool)RadioButton1.IsChecked)
                idx = 0;
            else if ((bool)RadioButton2.IsChecked)
                idx = 1;
            else if ((bool)RadioButton3.IsChecked)
                idx = 2;
            else if ((bool)RadioButton4.IsChecked)
                idx = 3;
            UpdateCountryListAndMap(idx);

            if (ListViewCountries != null)
            {
                TextBlockListViewTitle.Inlines.Clear();
                TextBlockListViewTitle.Inlines.Add("By country data on ");
                TextBlockListViewTitle.Inlines.Add(new Run(SliderDate.ToString("yyyy-MM-dd")) { Foreground = Brushes.White });
                ListViewCountries.Focus();
            }
            LabelSliderGraph.Content = SliderDate.ToString("yyyy-MM-dd");
            LabelSliderGraph.Visibility = Visibility.Hidden;
        }


        private void SliderGraph_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            LabelSliderGraph.Content = SliderDate.ToString("yyyy-MM-dd");
            LabelSliderGraph.Visibility = Visibility.Visible;
        }


        private void GridGraph_MouseEnter(object sender, MouseEventArgs e)
        {
            // set SliderGraph location and dimensions before showing it
            double RecWidth, RecHeight;
            if ((GridGraphBackground.ActualWidth / (GridGraphBackground.ActualHeight) >= GraphRatioWH))
            {
                RecHeight = GridGraphBackground.ActualHeight;
                RecWidth = RecHeight * GraphRatioWH;
            }
            else
            {
                RecWidth = GridGraphBackground.ActualWidth;
                RecHeight = RecWidth / GraphRatioWH;
            }
            Thickness mrg = new Thickness(
                0,
                0,
                InitSlideGraphLeft * RecWidth / InitGridGraphBackgroundWidth + (GridGraphBackground.ActualWidth - RecWidth) / 2,
                18 * RecHeight / InitGridGraphBackgroundHeight + (GridGraphBackground.ActualHeight - RecHeight) / 2
            );
            SliderGraph.Margin = mrg;
            SliderGraph.Width = InitSliderGraphWidth * RecWidth / InitGridGraphBackgroundWidth;
            SliderGraph.Visibility = Visibility.Visible;

            Label LabelMinY = (Label)GridGraph.FindName("LabelMinY");
            if (LabelMinY != null)
                LabelMinY.Visibility = Visibility.Hidden;
            Label LabelMaxY = (Label)GridGraph.FindName("LabelMaxY");
            if (LabelMaxY != null)
                LabelMaxY.Visibility = Visibility.Hidden;
            Label LabelMinX = (Label)GridGraph.FindName("LabelMinX");
            if (LabelMinX != null)
                LabelMinX.Visibility = Visibility.Hidden;
            Label LabelMaxX = (Label)GridGraph.FindName("LabelMaxX");
            if (LabelMaxX != null)
                LabelMaxX.Visibility = Visibility.Hidden;
        }


        private void GridGraph_MouseLeave(object sender, MouseEventArgs e)
        {
            // priority here is lower than priority of SliderGraph_MouseEnter so IsMouseOverSliderGraph = true occurs BEFORE SliderGraph.Visibility value is changed. cool :)
            Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                (Action)delegate ()
                {
                    if (IsMouseOverSliderGraph == false)
                    {
                        SliderGraph.Visibility = Visibility.Hidden;

                        Label LabelMinY = (Label)GridGraph.FindName("LabelMinY");
                        if (LabelMinY != null)
                            LabelMinY.Visibility = Visibility.Visible;
                        Label LabelMaxY = (Label)GridGraph.FindName("LabelMaxY");
                        if (LabelMaxY != null)
                            LabelMaxY.Visibility = Visibility.Visible;
                        Label LabelMinX = (Label)GridGraph.FindName("LabelMinX");
                        if (LabelMinX != null)
                            LabelMinX.Visibility = Visibility.Visible;
                        Label LabelMaxX = (Label)GridGraph.FindName("LabelMaxX");
                        if (LabelMaxX != null)
                            LabelMaxX.Visibility = Visibility.Visible;
                    }
                }
            );
        }


        private void SliderGraph_MouseEnter(object sender, MouseEventArgs e)
        {
            IsMouseOverSliderGraph = true;
        }


        private void SliderGraph_MouseLeave(object sender, MouseEventArgs e)
        {
            IsMouseOverSliderGraph = false;
        }


        private void Expander1_Collapsed(object sender, RoutedEventArgs e)
        {
            string str = "'";
            if ((bool)RadioButton1.IsChecked)
                str += RadioButton1.Content.ToString().Trim();
            else if ((bool)RadioButton2.IsChecked)
                str += RadioButton2.Content.ToString().Trim();
            else if ((bool)RadioButton3.IsChecked)
                str += RadioButton3.Content.ToString().Trim();
            else if ((bool)RadioButton4.IsChecked)
                str += RadioButton4.Content.ToString().Trim();
            str += "' map " + SliderDate.ToString("yyyy-MM-dd");
            LabelWorldMap.Content = str;
            LabelWorldMap.Visibility = Visibility.Visible;

            Rectangle RectangleGradient = GridWorldMap.Children.OfType<Rectangle>().FirstOrDefault(el => el.Name == "RectangleGradient");
            if (RectangleGradient != null)
                RectangleGradient.Visibility = Visibility.Visible;

            Label LabelGradientMax = GridWorldMap.Children.OfType<Label>().FirstOrDefault(el => el.Name == "LabelGradientMax");
            if (LabelGradientMax != null)
                LabelGradientMax.Visibility = Visibility.Visible;

            Label LabelGradientMin = GridWorldMap.Children.OfType<Label>().FirstOrDefault(el => el.Name == "LabelGradientMin");
            if (LabelGradientMin != null)
                LabelGradientMin.Visibility = Visibility.Visible;

            // flicker selected country
            if ((ListViewCountries != null) && (ListViewCountries.SelectedIndex >= 0))
            {
                string name = GetCountryName(ListViewCountries.SelectedItem.ToString());
                if (SVGCountryMap.ContainsKey(name))
                    Flicker(SVGCountryMap[name].pth);
            }
        }


        private void Expander1_Expanded(object sender, RoutedEventArgs e)
        {
            LabelWorldMap.Visibility = Visibility.Hidden;

            Rectangle RectangleGradient = GridWorldMap.Children.OfType<Rectangle>().FirstOrDefault(el => el.Name == "RectangleGradient");
            if (RectangleGradient != null)
                RectangleGradient.Visibility = Visibility.Hidden;

            Label LabelGradientMax = GridWorldMap.Children.OfType<Label>().FirstOrDefault(el => el.Name == "LabelGradientMax");
            if (LabelGradientMax != null)
                LabelGradientMax.Visibility = Visibility.Hidden;

            Label LabelGradientMin = GridWorldMap.Children.OfType<Label>().FirstOrDefault(el => el.Name == "LabelGradientMin");
            if (LabelGradientMin != null)
                LabelGradientMin.Visibility = Visibility.Hidden;

            if (ListViewCountries != null)
            {
                Dispatcher.BeginInvoke(
                    DispatcherPriority.Render,
                    (Action)delegate ()
                    {
                        ListViewCountries.Focus();
                    }
                );
            }
        }


        private void SliderGraph_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            SliderDate = MinDate.AddDays((double)GraphNumDays * (SliderGraph.Maximum - SliderGraph.Value) / SliderGraph.Maximum);
            LabelSliderGraph.Content = SliderDate.ToString("yyyy-MM-dd");
            SliderGraph.ToolTip = LabelSliderGraph.Content;
        }


        private void ButtonHelp_Click(object sender, RoutedEventArgs e)
        {
            HelpDlgRtf = new HelpDialogRtf();
            HelpDlgRtf.ShowDialog();
            HelpDlgRtf = null;
        }


        private void ToolBar_Loaded(object sender, RoutedEventArgs e)
        {
            ToolBar toolBar = sender as ToolBar;
            if (toolBar.Template.FindName("OverflowGrid", toolBar) is FrameworkElement overflowGrid)
            {
                overflowGrid.Visibility = Visibility.Collapsed;
            }

            if (toolBar.Template.FindName("MainPanelBorder", toolBar) is FrameworkElement mainPanelBorder)
            {
                mainPanelBorder.Margin = new Thickness();
            }
        }


        private void ButtonUpdate_Click(object sender, RoutedEventArgs e)
        {
            UpdateDataSet();
        }


        private string GetCountryName(string KeyStr)
        {
            foreach (var item in CountryItemToName)
                if (item.Key == KeyStr)
                    return item.Value;

            return "";
        }


        private void ListViewCountries_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // save last CurrentSelection to PreviousSelection
            if (CurrentSelection != null)
                PreviousSelection = new List<string>(CurrentSelection.Select(x => x.Clone() as string));
            CurrentSelection = new List<string>();

            // get selected indexes and names & update CurrentSelection
            SelectedIdx = new List<int>();
            if (ListViewCountries.SelectedItems != null)
                foreach (var item in ListViewCountries.SelectedItems)
                {
                    SelectedIdx.Add(ListViewCountries.Items.IndexOf(item));
                    CurrentSelection.Add(CountryItemToName[item.ToString()]);
                }

            // validate selection count
            if (ListViewCountries.SelectedItems.Count > MaxSelectedItems)
            {
                FoundExcessItems = true;

                // deselect last selected item: this id done itratively until not excess items are found
                ListViewItem item = ((ListViewItem)ListViewCountries.ItemContainerGenerator.ContainerFromIndex(SelectedIdx[SelectedIdx.Count - 1]));
                if (item != null)
                    item.IsSelected = false;
            }
            else
            {
                if (IterativelyUpdatingSelection == false)
                {
                    GridGraph.Children.Clear();

                    if (ListViewCountries.SelectedIndex >= 0)
                    {
                        TextBlockLabelGraphTitle.Inlines.Clear();
                        string perLabel = "";
                        if ((bool)RadioButton1.IsChecked)
                            perLabel += RadioButton1.Content.ToString().Trim();
                        else if ((bool)RadioButton2.IsChecked)
                            perLabel += RadioButton2.Content.ToString().Trim();
                        else if ((bool)RadioButton3.IsChecked)
                            perLabel += RadioButton3.Content.ToString().Trim();
                        else if ((bool)RadioButton4.IsChecked)
                            perLabel += RadioButton4.Content.ToString().Trim();
                        TextBlockLabelGraphTitle.Inlines.Add($"Historical '{perLabel}' data for ");
                        for (int i = 0; i < SelectedIdx.Count; i++)
                        {
                            Brush ForegroundBrush =
                                MaxSelectedItems == TrueColourBrush.Count ? TrueColourBrush[i] : TrueColourBrush[0];
                            TextBlockLabelGraphTitle.Inlines.Add(
                                new Run(GetCountryName(ListViewCountries.Items[SelectedIdx[i]].ToString()) + " ")
                                {
                                    Foreground = ForegroundBrush
                                }
                            );
                        }

                        int idx = -1;
                        if ((bool)RadioButton1.IsChecked)
                            idx = 0;
                        else if ((bool)RadioButton2.IsChecked)
                            idx = 1;
                        else if ((bool)RadioButton3.IsChecked)
                            idx = 2;
                        else if ((bool)RadioButton4.IsChecked)
                            idx = 3;
                        SetupGraph(idx);

                        ((Label)GridGraph.FindName("LabelMaxY")).Visibility = Visibility.Visible;
                        ((Label)GridGraph.FindName("LabelMinY")).Visibility = Visibility.Visible;
                        if (SliderGraph.Visibility != Visibility.Visible)
                        {
                            ((Label)GridGraph.FindName("LabelMinX")).Visibility = Visibility.Visible;
                            ((Label)GridGraph.FindName("LabelMaxX")).Visibility = Visibility.Visible;
                        }

                        for (int i = SelectedIdx.Count - 1; i >= 0; i--)
                        {
                            //string name = GetCountryName(ListViewCountries.SelectedItem.ToString());
                            string name = GetCountryName(ListViewCountries.Items[SelectedIdx[i]].ToString());

                            if ((DatasetCountryGraph[name].pth[idx] != null) && (DatasetCountryGraph[name].pth[idx].Count > 0))
                            {

                                //// create a composite shape (Path) for all data points; this shape can only have one tooltip
                                //GeometryGroup GeomGrp = new GeometryGroup();

                                //foreach (PointInfo ptInfo in DatasetCountryGraph[name].pth[idx])
                                //{
                                //    EllipseGeometry eli = new EllipseGeometry
                                //    {
                                //        Center = ptInfo.pt,
                                //        RadiusX = 2,
                                //        RadiusY = 2
                                //    };
                                //    GeomGrp.Children.Add(eli);
                                //}

                                //Path pth = new Path
                                //{
                                //    Stroke = Brushes.White,
                                //    Fill = Brushes.White,
                                //    StrokeThickness = 1,
                                //    Data = GeomGrp
                                //};
                                //GridGraph.Children.Add(pth);

                                // alternatively, create one shape per data point, each with its own tooltip
                                Brush GraphBrush =
                                    MaxSelectedItems == TrueColourBrush.Count ? TrueColourBrush[i] : TrueColourBrush[0];
                                foreach (PointInfo ptInfo in DatasetCountryGraph[name].pth[idx])
                                {
                                    Ellipse eli = new Ellipse
                                    {
                                        Height = 4,
                                        Width = 4,
                                        StrokeThickness = 1,
                                        Stroke = GraphBrush,
                                        Fill = GraphBrush,
                                        HorizontalAlignment = HorizontalAlignment.Left,
                                        VerticalAlignment = VerticalAlignment.Top
                                    };
                                    eli.ToolTip = new ToolTip { Content = $"{name}, {ptInfo.date} : {ptInfo.val}" };
                                    eli.Margin = new Thickness(ptInfo.pt.X, ptInfo.pt.Y, 0, 0);

                                    GridGraph.Children.Add(eli);
                                }
                            }
                        }
                    }
                }

                if (FoundExcessItems == true)
                {
                    FoundExcessItems = false;

                    MessageBox.Show(
                        $"You can select up to {MaxSelectedItems} countries to overlay historical data. " + Environment.NewLine + "Your selection was adjusted accordingly.",
                        "COV2CON",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                        );
                }
            }
        }


        void Country_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Source is FrameworkElement mouseWasDownOn)
            {
                string elementName = mouseWasDownOn.Name;
                string key = mouseWasDownOn.ToolTip.ToString();
                if (key.Contains("-") == true)
                    key = CountryItemToName[key];
                //Flicker(mouseWasDownOn);
                //ToggleCountrySelection(key);
            }
            e.Handled = true;
        }


        public void ToggleCountrySelection(string name)
        {
            if (SVGCountryMap[name].selected == false)
                SVGCountryMap[name].pth.Fill = Brushes.Green;
            else
                SVGCountryMap[name].pth.Fill = defaultCountryFillBrush;
            SVGCountryMap[name].selected = !SVGCountryMap[name].selected;
        }


        public async void UpdateDataSet()
        {
            Mouse.OverrideCursor = Cursors.Wait;

            LabelUpdate.Content = "Acquiring data...";

            ToolBarControl.IsEnabled = false;
            TextBoxFilter.IsEnabled = false;
            SliderGraph.IsEnabled = false;
            Expander1.IsEnabled = false;
            LastName = null;

            await ReadDataSet();
            SliderDate = MaxDate;
            UpdateCountryListAndMap(1);

            LabelUpdate.Content = "Dated: " + CurrentUpdate.ToString("yyyy-MM-dd");
            TextBlockListViewTitle.Inlines.Clear();
            TextBlockListViewTitle.Inlines.Add("By country data on ");
            TextBlockListViewTitle.Inlines.Add(new Run(SliderDate.ToString("yyyy-MM-dd")) { Foreground = Brushes.White });
            Expander1.IsEnabled = true;
            SliderGraph.IsEnabled = true;
            ToolBarControl.IsEnabled = true;
            TextBoxFilter.IsEnabled = true;
            if (CanTryManualUpdate == true)
                ButtonUpdate.IsEnabled = true;
            else
                ButtonUpdate.IsEnabled = false;
            ListViewCountries.Focus();

            Mouse.OverrideCursor = null;
        }


        public void SetupGraph(int idx)
        {
            GridGraph.Children.Clear();

            try
            {
                GridGraph.UnregisterName("LabelMaxY");
                GridGraph.UnregisterName("LabelMinY");
                GridGraph.UnregisterName("LabelMinX");
                GridGraph.UnregisterName("LabelMaxX");
            }
            catch (Exception) { }

            Rectangle RecFrame = new Rectangle
            {
                Width = 920,
                Height = 506,
                Stroke = Brushes.Transparent,
                StrokeThickness = 2,
                Fill = null
            };
            RecFrame.HorizontalAlignment = HorizontalAlignment.Stretch;
            RecFrame.VerticalAlignment = VerticalAlignment.Stretch;
            GridGraph.Children.Add(RecFrame);

            GraphRatioWH = RecFrame.Width / RecFrame.Height;

            Thickness thkMaxY = new Thickness(2, 2, 860, 2);
            Thickness thkMinY = new Thickness(2, 2, 860, 18);
            Thickness thkMinX = new Thickness(30, 2, 2, 6);
            Thickness thkMaxX = new Thickness(2, 2, 2, 6);

            Label LabelMaxY = new Label
            {
                Foreground = Brushes.White,
                FontSize = SystemFonts.MessageFontSize - 1,
                Name = "LabelMaxY",
                Content = MaxYVar[idx].ToString()
            };
            LabelMaxY.Margin = thkMaxY;
            LabelMaxY.HorizontalAlignment = HorizontalAlignment.Right;
            LabelMaxY.VerticalAlignment = VerticalAlignment.Top;
            GridGraph.Children.Add(LabelMaxY);
            GridGraph.RegisterName(LabelMaxY.Name, LabelMaxY);
            LabelMaxY.Visibility = Visibility.Hidden;

            Label LabelMinY = new Label
            {
                Foreground = Brushes.White,
                FontSize = SystemFonts.MessageFontSize - 1,
                Name = "LabelMinY",
                Content = ""
            };
            LabelMinY.Margin = thkMinY;
            LabelMinY.HorizontalAlignment = HorizontalAlignment.Right;
            LabelMinY.VerticalAlignment = VerticalAlignment.Bottom;
            LabelMinY.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            GridGraph.Children.Add(LabelMinY);
            GridGraph.RegisterName(LabelMinY.Name, LabelMinY);
            LabelMinY.Visibility = Visibility.Hidden;

            Label LabelMinX = new Label
            {
                Foreground = Brushes.White,
                FontSize = SystemFonts.MessageFontSize - 1,
                Name = "LabelMinX",
                Content = MinDate.ToString("yyyy-MM-dd")
            };
            LabelMinX.Margin = thkMinX;
            LabelMinX.HorizontalAlignment = HorizontalAlignment.Left;
            LabelMinX.VerticalAlignment = VerticalAlignment.Bottom;
            LabelMinX.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            GridGraph.Children.Add(LabelMinX);
            GridGraph.RegisterName(LabelMinX.Name, LabelMinX);
            LabelMinX.Visibility = Visibility.Hidden;

            Label LabelMaxX = new Label
            {
                Foreground = Brushes.White,
                FontSize = SystemFonts.MessageFontSize - 1,
                Name = "LabelMaxX",
                Content = MaxDate.ToString("yyyy-MM-dd")
            };
            LabelMaxX.Margin = thkMaxX;
            LabelMaxX.HorizontalAlignment = HorizontalAlignment.Right;
            LabelMaxX.VerticalAlignment = VerticalAlignment.Bottom;
            GridGraph.Children.Add(LabelMaxX);
            GridGraph.RegisterName(LabelMaxX.Name, LabelMaxX);
            LabelMaxX.Visibility = Visibility.Hidden;

            GraphLeft = thkMinX.Left + LabelMinX.DesiredSize.Width / 4 + 3;
            GraphBottom = thkMinY.Bottom + LabelMaxY.DesiredSize.Height / 4 + 15;
            GraphWidth = 920 - thkMinX.Left - LabelMinX.DesiredSize.Width / 2;
            GraphHeight = 506 - thkMinX.Bottom - LabelMinY.DesiredSize.Height * 3 / 4 - 8;

            InitSlideGraphLeft = GraphLeft;
            InitSliderGraphWidth = GraphWidth + 12;  // la plesneala...

            Rectangle RecGraph = new Rectangle
            {
                Width = GraphWidth,
                Height = GraphHeight,
                Stroke = Brushes.Transparent,
                StrokeThickness = 0.5,
                Fill = null
            };
            RecGraph.Margin = new Thickness(GraphLeft, 0, 0, GraphBottom);
            RecGraph.HorizontalAlignment = HorizontalAlignment.Left;
            RecGraph.VerticalAlignment = VerticalAlignment.Bottom;
            GridGraph.Children.Add(RecGraph);

            // label min on heat map legend
            Label LabelGradientMin = GridWorldMap.Children.OfType<Label>().FirstOrDefault(e => e.Name == "LabelGradientMin");
            if (LabelGradientMin == null)
            {
                LabelGradientMin = new Label()
                {
                    Content = "",
                    Name = "LabelGradientMin",
                    Foreground = Brushes.LightGray
                };
                LabelGradientMin.HorizontalAlignment = HorizontalAlignment.Left;
                LabelGradientMin.VerticalAlignment = VerticalAlignment.Top;
                Thickness mrg = new Thickness(34, 832, 0, 0);
                LabelGradientMin.Margin = mrg;
                GridWorldMap.Children.Add(LabelGradientMin);
                GridWorldMap.RegisterName(LabelGradientMin.Name, LabelGradientMin);
                LabelGradientMin.Visibility = Visibility.Hidden;
            }

            // label max on heat map legend
            Label LabelGradientMax = GridWorldMap.Children.OfType<Label>().FirstOrDefault(e => e.Name == "LabelGradientMax");
            if (LabelGradientMax == null)
            {
                LabelGradientMax = new Label()
                {
                    Name = "LabelGradientMax",
                    Foreground = Brushes.LightGray
                };
                LabelGradientMax.HorizontalAlignment = HorizontalAlignment.Left;
                LabelGradientMax.VerticalAlignment = VerticalAlignment.Top;
                Thickness mrg = new Thickness(34, 921, 0, 0);
                LabelGradientMax.Margin = mrg;
                GridWorldMap.Children.Add(LabelGradientMax);
                GridWorldMap.RegisterName(LabelGradientMax.Name, LabelGradientMax);
                LabelGradientMax.Visibility = Visibility.Hidden;
            }
            LabelGradientMax.Content = LabelMaxY.Content;
        }
    }


    public class CountryMapInfo
    {
        public int dictIdx;         // pointer to item's dictionary key
        public String name;         // name should match a name in CountryGraphInfo and vice versa
        public Path pth;            // SVG contour
        public Brush brs;           // colour in heat map
        public bool selected;


        public CountryMapInfo(int anIdx, string aName, Path aPath, Brush aBrush)
        {
            dictIdx = anIdx;
            name = aName;
            pth = aPath;
            brs = aBrush;
            selected = false;
        }
    }


    public class CountryGraphInfo
    {
        public string name;                 // ideally, name should match a name in CountryMapInfo and vice versa
        public List<List<PointInfo>> pth;   // points of historical data to plot in graph


        public CountryGraphInfo(string aName)
        {
            name = aName;
            pth = new List<List<PointInfo>> { new List<PointInfo>(), new List<PointInfo>(), new List<PointInfo>(), new List<PointInfo>(), new List<PointInfo>() };
        }
    }


    public class CountryACPOInfo
    {
        public Dictionary<DateTime, double> DictPositiveRate;
        public Dictionary<DateTime, double> DictActiveCasesPerMillion;
        public Dictionary<DateTime, double> DictHospICU;
        public Dictionary<DateTime, double> DictACPO;


        public CountryACPOInfo()
        {
            DictPositiveRate = new Dictionary<DateTime, double>();
            DictActiveCasesPerMillion = new Dictionary<DateTime, double>();
            DictHospICU = new Dictionary<DateTime, double>();
            DictACPO = new Dictionary<DateTime, double>();
        }
    }


    public class PointInfo
    {
        public string date;
        public double val;
        public Point pt;


        public PointInfo(string aDate, Point aPoint, double aValue)
        {
            date = aDate;
            pt = aPoint;
            val = aValue;
        }
    }
}
