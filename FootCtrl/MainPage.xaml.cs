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
using System.Threading;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.VisualBasic;
using System.Net;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Bluetooth;



// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

// https://docs.microsoft.com/it-it/windows/uwp/audio-video-camera/midi


namespace FootCtrl
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
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


        /*private async Task   GetBattery(string deviceID)
        {
            var device = await BluetoothLEDevice.FromIdAsync(deviceID);

            //get UUID of Services
            var services = await device.GetGattServicesAsync();
            if (services != null)
            {
                foreach (var servicesID in services.Services)
                {

                    //if there is a service thats same like the Battery Service
                    if (servicesID.Uuid.ToString() == BluetoothBLE.Constants.BATTERY_SERVICE)
                    {
                        //updateServiceList is like a console logging in my tool
                        updateServiceList($"Service: {servicesID.Uuid}");

                        var characteristics = await servicesID.GetCharacteristicsAsync();
                        foreach (var character in characteristics.Characteristics)
                        {
                            if (Constants.BATTERY_LEVEL == character.Uuid.ToString())
                            {

                                updateServiceList("C - UUID: " + character.Uuid.ToString());
                                GattReadResult result = await character.ReadValueAsync();
                                if (result.Status == GattCommunicationStatus.Success)
                                {
                                    var reader = DataReader.FromBuffer(result.Value);
                                    byte[] input = new byte[reader.UnconsumedBufferLength];
                                    reader.ReadBytes(input);
                                    System.Diagnostics.Debug.WriteLine(BitConverter.ToString(input));

                                }

                            }
                        }

                    }
                }
            }*/

        //MidiDeviceWatcher midiInDeviceWatcher;
        //MidiDeviceWatcher outputDeviceWatcher;
        MidiInPort midiInPort1;
        MidiInPort midiInPort2;
        MidiInPort midiInPort3;

        bool enumerationCompleted = false;
        CoreDispatcher coreDispatcher = null;


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

        private async Task ConnectFoot1(DeviceInformation deviceInfo)
        {
            //Debug.WriteLine("Connecting  midiInPort1 ...");
            Debug.WriteLine("Connecting  midiInPort1 ...");


            try
            {

                midiInPort1 = await MidiInPort.FromIdAsync(deviceInfo.Id);
                if (midiInPort1 == null)
                {
                    System.Diagnostics.Debug.WriteLine("Unable to create MidiInPort from input device");

                }
                else
                {
                    this.midiInPortListBox.Items.Add(deviceInfo.Name);
                    midiInPort1.MessageReceived += MidiInPort_MessageReceived;
                    Debug.WriteLine("midiInPort1 Connected");

                }
            }
            catch
            {
                Debug.WriteLine("Execpion on midiInPort1");
            }
        }

        private async Task ConnectFoot2(DeviceInformation deviceInfo)
        {

            Debug.WriteLine("Connecting  midiInPort2 ...");

            try
            {

                midiInPort2 = await MidiInPort.FromIdAsync(deviceInfo.Id);
                if (midiInPort2 == null)
                {
                    Debug.WriteLine("Unable to create MidiInPort from input device");

                }
                else
                {
                    this.midiInPortListBox.Items.Add(deviceInfo.Name);
                    midiInPort2.MessageReceived += MidiInPort_MessageReceived;
                    Debug.WriteLine("midiInPort2 Connected");

                }
            }
            catch
            {
                Debug.WriteLine("Execpion on midiInPort2");
            }
        }

        private async Task ConnectFoot3(DeviceInformation deviceInfo)
        {
            Debug.WriteLine("Connecting  midiInPort3 ...");

            try
            {

                midiInPort3 = await MidiInPort.FromIdAsync(deviceInfo.Id);
                if (midiInPort3 == null)
                {
                    Debug.WriteLine("Unable to create MidiInPort from input device");

                }
                else
                {
                    this.midiInPortListBox.Items.Add(deviceInfo.Name);
                    midiInPort3.MessageReceived += MidiInPort_MessageReceived;
                    Debug.WriteLine("midiInPort3 Connected");

                }
            }
            catch
            {
                Debug.WriteLine("Execpion on midiInPort3");
            }
        }

        private async Task EnumerateMidiInputDevices()
        {

            UpdateStatus("Scanning input devices ...",NotifyType.Green);

            if (midiInPort1 != null) midiInPort1.Dispose();
            if (midiInPort2 != null) midiInPort2.Dispose();
            if (midiInPort3 != null) midiInPort3.Dispose();


            // Find all input MIDI devices
            string midiInputQueryString =  MidiInPort.GetDeviceSelector();
            DeviceInformationCollection midiInputDevices = await DeviceInformation.FindAllAsync(midiInputQueryString);

            midiInPortListBox.Items.Clear();

            // Return if no external devices are connected
            if (midiInputDevices.Count == 0)
            {
                this.midiInPortListBox.Items.Add("No MIDI input devices found!");
                this.midiInPortListBox.IsEnabled = false;
                return;
            }

            // Else, add each connected input device to the list
            int devNumber = 0;
            foreach (DeviceInformation deviceInfo in midiInputDevices)
            {

                
                if (   deviceInfo.Name.Contains("FootCtrl")) {
                    devNumber++;

                    if ( devNumber == 1)
                    {
                        try
                        {
                              ConnectFoot1(deviceInfo);

                        }
                        catch
                        {
                            //Debug.WriteLine("Execpion on midiInPort1");
                            Debug.WriteLine("Execpion on midiInPort1");

                        }

                    }

  
                    if (devNumber == 2)
                    {
                        try
                        {
                             ConnectFoot2(deviceInfo);
                        }
                        catch
                        {
                            //Debug.WriteLine("Execpion on midiInPort1");
                            Debug.WriteLine("Execpion on midiInPort2");

                        }

                    }
                    
  

                    if (devNumber == 3)
                    {
                        try
                        {
                             ConnectFoot3(deviceInfo);
                        }

                        catch
                        {
                            //Debug.WriteLine("Execpion on midiInPort2");
                            Debug.WriteLine("Execpion on midiInPort3");

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


            // Find all output MIDI devices
            string midiOutportQueryString = MidiOutPort.GetDeviceSelector();
            DeviceInformationCollection midiOutputDevices = await DeviceInformation.FindAllAsync(midiOutportQueryString);

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

        public  async Task  RescanPorts()
        {
            await EnumerateMidiInputDevices();
            await EnumerateMidiOutputDevices();

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


            this.InitializeComponent();

            KnownDevices.Clear();

            ApplicationView.PreferredLaunchViewSize = new Size(600, 400);
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;

            BeginExtendedExecution();

            DeviceWatcher midiInDeviceWatcher = null;


            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected", "System.Devices.Aep.Bluetooth.Le.IsConnectable" };
            string aqsAllBluetoothLEDevices = "(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")";


            midiInDeviceWatcher =
                    DeviceInformation.CreateWatcher(
                        aqsAllBluetoothLEDevices,
                        requestedProperties,
                        DeviceInformationKind.AssociationEndpoint);




            midiInDeviceWatcher.Added += DeviceWatcher_Added;
            midiInDeviceWatcher.Removed += DeviceWatcher_Removed;
            midiInDeviceWatcher.Updated += DeviceWatcher_Updated;
            midiInDeviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;

            midiInDeviceWatcher.Start();




            RescanPorts();


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
            RescanPorts();
        }


        private async void UpdateDevices()
        {
            Debug.WriteLine("Umpdating devices ...");

            RescanPorts();

        }

        /// <summary>
        /// Update UI on device added
        /// </summary>
        /// <param name="sender">The active DeviceWatcher instance</param>
        /// <param name="args">Event arguments</param>
        private async void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation deviceInfo)
        {

            Debug.WriteLine(String.Format("Added {0}{1}", deviceInfo.Id, deviceInfo.Name));

            // If all devices have been enumerated
            if (true)// enumerationCompleted)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
                {
                    // Update the device list

                    if (FindBluetoothLEDeviceDisplay(deviceInfo.Id) == null)
                    {
                        if (deviceInfo.Name == "FootCtrl")
                        {
                            // If device has a friendly name display it immediately.
                            KnownDevices.Add(new BluetoothLEDeviceDisplay(deviceInfo));
                        }
                    }


                    //UpdateDevices();
                });
            }
        }

        /// <summary>
        /// Update UI on device removed
        /// </summary>
        /// <param name="sender">The active DeviceWatcher instance</param>
        /// <param name="args">Event arguments</param>
        private async void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            // If all devices have been enumerated
            if (this.enumerationCompleted)
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

        /// <summary>
        /// Update UI on device updated
        /// </summary>
        /// <param name="sender">The active DeviceWatcher instance</param>
        /// <param name="args">Event arguments</param>
        private async void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            Debug.WriteLine(String.Format("Updated {0}{1}", deviceInfoUpdate.Id, ""));


            // If all devices have been enumerated
            if (this.enumerationCompleted)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
                {

                    BluetoothLEDeviceDisplay bleDeviceDisplay = FindBluetoothLEDeviceDisplay(deviceInfoUpdate.Id);
                    if (bleDeviceDisplay != null)
                    {
                        // Device is already being displayed - update UX.
                        double seconds = stopwatch.GetElapsedTime().TotalSeconds;
                        Debug.WriteLine(seconds);
                        Debug.WriteLine(String.Format("IsConnectable {0} - IsConnected {1}", bleDeviceDisplay.IsConnectable.ToString(), bleDeviceDisplay.IsConnected.ToString()));

                        //if (bleDeviceDisplay.IsConnectable &&  !bleDeviceDisplay.IsConnected || !bleDeviceDisplay.IsConnectable && bleDeviceDisplay.IsConnected) {
                        if (seconds > 10) { 
                            bleDeviceDisplay.Update(deviceInfoUpdate);
                            stopwatch = ValueStopwatch.StartNew();
                            UpdateDevices();
                            return;
                        }

                    }
                    // Update the device list

                });
            }
        }

        /// <summary>
        /// Update UI on device enumeration completed.
        /// </summary>
        /// <param name="sender">The active DeviceWatcher instance</param>
        /// <param name="args">Event arguments</param>
        private async void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object e)
        {

            Debug.WriteLine($"{KnownDevices.Count} devices found. Enumeration completed.");


            enumerationCompleted = true;
            await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {

                // Update the device list
                //UpdateDevices();
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
