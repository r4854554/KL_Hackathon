using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;


// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace HDCircles.Hackathon.Views
{
    using DJI.WindowsSDK;
    using Windows.UI.Xaml.Controls;

    public sealed partial class ActivationPage : Page
    {
        public ActivationPage()
        {
            this.InitializeComponent();

            Loaded += ActivationPage_Loaded;
            Unloaded += ActivationPage_Unloaded;
        }

        private async void ActivationPage_Loaded(object sender, RoutedEventArgs e)
        {
            SdkKeyTextBox.Text = "cb98b917674f98a483eb9228";

            DJISDKManager.Instance.SDKRegistrationStateChanged += Instance_SDKRegistrationStateChanged;

            if (DJISDKManager.Instance.SDKRegistrationResultCode == SDKError.NO_ERROR)
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    ActivationStateTextBox.Text = SDKRegistrationState.Succeeded.ToString();
                    ActivationResultTextBox.Text = SDKError.NO_ERROR.ToString();
                });
            }

            Loaded -= ActivationPage_Loaded;
        }

        private async void ActivationPage_Unloaded(object sender, RoutedEventArgs e)
        {
            DJISDKManager.Instance.SDKRegistrationStateChanged -= Instance_SDKRegistrationStateChanged;
        }

        private async void Instance_SDKRegistrationStateChanged(DJI.WindowsSDK.SDKRegistrationState state, DJI.WindowsSDK.SDKError errorCode)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                ActivationStateTextBox.Text = state.ToString();
                ActivationResultTextBox.Text = errorCode.ToString();
            });
        }

        private void ActivateButton_Click(object sender, RoutedEventArgs e)
        {
            DJISDKManager.Instance.RegisterApp(SdkKeyTextBox.Text);
        }
    }
}
