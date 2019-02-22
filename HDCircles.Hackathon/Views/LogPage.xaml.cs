namespace HDCircles.Hackathon.Views
{
    using HDCircles.Hackathon.Services;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using Windows.UI.Core;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Data;

    public sealed partial class LogPage : Page
    {
        private object updateLock = new object();

        private ObservableCollection<LogRecord> Records { get; set; }

        public LogPage()
        {
            this.InitializeComponent();
            
            Records = new ObservableCollection<LogRecord>();

            Loaded += LogPage_Loaded;
            Unloaded += LogPage_Unloaded;
        }

        private void LogPage_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var records = Logger.Instance.GetLogs();

            lock (updateLock)
            {
                records.ForEach(Records.Add);
                
                LogGrid.ItemsSource = Records;
            }

            Logger.Instance.Logged += Instance_Logged;
        }
        
        private void LogPage_Unloaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Logger.Instance.Logged -= Instance_Logged;
        }
        
        private void Instance_Logged(LogRecord record)
        {
            lock (updateLock)
            {
                Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Records.Add(record);
                }).AsTask().Wait();
            }
        }
    }
}
