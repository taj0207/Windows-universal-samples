using System;
using System.IO;
using System.Reflection;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Storage.Streams;
using Windows.Security.Cryptography;
using Windows.Devices.SmartCards;
using System.Collections.Generic;
using Windows.Devices.Enumeration;
using System.Threading.Tasks;
using System.Threading;
using Windows.Storage;
using System.Diagnostics;

namespace SDKTemplate
{
    public sealed partial class Scenario9_tmp : Page
    {
        MainPage rootPage = MainPage.Current;
        List<SmartCardListItem> cardItems;
        string SmartCardReaderDeviceId;

        public string gettime()
        {
            DateTime now = DateTime.Now;
            Stopwatch stopwatch = Stopwatch.StartNew();

            // Format the current time with hours, minutes, seconds, and milliseconds
            string formattedTime = now.ToString("HH:mm:ss.fff");

            // Get the microseconds part from the stopwatch
            long microseconds = stopwatch.ElapsedTicks / (Stopwatch.Frequency / (1000L * 1000L));

            // Combine the formatted time with microseconds
            string timeWithMicroseconds = $"{formattedTime}:{microseconds % 1000:D3}";

            return timeWithMicroseconds;
        }
        public async void DebugOutput(string s)
        {
            StorageFolder storageFolder = await KnownFolders.GetFolderForUserAsync(null /* current user */, KnownFolderId.PicturesLibrary);
            Windows.Storage.StorageFile sampleFile = await storageFolder.CreateFileAsync(@"output.log", CreationCollisionOption.OpenIfExists);
            await FileIO.AppendTextAsync( sampleFile, gettime() +"  " + s + "\n");
        }

        public Scenario9_tmp()
        {
            InitializeComponent();
            EnumCardReader();
            ItemListView.SelectedIndex = 0;
        }

        /// <summary>
        /// Click handler for the 'TransmitAPDU' button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Transmit()
        {
            DebugOutput("Transmit");

            SmartCardReader reader = await SmartCardReader.FromIdAsync(SmartCardReaderDeviceId);

            IReadOnlyList<SmartCard> cards = await reader.FindAllCardsAsync();

            if (1 != cards.Count)
            {
                throw new InvalidOperationException("Reader has an unexpected number of cards (" + cards.Count + ")");
            }
            SmartCard card = cards[0];
            rootPage.NotifyUser("get card: " + card.ToString(), NotifyType.StatusMessage);
            DebugOutput("get card: " + card.ToString());

            SmartCardProvisioning provisioning = await SmartCardProvisioning.FromSmartCardAsync(card);
            IBuffer result = null;
            using (SmartCardConnection connection = await card.ConnectAsync())
            {
                DebugOutput("sending APDU: " + ApduToSend.Text);
                rootPage.NotifyUser(ApduToSend.Text, NotifyType.StatusMessage);
                Thread.Sleep(3000);
                byte[] sendapdu = Enumerable.Range(0, ApduToSend.Text.Length).Where(x => x % 2 == 0).Select(x => Convert.ToByte(ApduToSend.Text.Substring(x, 2), 16)).ToArray();
                // default: get atr
                // 00 CB 2F 01 02 5C 00 FF
                // select ppse
                // 00 A4 04 00 0E 32 50 41 59 2E 53 59 53 2E 44 44 46 30 31 00 

                IBuffer apdu = CryptographicBuffer.CreateFromByteArray(sendapdu);

                result = await connection.TransmitAsync(apdu);
                ApduResponse.Text = CryptographicBuffer.EncodeToHexString(result);
                DebugOutput("got APDU: " + ApduResponse.Text);
            }
        }

        async void cardadded(SmartCardReader reader, CardAddedEventArgs args)
        {
            DebugOutput("Cardadd enter");
            foreach (SmartCardListItem scli in cardItems)
            {
                if (scli.Reader == reader)
                {
                    SmartCardProvisioning provisioning = await SmartCardProvisioning.FromSmartCardAsync(args.SmartCard);
                    scli.CardNames.Add(await provisioning.GetNameAsync());
                    DebugOutput("Cardadded1");
                    break;

                }
            }
            rootPage.NotifyUser("Add card to card reader: " + reader.Name, NotifyType.StatusMessage);
            DebugOutput("Cardadded");
            Transmit();
        }
        async void cardremoved(SmartCardReader reader, CardRemovedEventArgs args)
        {
            foreach (SmartCardListItem scli in cardItems)
            {
                if (scli.Reader == reader)
                {
                    SmartCardProvisioning provisioning = await SmartCardProvisioning.FromSmartCardAsync(args.SmartCard);
                    scli.CardNames.Remove(await provisioning.GetNameAsync());
                    break;
                }
            }
            rootPage.NotifyUser("Remove card from card reader: " + reader.Name, NotifyType.StatusMessage);

        }
        private async void EnumCardReader()
        {
            try
            {
                rootPage.NotifyUser("Enumerating smart cards...", NotifyType.StatusMessage);
                cardItems = new List<SmartCardListItem>();

                string selector = SmartCardReader.GetDeviceSelector();
                DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(selector);

                // DeviceInformation.FindAllAsync gives us a
                // DeviceInformationCollection, which is essentially a list
                // of DeviceInformation objects.  We must iterate through that
                // list and instantiate SmartCardReader objects from the
                // DeviceInformation objects.
                foreach (DeviceInformation device in devices)
                {
                    SmartCardReader reader = await SmartCardReader.FromIdAsync(device.Id);

                    // For each reader, we want to find all the cards associated
                    // with it.  Then we will create a SmartCardListItem for
                    // each (reader, card) pair.
                    IReadOnlyList<SmartCard> cards = await reader.FindAllCardsAsync();

                    var item = new SmartCardListItem()
                    {
                        Reader = reader,
                        CardNames = new List<string>(cards.Count),
                    };

                    foreach (SmartCard card in cards)
                    {
                        SmartCardProvisioning provisioning = await SmartCardProvisioning.FromSmartCardAsync(card);
                        item.CardNames.Add(await provisioning.GetNameAsync());
                    }

                    cardItems.Add(item);
                }
                // Bind the source of ItemListView to our SmartCardListItem list.
                ItemListView.ItemsSource = cardItems;
                ItemListView.SelectedIndex = -1;
                ItemListView.SelectionChanged += SelectedIndexChange;

                rootPage.NotifyUser("Enumerating smart cards completed.", NotifyType.StatusMessage);
            }
            catch (Exception ex)
            {
                rootPage.NotifyUser("Enumerating smart cards failed with exception: " + ex.ToString(), NotifyType.ErrorMessage);
            }
            finally
            {
            }
        }
        async void SelectedIndexChange(object sender, SelectionChangedEventArgs args)
        {
            SmartCardReader reader;
            if (SmartCardReaderDeviceId != null)
            {
                reader = await SmartCardReader.FromIdAsync(SmartCardReaderDeviceId);
                reader.CardAdded -= cardadded;
                reader.CardRemoved -= cardremoved;
            }
            SmartCardReaderDeviceId = cardItems[ItemListView.SelectedIndex].Reader.DeviceId;
            reader = await SmartCardReader.FromIdAsync(SmartCardReaderDeviceId);
            reader.CardAdded += cardadded;
            reader.CardRemoved += cardremoved;
            rootPage.NotifyUser("select card reader: " + SmartCardReaderDeviceId, NotifyType.StatusMessage);
            DebugOutput("select card reader: " + SmartCardReaderDeviceId);

        }
    }
}
