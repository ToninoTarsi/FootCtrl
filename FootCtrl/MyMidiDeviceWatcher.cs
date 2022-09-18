using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

namespace FootCtrl
{

    internal class MidiDeviceWatcher
    {
        internal DeviceWatcher deviceWatcher = null;
        internal DeviceInformationCollection deviceInformationCollection = null;
        bool enumerationCompleted = false;
        ListBox portList = null;
        string midiSelector = string.Empty;
        CoreDispatcher coreDispatcher = null;

        private ObservableCollection<BluetoothLEDeviceDisplay> KnownDevices = new ObservableCollection<BluetoothLEDeviceDisplay>();

        /// <summary>
        /// Constructor: Initialize and hook up Device Watcher events
        /// </summary>
        /// <param name="midiSelectorString">MIDI Device Selector</param>
        /// <param name="dispatcher">CoreDispatcher instance, to update UI thread</param>
        /// <param name="portListBox">The UI element to update with list of devices</param>
        internal MidiDeviceWatcher(string midiSelectorString, CoreDispatcher dispatcher, ListBox portListBox)
        {

            KnownDevices.Clear();

            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected", "System.Devices.Aep.Bluetooth.Le.IsConnectable" };
            string aqsAllBluetoothLEDevices = "(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")";


            this.deviceWatcher =
                    DeviceInformation.CreateWatcher(
                        aqsAllBluetoothLEDevices,
                        requestedProperties,
                        DeviceInformationKind.AssociationEndpoint);



            this.portList = portListBox;
            this.midiSelector = aqsAllBluetoothLEDevices;
            this.coreDispatcher = dispatcher;

            this.deviceWatcher.Added += DeviceWatcher_Added;
            this.deviceWatcher.Removed += DeviceWatcher_Removed;
            this.deviceWatcher.Updated += DeviceWatcher_Updated;
            this.deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
        }

        /// <summary>
        /// Destructor: Remove Device Watcher events
        /// </summary>
        ~MidiDeviceWatcher()
        {
            this.deviceWatcher.Added -= DeviceWatcher_Added;
            this.deviceWatcher.Removed -= DeviceWatcher_Removed;
            this.deviceWatcher.Updated -= DeviceWatcher_Updated;
            this.deviceWatcher.EnumerationCompleted -= DeviceWatcher_EnumerationCompleted;
        }

        /// <summary>
        /// Start the Device Watcher
        /// </summary>
        internal void Start()
        {
            if (this.deviceWatcher.Status != DeviceWatcherStatus.Started)
            {
                this.deviceWatcher.Start();
            }
        }

        /// <summary>
        /// Stop the Device Watcher
        /// </summary>
        internal void Stop()
        {
            if (this.deviceWatcher.Status != DeviceWatcherStatus.Stopped)
            {
                this.deviceWatcher.Stop();
            }
        }

        /// <summary>
        /// Get the DeviceInformationCollection
        /// </summary>
        /// <returns></returns>
        internal DeviceInformationCollection GetDeviceInformationCollection()
        {
            return this.deviceInformationCollection;
        }

        /// <summary>
        /// Add any connected MIDI devices to the list
        /// </summary>
        private async void UpdateDevices()
        {
            // Get a list of all MIDI devices
            //string[] requestedProperties = { "System.Devices.Aep.IsConnected" };

            //KnownDevices;

            // If no devices are found, update the ListBox

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
            if (this.enumerationCompleted)
            {
                await coreDispatcher.RunAsync(CoreDispatcherPriority.High, () =>
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

                await coreDispatcher.RunAsync(CoreDispatcherPriority.High, () =>
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
                await coreDispatcher.RunAsync(CoreDispatcherPriority.High, () =>
                {

                    BluetoothLEDeviceDisplay bleDeviceDisplay = FindBluetoothLEDeviceDisplay(deviceInfoUpdate.Id);
                    if (bleDeviceDisplay != null)
                    {
                        // Device is already being displayed - update UX.
                        bleDeviceDisplay.Update(deviceInfoUpdate);
                        return;
                    }
                    // Update the device list

                    UpdateDevices();
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

            //Debug.WriteLine($"{KnownDevices.Count} devices found. Enumeration completed.");


            this.enumerationCompleted = true;
            await coreDispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {

                // Update the device list
                UpdateDevices();
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
