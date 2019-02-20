using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
namespace HDCircles.Hackathon
{
    using Catel.IoC;
    using Catel.MVVM;
    using HDCircles.Hackathon.Services;
    using HDCircles.Hackathon.ViewModels;
    using Windows.UI.Xaml.Controls;

    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private readonly ICommandManager _commandManager;

        private readonly MainPageViewModel _viewModel;

        public MainPage()
        {
            InitializeComponent();

            var dependencyResolver = this.GetDependencyResolver();
            var typeFactory = this.GetTypeFactory();

            var commandManager = dependencyResolver.Resolve<ICommandManager>();
            var vm = typeFactory.CreateInstance<MainPageViewModel>();

            MainPageViewModel.Dispatcher = Dispatcher;

            _viewModel = vm;
            _commandManager = commandManager;
            DataContext = _viewModel;

            Loaded += MainPage_Loaded;
        }

        private void MainPage_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var vm = ((MainPage)sender)._viewModel;

            vm.MainPage = this;
            vm.SwapChainPanel = FpvSwapChainPanel;
            
            _commandManager.ExecuteCommand(Commands.MainPageLoaded);

            Loaded -= MainPage_Loaded;
        }

        private void HyperlinkButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(GraphPage));
        }
    }
}
