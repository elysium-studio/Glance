using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace Glance.DevicePresence.WinUI;

public sealed class WindowsDevicePresenceService :
    IDevicePresenceService,
    IDisposable
{
    private const string BatteryLifeProperty = "System.Devices.BatteryLife";
    private const string CategoryProperty = "System.Devices.Aep.Category";
    private const string ClassMajorProperty = "System.Devices.Aep.Bluetooth.Cod.Major";
    private const string ClassMinorProperty = "System.Devices.Aep.Bluetooth.Cod.Minor";
    private const string ContainerIdProperty = "System.Devices.Aep.ContainerId";
    private const string DeviceAddressProperty = "System.Devices.Aep.DeviceAddress";
    private const string DisplayNameProperty = "System.ItemNameDisplay";
    private const string LeAppearanceCategoryProperty = "System.Devices.Aep.Bluetooth.Le.Appearance.Category";
    private const string LeAppearanceSubcategoryProperty = "System.Devices.Aep.Bluetooth.Le.Appearance.Subcategory";
    private static readonly string[] RequestedProperties = [BatteryLifeProperty, CategoryProperty, ClassMajorProperty, ClassMinorProperty, ContainerIdProperty, DeviceAddressProperty, DisplayNameProperty, LeAppearanceCategoryProperty, LeAppearanceSubcategoryProperty];
    private readonly HashSet<DeviceWatcher> completedWatchers = [];
    private readonly HashSet<string> activeBatteryReads = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ObservedDevice> devices = new(StringComparer.OrdinalIgnoreCase);
    private readonly object gate = new();
    private readonly List<DeviceWatcher> watchers = [];
    private readonly Dictionary<DeviceWatcher, BluetoothTransport> watcherTransports = [];
    private readonly Timer batteryRefreshTimer;
    private int expectedWatcherCount;
    private bool isDisposed;
    private bool isReady;

    public WindowsDevicePresenceService()
    {
        TryAddWatcher(BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected), BluetoothTransport.Classic);
        TryAddWatcher(BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected), BluetoothTransport.LowEnergy);
        expectedWatcherCount = watchers.Count;

        foreach (DeviceWatcher watcher in watchers.ToArray())
        {
            TryStartWatcher(watcher);
        }

        lock (gate)
        {
            isReady = completedWatchers.Count == expectedWatcherCount;
        }

        batteryRefreshTimer = new Timer(_ => RefreshBatteryLevels(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public event EventHandler? DevicesChanged;

    public bool IsReady
    {
        get
        {
            lock (gate)
            {
                return isReady;
            }
        }
    }

    public IReadOnlyList<ConnectedBluetoothDevice> GetConnectedDevices()
    {
        lock (gate)
        {
            return devices.Values.GroupBy(device => device.Identity, StringComparer.OrdinalIgnoreCase).Select(Merge).OrderBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        }
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        batteryRefreshTimer.Dispose();

        foreach (DeviceWatcher watcher in watchers)
        {
            watcher.Added -= HandleAdded;
            watcher.Updated -= HandleUpdated;
            watcher.Removed -= HandleRemoved;
            watcher.EnumerationCompleted -= HandleEnumerationCompleted;
            watcher.Stopped -= HandleStopped;

            if (watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
            {
                watcher.Stop();
            }
        }

        watchers.Clear();
        watcherTransports.Clear();
    }

    private void TryAddWatcher(string selector, BluetoothTransport transport)
    {
        try
        {
            DeviceWatcher watcher = DeviceInformation.CreateWatcher(selector, RequestedProperties, DeviceInformationKind.AssociationEndpoint);
            watcher.Added += HandleAdded;
            watcher.Updated += HandleUpdated;
            watcher.Removed += HandleRemoved;
            watcher.EnumerationCompleted += HandleEnumerationCompleted;
            watcher.Stopped += HandleStopped;
            watchers.Add(watcher);
            watcherTransports.Add(watcher, transport);
        }
        catch (Exception)
        {
        }
    }

    private void TryStartWatcher(DeviceWatcher watcher)
    {
        try
        {
            watcher.Start();
        }
        catch (Exception)
        {
            watcher.Added -= HandleAdded;
            watcher.Updated -= HandleUpdated;
            watcher.Removed -= HandleRemoved;
            watcher.EnumerationCompleted -= HandleEnumerationCompleted;
            watcher.Stopped -= HandleStopped;
            watchers.Remove(watcher);
            watcherTransports.Remove(watcher);
            expectedWatcherCount--;
        }
    }

    private void HandleAdded(DeviceWatcher sender, DeviceInformation information)
    {
        BluetoothTransport transport = watcherTransports.GetValueOrDefault(sender);
        ObservedDevice device = new(transport, information.Id, information.Name, information.Properties);
        bool notify;

        lock (gate)
        {
            devices[GetStorageKey(transport, information.Id)] = device;
            notify = isReady;
        }

        if (notify)
        {
            DevicesChanged?.Invoke(this, EventArgs.Empty);
        }

        if (transport == BluetoothTransport.LowEnergy)
        {
            RefreshBatteryLevel(GetStorageKey(transport, information.Id), information.Id);
        }
    }

    private void HandleUpdated(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        BluetoothTransport transport = watcherTransports.GetValueOrDefault(sender);
        bool notify;

        lock (gate)
        {
            string key = GetStorageKey(transport, update.Id);

            if (devices.TryGetValue(key, out ObservedDevice? device))
            {
                device.Update(update.Properties);
            }

            notify = isReady;
        }

        if (notify)
        {
            DevicesChanged?.Invoke(this, EventArgs.Empty);
        }

        if (transport == BluetoothTransport.LowEnergy)
        {
            RefreshBatteryLevel(GetStorageKey(transport, update.Id), update.Id);
        }
    }

    private void HandleRemoved(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        BluetoothTransport transport = watcherTransports.GetValueOrDefault(sender);
        bool removed;
        bool notify;

        lock (gate)
        {
            removed = devices.Remove(GetStorageKey(transport, update.Id));
            notify = isReady;
        }

        if (removed && notify)
        {
            DevicesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void HandleEnumerationCompleted(DeviceWatcher sender, object args) =>
        CompleteWatcher(sender);

    private void HandleStopped(DeviceWatcher sender, object args) =>
        CompleteWatcher(sender);

    private void CompleteWatcher(DeviceWatcher watcher)
    {
        bool notify = false;

        lock (gate)
        {
            completedWatchers.Add(watcher);

            if (!isReady && completedWatchers.Count == expectedWatcherCount)
            {
                isReady = true;
                notify = true;
            }
        }

        if (notify)
        {
            DevicesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static ConnectedBluetoothDevice Merge(IGrouping<string, ObservedDevice> group)
    {
        ObservedDevice[] endpoints = group.ToArray();
        string name = endpoints.Select(endpoint => endpoint.Name).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        BluetoothDeviceKind kind = endpoints.Select(endpoint => endpoint.Kind).FirstOrDefault(value => value != BluetoothDeviceKind.Bluetooth);
        byte? batteryLevel = endpoints.Select(endpoint => endpoint.BatteryLevel).FirstOrDefault(value => value.HasValue);
        return new ConnectedBluetoothDevice(group.Key, name, kind, batteryLevel);
    }

    private static string GetStorageKey(BluetoothTransport transport, string id) =>
        $"{transport}:{id}";

    private void RefreshBatteryLevels()
    {
        KeyValuePair<string, string>[] endpoints;

        lock (gate)
        {
            endpoints = devices.Where(pair => pair.Value.Transport == BluetoothTransport.LowEnergy).Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value.Id)).ToArray();
        }

        foreach ((string key, string id) in endpoints)
        {
            RefreshBatteryLevel(key, id);
        }
    }

    private void RefreshBatteryLevel(string key, string id)
    {
        lock (gate)
        {
            if (isDisposed || !activeBatteryReads.Add(key))
            {
                return;
            }
        }

        _ = RefreshBatteryLevelAsync(key, id);
    }

    private async Task RefreshBatteryLevelAsync(string key, string id)
    {
        byte? batteryLevel = null;

        try
        {
            batteryLevel = await ReadBatteryLevelAsync(id);
        }
        catch (Exception)
        {
        }

        bool notify = false;

        lock (gate)
        {
            activeBatteryReads.Remove(key);

            if (batteryLevel is byte value && devices.TryGetValue(key, out ObservedDevice? device) && device.BatteryLevel != value)
            {
                device.SetBatteryLevel(value);
                notify = isReady;
            }
        }

        if (notify)
        {
            DevicesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static async Task<byte?> ReadBatteryLevelAsync(string id)
    {
        using BluetoothLEDevice? device = await BluetoothLEDevice.FromIdAsync(id);

        if (device is null)
        {
            return null;
        }

        foreach (BluetoothCacheMode cacheMode in new[] { BluetoothCacheMode.Uncached, BluetoothCacheMode.Cached })
        {
            GattDeviceServicesResult servicesResult = await device.GetGattServicesForUuidAsync(GattServiceUuids.Battery, cacheMode);

            if (servicesResult.Status != GattCommunicationStatus.Success)
            {
                continue;
            }

            GattDeviceService[] services = servicesResult.Services.ToArray();

            try
            {
                foreach (GattDeviceService service in services)
                {
                    GattCharacteristicsResult characteristicsResult = await service.GetCharacteristicsForUuidAsync(GattCharacteristicUuids.BatteryLevel, cacheMode);

                    if (characteristicsResult.Status != GattCommunicationStatus.Success)
                    {
                        continue;
                    }

                    foreach (GattCharacteristic characteristic in characteristicsResult.Characteristics)
                    {
                        GattReadResult readResult = await characteristic.ReadValueAsync(cacheMode);

                        if (readResult.Status == GattCommunicationStatus.Success && readResult.Value.Length > 0)
                        {
                            using DataReader reader = DataReader.FromBuffer(readResult.Value);
                            byte value = reader.ReadByte();
                            return value <= 100 ? value : null;
                        }
                    }
                }
            }
            finally
            {
                foreach (GattDeviceService service in services)
                {
                    service.Dispose();
                }
            }
        }

        return null;
    }

    private sealed class ObservedDevice
    {
        private readonly Dictionary<string, object> properties;
        private byte? supplementalBatteryLevel;

        public ObservedDevice(
            BluetoothTransport transport,
            string id,
            string name,
            IReadOnlyDictionary<string, object> properties)
        {
            Transport = transport;
            Id = id;
            Name = name;
            this.properties = new Dictionary<string, object>(properties, StringComparer.OrdinalIgnoreCase);
        }

        public BluetoothTransport Transport { get; }

        public string Id { get; }

        public string Name { get; private set; }

        public string Identity => GetIdentity(properties, Id);

        public BluetoothDeviceKind Kind => GetKind(properties, Name);

        public byte? BatteryLevel => GetBatteryLevel(properties) ?? supplementalBatteryLevel;

        public void SetBatteryLevel(byte value) =>
            supplementalBatteryLevel = value;

        public void Update(IReadOnlyDictionary<string, object> updates)
        {
            foreach ((string key, object value) in updates)
            {
                properties[key] = value;
            }

            if (updates.TryGetValue(DisplayNameProperty, out object? displayName) && displayName is string updatedName && !string.IsNullOrWhiteSpace(updatedName))
            {
                Name = updatedName;
            }
        }
    }

    private static string GetIdentity(IReadOnlyDictionary<string, object> properties, string fallbackId)
    {
        string? address = GetString(properties, DeviceAddressProperty);

        if (!string.IsNullOrWhiteSpace(address))
        {
            return address.Replace(":", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        }

        string? containerId = GetString(properties, ContainerIdProperty);
        return string.IsNullOrWhiteSpace(containerId) ? fallbackId : containerId;
    }

    private static byte? GetBatteryLevel(IReadOnlyDictionary<string, object> properties)
    {
        if (!properties.TryGetValue(BatteryLifeProperty, out object? value) || value is null)
        {
            return null;
        }

        try
        {
            byte batteryLevel = Convert.ToByte(value);
            return batteryLevel <= 100 ? batteryLevel : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static BluetoothDeviceKind GetKind(IReadOnlyDictionary<string, object> properties, string name)
    {
        string categories = string.Join(' ', GetStrings(properties, CategoryProperty));
        string searchable = $"{categories} {name}".ToLowerInvariant();

        if (searchable.Contains("head") || searchable.Contains("earbud") || searchable.Contains("airpod") || searchable.Contains("speaker") || searchable.Contains("audio"))
        {
            return BluetoothDeviceKind.Audio;
        }

        if (searchable.Contains("keyboard"))
        {
            return BluetoothDeviceKind.Keyboard;
        }

        if (searchable.Contains("mouse"))
        {
            return BluetoothDeviceKind.Mouse;
        }

        if (searchable.Contains("controller") || searchable.Contains("gamepad") || searchable.Contains("xbox") || searchable.Contains("dualsense"))
        {
            return BluetoothDeviceKind.GameController;
        }

        if (searchable.Contains("phone"))
        {
            return BluetoothDeviceKind.Phone;
        }

        ushort major = GetUInt16(properties, ClassMajorProperty);
        ushort minor = GetUInt16(properties, ClassMinorProperty);

        return major switch
        {
            1 => BluetoothDeviceKind.Computer,
            2 => BluetoothDeviceKind.Phone,
            4 => BluetoothDeviceKind.Audio,
            5 when (minor & 0x30) == 0x10 => BluetoothDeviceKind.Keyboard,
            5 when (minor & 0x30) == 0x20 => BluetoothDeviceKind.Mouse,
            5 when (minor & 0x0F) is 1 or 2 => BluetoothDeviceKind.GameController,
            7 => BluetoothDeviceKind.Wearable,
            _ => GetLeKind(properties)
        };
    }

    private static BluetoothDeviceKind GetLeKind(IReadOnlyDictionary<string, object> properties)
    {
        ushort category = GetUInt16(properties, LeAppearanceCategoryProperty);
        ushort subcategory = GetUInt16(properties, LeAppearanceSubcategoryProperty);

        return category switch
        {
            1 => BluetoothDeviceKind.Phone,
            2 => BluetoothDeviceKind.Computer,
            3 or 7 => BluetoothDeviceKind.Wearable,
            15 when subcategory == 1 => BluetoothDeviceKind.Keyboard,
            15 when subcategory is 2 or 9 => BluetoothDeviceKind.Mouse,
            15 when subcategory is 3 or 4 => BluetoothDeviceKind.GameController,
            _ => BluetoothDeviceKind.Bluetooth
        };
    }

    private static string? GetString(IReadOnlyDictionary<string, object> properties, string key) =>
        properties.TryGetValue(key, out object? value) ? value?.ToString() : null;

    private static IEnumerable<string> GetStrings(IReadOnlyDictionary<string, object> properties, string key)
    {
        if (!properties.TryGetValue(key, out object? value) || value is null)
        {
            return [];
        }

        if (value is string text)
        {
            return [text];
        }

        return value is IEnumerable values
            ? values.Cast<object>().Select(item => item?.ToString()).OfType<string>()
            : [];
    }

    private static ushort GetUInt16(IReadOnlyDictionary<string, object> properties, string key)
    {
        if (!properties.TryGetValue(key, out object? value) || value is null)
        {
            return 0;
        }

        try
        {
            return Convert.ToUInt16(value);
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private enum BluetoothTransport
    {
        Classic,
        LowEnergy
    }
}
