using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.UI.WinUI;
using System;

namespace Glance.Media.WinUI;

public sealed partial class AudioVisualizationSettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, MediaSettings settings, IWritableOptions<MediaSettings> writer) :
    ModuleSettingViewModel<MediaSettings, bool>(provider, factory, messenger, disposer, dispatcher, settings, writer, "Media", 10, config => config.ShowAudioVisualization, (config, value) => config.ShowAudioVisualization = value);
