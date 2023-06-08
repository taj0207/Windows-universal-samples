using System;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Storage.Streams;
using Windows.Security.Cryptography;
using Windows.Devices.SmartCards;

namespace SDKTemplate
{
    public sealed partial class Scenario8_TransmitAPDU : Page
    {
        MainPage rootPage = MainPage.Current;

        public Scenario8_TransmitAPDU()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Click handler for the 'TransmitAPDU' button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Transmit_Click(object sender, RoutedEventArgs e)
        {
            if (!rootPage.ValidateTPMSmartCard())
            {
                rootPage.NotifyUser("Use Scenario One to create a TPM virtual smart card.", NotifyType.ErrorMessage);
            }
            if (ApduToSend.Text.Length % 2 != 0)
            {
                rootPage.NotifyUser("Lenght of ApduToSend must be odd.", NotifyType.ErrorMessage);
            }

            Button b = sender as Button;
            b.IsEnabled = false;

            try
            {                
                {
                    SmartCard card = await rootPage.GetSmartCard();
                    SmartCardProvisioning provisioning = await SmartCardProvisioning.FromSmartCardAsync(card);
                    IBuffer result = null;
                    using (SmartCardConnection connection = await card.ConnectAsync())
                    {
                        rootPage.NotifyUser(ApduToSend.Text , NotifyType.StatusMessage);
                        byte[] sendapdu  = Enumerable.Range(0, ApduToSend.Text.Length).Where(x => x % 2 == 0).Select(x => Convert.ToByte(ApduToSend.Text.Substring(x, 2), 16)).ToArray();
                        // default: get atr
                        // 00 CB 2F 01 02 5C 00 FF
                        // select ppse
                        // 00 A4 04 00 0E 32 50 41 59 2E 53 59 53 2E 44 44 46 30 31 00 

                        IBuffer apdu = CryptographicBuffer.CreateFromByteArray(sendapdu);

                        result = await connection.TransmitAsync(apdu);
                        ApduResponse.Text = CryptographicBuffer.EncodeToHexString(result);
                    }
                }
            }
            catch (Exception ex)
            {
                rootPage.NotifyUser("Transmitting APDU to card failed with exception: " + ex.ToString(), NotifyType.ErrorMessage);
            }
            finally
            {
                b.IsEnabled = true;
            }
        }
    }
}
