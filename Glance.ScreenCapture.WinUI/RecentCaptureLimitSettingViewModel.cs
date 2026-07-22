using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.UI.WinUI;
using System;

namespace Glance.ScreenCapture.WinUI;

public sealed partial class RecentCaptureLimitSettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, ScreenCaptureSettings settings, IWritableOptions<ScreenCaptureSettings> writer) :
    ModuleSettingViewModel<ScreenCaptureSettings, double>(provider, factory, messenger, disposer, dispatcher, settings, writer, "ScreenCapture", 10, config => config.RecentCaptureLimit, (config, value) => config.RecentCaptureLimit = Math.Clamp(value, 1, 12));
