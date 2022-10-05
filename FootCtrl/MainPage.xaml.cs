using System;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

using Windows.Devices.Enumeration;
using Windows.Devices.Midi;
using System.Threading.Tasks;
using Windows.UI.Core;
using System.Threading;
using System.Text;
using Windows.Storage.Streams;
using Windows.UI.ViewManagement;
using Windows.ApplicationModel.ExtendedExecution;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.VisualBasic;
using System.Net;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Bluetooth;
using System.ComponentModel;
using Windows.Security.Cryptography;
using System.Collections.Generic;
using Windows.Devices.PointOfService;
using Windows.Devices.Power;
using System.Security.Cryptography;
using Windows.ApplicationModel;



// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

// https://docs.microsoft.com/it-it/windows/uwp/audio-video-camera/midi


namespace FootCtrl
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    /// 

    public sealed partial class MainPage : Page
    {

        public ValueStopwatch stopwatch = ValueStopwatch.StartNew();
       
        public readonly struct ValueStopwatch
        {
            private static readonly double s_timestampToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;

            private readonly long _startTimestamp;

            private ValueStopwatch(long startTimestamp) => _startTimestamp = startTimestamp;

            public static ValueStopwatch StartNew() => new ValueStopwatch(GetTimestamp());

            public static long GetTimestamp() => Stopwatch.GetTimestamp();

            public static TimeSpan GetElapsedTime(long startTimestamp, long endTimestamp)
            {
                var timestampDelta = endTimestamp - startTimestamp;
                var ticks = (long)(s_timestampToTicks * timestampDelta);
                return new TimeSpan(ticks);
            }

            public TimeSpan GetElapsedTime() => GetElapsedTime(_startTimestamp, GetTimestamp());
        }


        


        MidiInPort[] midiInPort;
        bool[] isCommected;
        String[] ContainerId;
        String[] DeviceNames;
        IDictionary<String, String> batteryLevel ;
        

        DeviceInformationCollection midiInputDevices = null;
        DeviceInformationCollection midiOutputDevices = null;


        bool enumerationBTCompleted = false;


        private ObservableCollection<BluetoothLEDeviceDisplay> KnownDevices = new ObservableCollection<BluetoothLEDeviceDisplay>();


        private ExtendedExecutionSession session = null;

        IMidiOutPort midiOutPort;
        public static TeVirtualMIDI port;



        public enum NotifyType
        {
            Green,
            Red
        };

        public void NotifyUser(string strMessage, NotifyType type)
        {
            // If called from the UI thread, then update immediately.
            // Otherwise, schedule a task on the UI thread to perform the update.
            if (Dispatcher.HasThreadAccess)
            {
                UpdateStatus(strMessage, type);
            }
            else
            {
                var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateStatus(strMessage, type));
            }
        }

        private  void UpdateStatus(string strMessage,NotifyType type)
        {
            switch (type)
            {
                case NotifyType.Green:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                    break;
                case NotifyType.Red:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                    break;
            }

            StatusBlock.Text = strMessage;
        }


        

        private String Between(String value, String a, String b)
        {
            int posA = value.IndexOf(a);
            int posB = value.LastIndexOf(b);
            if (posA == -1)
            {
                return "";
            }
            if (posB == -1)
            {
                return "";
            }
            int adjustedPosA = posA + a.Length;
            if (adjustedPosA >= posB)
            {
                return "";
            }
            return value.Substring(adjustedPosA, posB - adjustedPosA);
        }

        private bool isConnectedBT(DeviceInformation midiDeviceInfo)
        {
            bool btConnected = true;
            String ContainerId = (String)midiDeviceInfo.Properties["System.Devices.ContainerId"].ToString();

            for (int i = 0; i < Math.Min(3, KnownDevices.Count); i++)
            {
                String BTContainerId = (String)KnownDevices[i].DeviceInformation.Properties["System.Devices.Aep.ContainerId"].ToString();
                if (BTContainerId == ContainerId)
                {
                    if (!KnownDevices[i].IsConnected)
                    {
                        btConnected = false;
                    }
                }

            }
            return btConnected;
        }


        private string FormatValueByPresentation(IBuffer buffer)
        {
            // BT_Code: For the purpose of this sample, this function converts only UInt32 and
            // UTF-8 buffers to readable text. It can be extended to support other formats if your app needs them.
            byte[] data;
            CryptographicBuffer.CopyToByteArray(buffer, out data);


            try
            {
                // battery level is encoded as a percentage value in the first byte according to
                // https://www.bluetooth.com/specifications/gatt/viewer?attributeXmlFile=org.bluetooth.characteristic.battery_level.xml
                return data[0].ToString();
            }
            catch (ArgumentException)
            {
                return "Battery Level: (unable to parse)";
            }
                
               
        }

        private async Task GetBatteryLevel(DeviceInformation deviceInfo)
        {

            String BTContainerId = (String)deviceInfo.Properties["System.Devices.Aep.ContainerId"].ToString();

            if (batteryLevel.ContainsKey(BTContainerId))
            {
                 
            }


            BluetoothLEDevice bluetoothLeDevice = null;
            bluetoothLeDevice = await BluetoothLEDevice.FromIdAsync(deviceInfo.Id);

            if (bluetoothLeDevice == null)
            {
                Debug.WriteLine("Error geting battey 1...");
                return;

            }
            //get UUID of Services
            var services = await bluetoothLeDevice.GetGattServicesAsync();
            if (services != null)
            {
                foreach (var servicesID in services.Services)
                {

                    //if there is a service thats same like the Battery Service
                    String sName = servicesID.Uuid.ToString();
                    if (servicesID.Uuid.ToString() != "")
                    {
                        //updateServiceList is like a console logging in my tool
                        //updateServiceList($"Service: {servicesID.Uuid}");

                        var characteristics = await servicesID.GetCharacteristicsAsync();
                        foreach (var character in characteristics.Characteristics)
                        {
                            if (character.Uuid.Equals(GattCharacteristicUuids.BatteryLevel))
                            {

                                //updateServiceList("C - UUID: " + character.Uuid.ToString());
                                GattReadResult result = await character.ReadValueAsync();
                                if (result.Status == GattCommunicationStatus.Success)
                                {
                                    string formattedResult = FormatValueByPresentation(result.Value);

                                    Debug.WriteLine($"Battery: {BTContainerId} = {formattedResult}");
                                    if ( batteryLevel.ContainsKey(BTContainerId) )
                                    {
                                        batteryLevel[BTContainerId] = formattedResult + "%";
                                    }
                                    else
                                    {
                                        batteryLevel.Add(BTContainerId, formattedResult + "%");
                                    }
                                    UpdateBatteyInfo();

                                }

                            }
                        }

                    }


                }
            }
        }

        private String GetBatteryFromContainerID(String containerId)
        {
            if ( batteryLevel.ContainsKey (containerId) )
            {
                return batteryLevel[containerId];
            }
            return "";
        }


        private void UpdateBatteyInfo()
        {
            int i = 0;
            foreach (DeviceInformation deviceInfo in midiInputDevices)
            {
                if (deviceInfo.Name == "FootCtrl (Bluetooth MIDI IN)")
                {
                    String ContainerId = (String)deviceInfo.Properties["System.Devices.ContainerId"].ToString();
                    String battery = GetBatteryFromContainerID(ContainerId);
                    String code = Between(deviceInfo.Id, "MIDII_", "#{");
                    String devName = $"Footctrl: {battery} {code}";
                    String devNameConnected = $"Footctrl: {battery} CONNECTED {code}";

                    if (!isCommected[i])
                    {
                        this.midiInPortListBox.Items[i] = devName;

                    }
                    else
                    {
                        this.midiInPortListBox.Items[i] = devNameConnected;

                    }
                    i++;
                }
            }

                
        }

        private async Task EnumerateMidiInputDevices()
        {

            Debug.WriteLine("Scanning input devices ...");

            // Find all input MIDI devices

            if (midiInputDevices==null)
            {
                string midiInputQueryString = MidiInPort.GetDeviceSelector();
                midiInputDevices = await DeviceInformation.FindAllAsync(midiInputQueryString);
            }



            // Return if no external devices are connected
            if (midiInputDevices.Count == 0)
            {
                this.midiInPortListBox.Items.Add("No MIDI input devices found!");
                this.midiInPortListBox.IsEnabled = false;
                return;
            }

            midiInPortListBox.Items.Clear();

            // Else, add each connected input device to the list
            int devNumber = -1;
            foreach (DeviceInformation deviceInfo in midiInputDevices)
            {

                if (   deviceInfo.Name == "FootCtrl (Bluetooth MIDI IN)") 
                {
                    devNumber++;

                    String ContainerId = (String)deviceInfo.Properties["System.Devices.ContainerId"].ToString();

                    String battery = GetBatteryFromContainerID(ContainerId);
                    String code = Between(deviceInfo.Id, "MIDII_", "#{");
                    String devName = $"Footctrl: {battery} {code}";
                    String devNameConnected = $"Footctrl: {battery} CONNECTED {code}";

                    DeviceNames[devNumber] = devName;

                    if (!isCommected[devNumber])
                    {
                        this.midiInPortListBox.Items.Add(devName);

                    }
                    else
                    {
                        this.midiInPortListBox.Items.Add(devNameConnected);

                    }

                    bool btConnected = isConnectedBT(deviceInfo);

                    Debug.WriteLine($"Device {devNumber}: {code} MIDI:{isCommected[devNumber]} BT: {btConnected}  ");


                    if (!isCommected[devNumber])  // midi was not connected
                    {


                        if (btConnected)
                        {
                            try
                            {

                                midiInPort[devNumber] = await MidiInPort.FromIdAsync(deviceInfo.Id);
                                if (midiInPort[devNumber] == null)
                                {
                                    System.Diagnostics.Debug.WriteLine("Unable to create MidiInPort from input device");

                                }
                                else
                                {
                                    this.midiInPortListBox.Items[devNumber] = devNameConnected;
                               
                                    midiInPort[devNumber].MessageReceived += MidiInPort_MessageReceived;
                                    isCommected[devNumber] = true;
                                    Debug.WriteLine($"midiInPort[{devNumber}] Connected");
                                }
                            }
                            catch
                            {
                                Debug.WriteLine("Execpion on midiInPort[0]");
                            }
                        }

                    }
                    else // midi was connected
                    {
                        if (!btConnected)
                        {
                            midiInPort[devNumber].Dispose();
                            midiInPort[devNumber] = null;
                            isCommected[devNumber] = false;
                            this.midiInPortListBox.Items[devNumber] = devName;
                        }
                    }

       
                }
            }
            this.midiInPortListBox.IsEnabled = false;

            UpdateStatus("Done",NotifyType.Green);

        }

        private async Task EnumerateMidiOutputDevices()
        {

            UpdateStatus("Scanning input devices ...",NotifyType.Red);



            if (midiOutputDevices == null)
            {
                string midiOutportQueryString = MidiOutPort.GetDeviceSelector();
                midiOutputDevices = await DeviceInformation.FindAllAsync(midiOutportQueryString);
            }
            midiOutPortListBox.Items.Clear();

            // Return if no external devices are connected
            if (midiOutputDevices.Count == 0)
            {
                this.midiOutPortListBox.Items.Add("No MIDI output devices found!");
                this.midiOutPortListBox.IsEnabled = false;
                return;
            }

            // Else, add each connected input device to the list
            foreach (DeviceInformation deviceInfo in midiOutputDevices)
            {
                if (deviceInfo.Name == "MIDI")
                {
                    this.midiOutPortListBox.Items.Add(deviceInfo.Name);
                    midiOutPort = await MidiOutPort.FromIdAsync(deviceInfo.Id);

                    if (midiOutPort == null)
                    {
                        Debug.WriteLine("Unable to create MidiOutPort from output device");
                        return;
                    }
                    return;
                }
            }
            this.midiOutPortListBox.IsEnabled = false;

            UpdateStatus("Done",NotifyType.Green);

        }
        public static string GetAppVersion()
        {
            Package package = Package.Current;
            PackageId packageId = package.Id;
            PackageVersion version = packageId.Version;

            return string.Format("ver = {0}.{1}.{2}", version.Major, version.Minor, version.Build);
        }

        public async Task GetAllBatteyLevels()
        {
            if (true)
            {
                for (int i = 0; i < Math.Min(3, KnownDevices.Count); i++)
                {

                    GetBatteryLevel(KnownDevices[i].DeviceInformation);

                }
            }
        }


        public  async Task  RescanMidiInPorts()
        {

            
            UpdateStatus("Scanning input devices ...", NotifyType.Red);

            bool bbBattery_Level = (bool) Battery_Level.IsChecked;




            if (bbBattery_Level)
            {
                for (int i = 0; i < Math.Min(3, KnownDevices.Count); i++)
                {

                     GetBatteryLevel(KnownDevices[i].DeviceInformation);

                }
            }


            await EnumerateMidiInputDevices();

        }


        private async void SessionRevoked(object sender, ExtendedExecutionRevokedEventArgs args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                switch (args.Reason)
                {
                    case ExtendedExecutionRevokedReason.Resumed:
                        NotifyUser("Extended execution revoked due to returning to foreground.", NotifyType.Green);
                        break;

                    case ExtendedExecutionRevokedReason.SystemPolicy:
                        NotifyUser("Extended execution revoked due to system policy.", NotifyType.Green);
                        break;
                }

                //EndExtendedExecution();
            });
        }
        private async void BeginExtendedExecution()
        {
            // The previous Extended Execution must be closed before a new one can be requested.
            // This code is redundant here because the sample doesn't allow a new extended
            // execution to begin until the previous one ends, but we leave it here for illustration.
            //ClearExtendedExecution();

            var newSession = new ExtendedExecutionSession();
            newSession.Reason = ExtendedExecutionReason.Unspecified;
            newSession.Description = "Raising periodic toasts";
            newSession.Revoked += SessionRevoked;
            ExtendedExecutionResult result = await newSession.RequestExtensionAsync();

            switch (result)
            {
                case ExtendedExecutionResult.Allowed:
                    NotifyUser("Extended execution allowed.", NotifyType.Green);
                    session = newSession;
                    //periodicTimer = new System.Threading.Timer(OnTimer, DateTime.Now, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10));
                    break;

                default:
                case ExtendedExecutionResult.Denied:
                    NotifyUser("Extended execution denied.", NotifyType.Green);
                    newSession.Dispose();
                    break;
            }
            //UpdateUI();
        }

        public  MainPage()
        {
            const int maxPort = 4;

            midiInPort = new MidiInPort[maxPort];
            isCommected = new bool[maxPort];
            ContainerId = new String[maxPort];
            batteryLevel = new Dictionary<String, String>();
            DeviceNames = new String[maxPort];

            this.InitializeComponent();

            txtVersion.Text = GetAppVersion();

            KnownDevices.Clear();

            ApplicationView.PreferredLaunchViewSize = new Size(600, 400);
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;

            BeginExtendedExecution();

            DeviceWatcher pairedBTDeviceWatcher = null;


            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected", "System.Devices.Aep.Bluetooth.Le.IsConnectable" };
 
            
            //string aqsAllBluetoothLEDevices = "(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")";
            string aqsAllBluetoothLEDevices = "System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\" AND (System.Devices.Aep.IsPaired:=System.StructuredQueryType.Boolean#True \r\n  OR System.Devices.Aep.Bluetooth.IssueInquiry:=System.StructuredQueryType.Boolean#False) ";


            pairedBTDeviceWatcher =
                    DeviceInformation.CreateWatcher(
                        aqsAllBluetoothLEDevices,
                        requestedProperties,
                        DeviceInformationKind.AssociationEndpoint);

            pairedBTDeviceWatcher.Added += DeviceWatcher_Added;
            pairedBTDeviceWatcher.Removed += DeviceWatcher_Removed;
            pairedBTDeviceWatcher.Updated += DeviceWatcher_Updated;
            pairedBTDeviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;

            pairedBTDeviceWatcher.Start();



            EnumerateMidiOutputDevices();
    
            //RescanMidiInPorts();

                //int devN = MidiDeviceNumberFromBTDevice(deviceInfoUpdate);
                //Debug.WriteLine(String.Format("Updated {0}{1}", deviceInfoUpdate.Id, devN));
            

            //RescanPorts();


            //inputDeviceWatcher =   new MyMidiDeviceWatcher(MidiInPort.GetDeviceSelector(), midiInPortListBox, Dispatcher);

            //inputDeviceWatcher.StartWatcher();

            //outputDeviceWatcher = new MyMidiDeviceWatcher(MidiOutPort.GetDeviceSelector(), midiOutPortListBox, Dispatcher);

            //outputDeviceWatcher.StartWatcher();


        }



        private void Button_Click(object sender, RoutedEventArgs e)
        {
            byte channel = 0;
            byte note = 60;
            byte velocity = 127;
            IMidiMessage midiMessageToSend = new MidiNoteOnMessage(channel, note, velocity);

            midiOutPort.SendMessage(midiMessageToSend);
        }

        private void MidiInPort_MessageReceived(MidiInPort sender, MidiMessageReceivedEventArgs args)
        {
            IMidiMessage receivedMidiMessage = args.Message;

            midiOutPort.SendMessage(receivedMidiMessage);

            Debug.WriteLine(receivedMidiMessage.Timestamp.ToString());

            StringBuilder outputMessage = new StringBuilder();
            outputMessage.Append(receivedMidiMessage.Timestamp.ToString()).Append(", Type: ").Append(receivedMidiMessage.Type);

            switch (receivedMidiMessage.Type)
            {
                case MidiMessageType.NoteOff:
                    var noteOffMessage = (MidiNoteOffMessage)receivedMidiMessage;
                    outputMessage.Append(", Channel: ").Append(noteOffMessage.Channel).Append(", Note: ").Append(noteOffMessage.Note).Append(", Velocity: ").Append(noteOffMessage.Velocity);
                    break;
                case MidiMessageType.NoteOn:
                    var noteOnMessage = (MidiNoteOnMessage)receivedMidiMessage;
                    outputMessage.Append(", Channel: ").Append(noteOnMessage.Channel).Append(", Note: ").Append(noteOnMessage.Note).Append(", Velocity: ").Append(noteOnMessage.Velocity);
                    break;
                case MidiMessageType.PolyphonicKeyPressure:
                    var polyphonicKeyPressureMessage = (MidiPolyphonicKeyPressureMessage)receivedMidiMessage;
                    outputMessage.Append(", Channel: ").Append(polyphonicKeyPressureMessage.Channel).Append(", Note: ").Append(polyphonicKeyPressureMessage.Note).Append(", Pressure: ").Append(polyphonicKeyPressureMessage.Pressure);
                    break;
                case MidiMessageType.ControlChange:
                    var controlChangeMessage = (MidiControlChangeMessage)receivedMidiMessage;
                    outputMessage.Append(", Channel: ").Append(controlChangeMessage.Channel).Append(", Controller: ").Append(controlChangeMessage.Controller).Append(", Value: ").Append(controlChangeMessage.ControlValue);
                    break;
                case MidiMessageType.ProgramChange:
                    var programChangeMessage = (MidiProgramChangeMessage)receivedMidiMessage;
                    outputMessage.Append(", Channel: ").Append(programChangeMessage.Channel).Append(", Program: ").Append(programChangeMessage.Program);
                    break;
                case MidiMessageType.ChannelPressure:
                    var channelPressureMessage = (MidiChannelPressureMessage)receivedMidiMessage;
                    outputMessage.Append(", Channel: ").Append(channelPressureMessage.Channel).Append(", Pressure: ").Append(channelPressureMessage.Pressure);
                    break;
                case MidiMessageType.PitchBendChange:
                    var pitchBendChangeMessage = (MidiPitchBendChangeMessage)receivedMidiMessage;
                    outputMessage.Append(", Channel: ").Append(pitchBendChangeMessage.Channel).Append(", Bend: ").Append(pitchBendChangeMessage.Bend);
                    break;
                case MidiMessageType.SystemExclusive:
                    var systemExclusiveMessage = (MidiSystemExclusiveMessage)receivedMidiMessage;
                    outputMessage.Append(", ");

                    // Read the SysEx bufffer
                    var sysExDataReader = DataReader.FromBuffer(systemExclusiveMessage.RawData);
                    while (sysExDataReader.UnconsumedBufferLength > 0)
                    {
                        byte byteRead = sysExDataReader.ReadByte();
                        // Pad with leading zero if necessary
                        outputMessage.Append(byteRead.ToString("X2")).Append(" ");
                    }
                    break;
                case MidiMessageType.MidiTimeCode:
                    var timeCodeMessage = (MidiTimeCodeMessage)receivedMidiMessage;
                    outputMessage.Append(", FrameType: ").Append(timeCodeMessage.FrameType).Append(", Values: ").Append(timeCodeMessage.Values);
                    break;
                case MidiMessageType.SongPositionPointer:
                    var songPositionPointerMessage = (MidiSongPositionPointerMessage)receivedMidiMessage;
                    outputMessage.Append(", Beats: ").Append(songPositionPointerMessage.Beats);
                    break;
                case MidiMessageType.SongSelect:
                    var songSelectMessage = (MidiSongSelectMessage)receivedMidiMessage;
                    outputMessage.Append(", Song: ").Append(songSelectMessage.Song);
                    break;
                case MidiMessageType.TuneRequest:
                    var tuneRequestMessage = (MidiTuneRequestMessage)receivedMidiMessage;
                    break;
                case MidiMessageType.TimingClock:
                    var timingClockMessage = (MidiTimingClockMessage)receivedMidiMessage;
                    break;
                case MidiMessageType.Start:
                    var startMessage = (MidiStartMessage)receivedMidiMessage;
                    break;
                case MidiMessageType.Continue:
                    var continueMessage = (MidiContinueMessage)receivedMidiMessage;
                    break;
                case MidiMessageType.Stop:
                    var stopMessage = (MidiStopMessage)receivedMidiMessage;
                    break;
                case MidiMessageType.ActiveSensing:
                    var activeSensingMessage = (MidiActiveSensingMessage)receivedMidiMessage;
                    break;
                case MidiMessageType.SystemReset:
                    var systemResetMessage = (MidiSystemResetMessage)receivedMidiMessage;
                    break;
                case MidiMessageType.None:
                    throw new InvalidOperationException();
                default:
                    break;
            }


            NotifyUser(outputMessage.ToString(),NotifyType.Green);

            if (receivedMidiMessage.Type == MidiMessageType.NoteOn)
            {
                Debug.WriteLine(((MidiNoteOnMessage)receivedMidiMessage).Channel);
                Debug.WriteLine(((MidiNoteOnMessage)receivedMidiMessage).Note);
                Debug.WriteLine(((MidiNoteOnMessage)receivedMidiMessage).Velocity);
            }
        }



        

        private void Rescan_Click(object sender, RoutedEventArgs e)
        {
            RescanMidiInPorts();
        }


   /*     private async void UpdateDevices()
        {
            Debug.WriteLine("Umpdating devices ...");

            RescanMidiInPorts();

        }*/


        private async void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation deviceInfo)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            /*await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (this)
                {*/
                    Debug.WriteLine(String.Format("Added {0}{1}", deviceInfo.Id, deviceInfo.Name));

                    // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                    
                        // Make sure device isn't already present in the list.
                        if (FindBluetoothLEDeviceDisplay(deviceInfo.Id) == null)
                        {
                            if (deviceInfo.Name == "FootCtrl")
                            {
                                // If device has a friendly name display it immediately.
                                KnownDevices.Add(new BluetoothLEDeviceDisplay(deviceInfo));

                            }
                            else
                            {
                                // Add it to a list in case the name gets updated later. 
                                //UnknownDevices.Add(deviceInfo);
                            }
                        }

                        foreach (var deviceProperty in deviceInfo.Properties)
                        {
                            if (deviceProperty.Key == "System.Devices.Aep.ContainerId")
                            {
                                Debug.WriteLine(deviceInfo.Name + " " + deviceProperty.Key + ": " + deviceProperty.Value);

                            }

                        }

                    
               /* }
            });*/
        }



        /// <summary>
        /// Update UI on device removed
        /// </summary>
        /// <param name="sender">The active DeviceWatcher instance</param>
        /// <param name="args">Event arguments</param>
        private async void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            return;

            // If all devices have been enumerated
            if (this.enumerationBTCompleted)
            {

                Debug.WriteLine(String.Format("Removed {0}{1}", deviceInfoUpdate.Id, ""));
                UpdateStatus("Done", NotifyType.Green);

                await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
                {

                    BluetoothLEDeviceDisplay bleDeviceDisplay = FindBluetoothLEDeviceDisplay(deviceInfoUpdate.Id);
                    if (bleDeviceDisplay != null)
                    {
                        KnownDevices.Remove(bleDeviceDisplay);
                    }
                    // Update the device list
                    //UpdateDevices();

                });
            }
        }


        private int MidiDeviceNumberFromBTDevice(DeviceInformation BTdeviceInfo)
        {

            String BTContainerId = (String)BTdeviceInfo.Properties["System.Devices.Aep.ContainerId"].ToString();

            int devNumber = -1;
            if (midiInputDevices != null)
            {
                foreach (DeviceInformation deviceInfo in midiInputDevices)
                {

                    if (deviceInfo.Name == "FootCtrl (Bluetooth MIDI IN)")
                    {
                        devNumber++;
                        String ContainerId = (String)deviceInfo.Properties["System.Devices.ContainerId"].ToString();
                        if (ContainerId == BTContainerId)
                        {
                            return devNumber;
                        }

                    }

                }
            }

            return -1;

        }



        private async void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {

                lock (this)
                {
                    Debug.WriteLine(String.Format("Updating {0}{1}", deviceInfoUpdate.Id, ""));

                    // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                    {
                        BluetoothLEDeviceDisplay bleDeviceDisplay = FindBluetoothLEDeviceDisplay(deviceInfoUpdate.Id);
                        if (bleDeviceDisplay.Name == "FootCtrl")
                        {
                            // Device is already being displayed - update UX.
                            bleDeviceDisplay.Update(deviceInfoUpdate);
                        }

                    }

                    if (enumerationBTCompleted)
                    {
                        RescanMidiInPorts();
                    }
                }
            });
        }

        /// <summary>
        /// Update UI on device enumeration completed.
        /// </summary>
        /// <param name="sender">The active DeviceWatcher instance</param>
        /// <param name="args">Event arguments</param>
        private async void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object e)
        {

            Debug.WriteLine($"{KnownDevices.Count} devices found. Enumeration completed.");


            enumerationBTCompleted = true;


            await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                RescanMidiInPorts();
            });
        }


        private BluetoothLEDeviceDisplay FindBluetoothLEDeviceDisplay(string id)
        {
            foreach (BluetoothLEDeviceDisplay bleDeviceDisplay in KnownDevices)
            {
                if (bleDeviceDisplay.Id == id)
                {
                    return bleDeviceDisplay;
                }
            }
            return null;
        }


    }
    
}
