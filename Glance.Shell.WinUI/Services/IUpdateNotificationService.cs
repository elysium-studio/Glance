using Elysium.UI.WinUI;
using System;

namespace Glance.Shell.WinUI;

internal interface IUpdateNotificationService
{
    void Show(ToastContent content, Action<string> onActivated);
}
