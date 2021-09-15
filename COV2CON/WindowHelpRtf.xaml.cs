using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Resources;
using System.Windows.Threading;


namespace COV2CON
{
    public partial class HelpDialogRtf : Window
    {
        public HelpDialogRtf()
        {
            InitializeComponent();

            FlowDocumentReaderHelp.AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(HandlerRequestNavigate));
        }


        async private void FlowDocumentReaderHelp_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Uri uri = new Uri("pack://application:,,,/help/help.rtf");
                StreamResourceInfo sri = Application.GetResourceStream(uri);
                if (sri != null)
                    using (Stream stream = sri.Stream)
                    {
                        FlowDocumentReaderHelp.Visibility = Visibility.Hidden;
                        FlowDocumentReaderHelp.Selection.Load(stream, DataFormats.Rtf);
                        await Task.Delay(500);
                        FlowDocumentReaderHelp.Document.Blocks.FirstBlock.BringIntoView();
                        FlowDocumentReaderHelp.Visibility = Visibility.Visible;
                    }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"** Exception reading 'help.rtf' resource, with message: {ex.Message}");
            }
        }


        // ref: https://docs.microsoft.com/en-us/dotnet/desktop/wpf/controls/how-to-save-load-and-print-richtextbox-content?view=netframeworkdesktop-4.8
        private void Window_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Ctrl P = print
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (e.Key == Key.P)
                {
                    // this works but still need to adjust from 2->1 columns
                    //FlowDocumentReaderHelp.Print();

                    PrintDialog pd = new PrintDialog
                    {
                        PageRangeSelection = PageRangeSelection.AllPages,
                        UserPageRangeEnabled = true
                    };

                    if (pd.ShowDialog() == true)
                    {
                        // need to create & print a duplicate floddocument else app crashes
                        MemoryStream stream = new MemoryStream();
                        TextRange sourceDocument = new TextRange(FlowDocumentReaderHelp.Document.ContentStart, FlowDocumentReaderHelp.Document.ContentEnd);
                        sourceDocument.Save(stream, DataFormats.Rtf);

                        FlowDocument flowDocumentCopy = new FlowDocument
                        {
                            // set col cnt = 1 (default = 2)
                            PagePadding = new Thickness(50),
                            ColumnGap = 0,
                            ColumnWidth = pd.PrintableAreaWidth
                        };
                        TextRange copyDocumentRange = new TextRange(flowDocumentCopy.ContentStart, flowDocumentCopy.ContentEnd);
                        copyDocumentRange.Load(stream, DataFormats.Rtf);


                        DocumentPaginator dp = ((IDocumentPaginatorSource)flowDocumentCopy).DocumentPaginator;
                        if (dp != null)
                            pd.PrintDocument(dp, "COV2CON Help");
                    }
                }
            }
        }


        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.ActualWidth < 800)
                this.Width = 800;

            if (this.ActualHeight < 680)
                this.Height = 680;

            FlowDocumentReaderHelp.Document.PagePadding = new Thickness(10);
            FlowDocumentReaderHelp.Document.ColumnGap = 12.0;
            FlowDocumentReaderHelp.Document.ColumnWidth = (FlowDocumentReaderHelp.ActualWidth - FlowDocumentReaderHelp.Document.ColumnGap) / 2;
        }


        void HandlerRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(e.Uri.ToString());
            e.Handled = true;
        }
    }
}
