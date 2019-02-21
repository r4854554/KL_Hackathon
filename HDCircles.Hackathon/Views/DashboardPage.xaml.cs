namespace HDCircles.Hackathon.Views
{
    using HDCircles.Hackathon.Services;
    using LiveCharts;
    using System;
    using System.Diagnostics;
    using System.Linq;
    using Windows.UI.Core;
    using Windows.UI.Xaml.Controls;

    public sealed partial class DashboardPage : Page
    {
        private object stateLock = new object();

        private FlightState flightState;

        private ChartValues<double> Values { get; set; }

        private int Count = 0;

        private int MaxRecordNumber = 1000;

        public DashboardPage()
        {
            InitializeComponent();

            Values = new ChartValues<double>();
            Drone.Instance.StateChanged += Drone_StateChanged;

            YawChartSeries.Values = Values;
        }

        private void Drone_StateChanged(FlightState state)
        {
            lock (stateLock)
            {
                try
                {
                    flightState = new FlightState(state.Altitude, state.Yaw, state.Pitch, state.Roll, state.Error);

                    var first = Values.DefaultIfEmpty(0).FirstOrDefault();

                    Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        if (Values.Count >= MaxRecordNumber)
                        {
                            Values.Remove(first);
                        }

                        if (Values.Count < MaxRecordNumber)
                        {
                            Values.Add(flightState.Yaw);
                        }
                    });

                    Count = Values.Count;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                }
            }
        }
    }
}
