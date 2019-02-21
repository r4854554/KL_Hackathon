
using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Uwp;
using System;
using System.Threading;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace HDCircles.Hackathon
{
    public class DateModel
    {
        public System.DateTime DateTime { get; set; }
        public double Value { get; set; }
    }

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class GraphPage : Page
    {
        public GraphPage()
        {
            InitializeComponent();


            var dayConfig = Mappers.Xy<DateModel>()
    .X(dayModel => (double)dayModel.DateTime.Ticks / TimeSpan.FromHours(1).Ticks)
    .Y(dayModel => dayModel.Value);

            //Notice you can also configure this type globally, so you don't need to configure every
            //SeriesCollection instance using the type.
            //more info at http://lvcharts.net/App/Index#/examples/v1/wpf/Types%20and%20Configuration
            var i = 1;
            Series = new SeriesCollection(dayConfig)
            {
                new LineSeries
                {
                    Values = new ChartValues<DateModel>
                    {
                        new DateModel
                        {
                            DateTime = System.DateTime.Now,
                            Value = i
                        }
                    },
                    Fill = new SolidColorBrush(Windows.UI.Colors.Transparent)
                },
               
            };
            var temp = new DateModel();
            
         
            for (var j = 1; j<50; j++)
            {
                AddDataModel(new DateModel
                {
                    DateTime = System.DateTime.Now,
                    Value = i++
                });
                Thread.Sleep(100);
            }
            


            Formatter = value => new System.DateTime((long)(value * TimeSpan.FromHours(1).Ticks)).ToString("t");
            DataContext = this;
        }
        public void AddDataModel(DateModel dataModel)
        {
            Series[0].Values.Add(dataModel);
        }
        public Func<double, string> Formatter { get; set; }
        public SeriesCollection Series { get; set; }
        public Func<double, string> YFormatter { get; set; }
    }
}
