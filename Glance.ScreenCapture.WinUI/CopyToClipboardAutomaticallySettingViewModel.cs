using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.UI.WinUI;
using System;

namespace Glance.ScreenCapture.WinUI;

public sealed partial class CopyToClipboardAutomaticallySettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, ScreenCaptureSettings settings, IWritableOptions<ScreenCaptureSettings> writer) :
    ModuleSettingViewModel<ScreenCaptureSettings, bool>(provider, factory, messenger, disposer, dispatcher, settings, writer, "ScreenCapture", 20, config => config.CopyToClipboardAutomatically, (config, value) => config.CopyToClipboardAutomatically = value);
