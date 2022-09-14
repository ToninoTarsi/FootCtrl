using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using Windows.Devices.Enumeration;
using Windows.Devices.Midi;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.ApplicationModel.UserActivities;
using System.Threading;
using System.Text;
using Windows.Storage.Streams;
using Windows.UI.ViewManagement;



// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

// https://docs.microsoft.com/it-it/windows/uwp/audio-video-camera/midi


namespace FootCtrl
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        MyMidiDeviceWatcher inputDeviceWatcher;
        MyMidiDeviceWatcher outputDeviceWatcher;
        MidiInPort midiInPort1;
        MidiInPort midiInPort2;
        MidiInPort midiInPort3;

        IMidiOutPort midiOutPort;
        public static TeVirtualMIDI port;


        public void NotifyUser(string strMessage)
        {
            // If called from the UI thread, then update immediately.
            // Otherwise, schedule a task on the UI thread to perform the update.
            if (Dispatcher.HasThreadAccess)
            {
                UpdateStatus(strMessage);
            }
            else
            {
                var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateStatus(strMessage));
            }
        }

        private void UpdateStatus(string strMessage)
        {

            StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Green);


            StatusBlock.Text = strMessage;
        }


            private async Task EnumerateMidiInputDevices()
        {

            if (midiInPort1 != null) midiInPort1.Dispose();
            if (midiInPort2 != null) midiInPort2.Dispose();
            if (midiInPort3 != null) midiInPort3.Dispose();


            // Find all input MIDI devices
            string midiInputQueryString = MidiInPort.GetDeviceSelector();
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
                if (deviceInfo.Name.Contains("FootCtrl")) {
                    devNumber++;
                    this.midiInPortListBox.Items.Add(deviceInfo.Name);

                    if ( devNumber == 1)
                    {
                        midiInPort1 = await MidiInPort.FromIdAsync(deviceInfo.Id);
                        if (midiInPort1 == null)
                        {
                            System.Diagnostics.Debug.WriteLine("Unable to create MidiInPort from input device");
                            return;
                        }
                        midiInPort1.MessageReceived += MidiInPort_MessageReceived;
                    }

                    if (devNumber == 2)
                    {
                        midiInPort2 = await MidiInPort.FromIdAsync(deviceInfo.Id);
                        if (midiInPort2 == null)
                        {
                            System.Diagnostics.Debug.WriteLine("Unable to create MidiInPort2 from input device");
                            return;
                        }
                        midiInPort2.MessageReceived += MidiInPort_MessageReceived;
                    }

                    if (devNumber == 3)
                    {
                        midiInPort3 = await MidiInPort.FromIdAsync(deviceInfo.Id);
                        if (midiInPort3 == null)
                        {
                            System.Diagnostics.Debug.WriteLine("Unable to create MidiInPort2 from input device");
                            return;
                        }
                        midiInPort3.MessageReceived += MidiInPort_MessageReceived;
                    }

                }
            }
            this.midiInPortListBox.IsEnabled = false;
        }

        private async Task EnumerateMidiOutputDevices()
        {

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
                        System.Diagnostics.Debug.WriteLine("Unable to create MidiOutPort from output device");
                        return;
                    }
                }
            }
            this.midiOutPortListBox.IsEnabled = false;
        }

        public void RescanPorts()
        {
            EnumerateMidiInputDevices();
            EnumerateMidiOutputDevices();

        }
  

        public  MainPage()
        {

            RescanPorts();

            this.InitializeComponent();

            ApplicationView.PreferredLaunchViewSize = new Size(600, 400);
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;


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

            System.Diagnostics.Debug.WriteLine(receivedMidiMessage.Timestamp.ToString());

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


            NotifyUser(outputMessage.ToString());

            if (receivedMidiMessage.Type == MidiMessageType.NoteOn)
            {
                System.Diagnostics.Debug.WriteLine(((MidiNoteOnMessage)receivedMidiMessage).Channel);
                System.Diagnostics.Debug.WriteLine(((MidiNoteOnMessage)receivedMidiMessage).Note);
                System.Diagnostics.Debug.WriteLine(((MidiNoteOnMessage)receivedMidiMessage).Velocity);
            }
        }

        private async void midiOutPortListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var deviceInformationCollection = outputDeviceWatcher.DeviceInformationCollection;

            if (deviceInformationCollection == null)
            {
                return;
            }

            DeviceInformation devInfo = deviceInformationCollection[midiOutPortListBox.SelectedIndex];

            if (devInfo == null)
            {
                return;
            }

            midiOutPort = await MidiOutPort.FromIdAsync(devInfo.Id);

            if (midiOutPort == null)
            {
                System.Diagnostics.Debug.WriteLine("Unable to create MidiOutPort from output device");
                return;
            }

        }

        private async void midiInPortListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var deviceInformationCollection = inputDeviceWatcher.DeviceInformationCollection;

            if (deviceInformationCollection == null)
            {
                return;
            }

            DeviceInformation devInfo = deviceInformationCollection[midiInPortListBox.SelectedIndex];

            if (devInfo == null)
            {
                return;
            }

            midiInPort1 = await MidiInPort.FromIdAsync(devInfo.Id);

            if (midiInPort1 == null)
            {
                System.Diagnostics.Debug.WriteLine("Unable to create MidiInPort from input device");
                return;
            }
            midiInPort1.MessageReceived += MidiInPort_MessageReceived;
        }

        

        private void Rescan_Click(object sender, RoutedEventArgs e)
        {
            RescanPorts();
        }
    }
    
}
