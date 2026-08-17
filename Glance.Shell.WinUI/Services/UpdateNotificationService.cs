using Elysium.UI.WinUI;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Security;
using System.Text;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Glance.Shell.WinUI;

internal sealed class UpdateNotificationService(AppToastNotifier notifier, ILogger<UpdateNotificationService> logger) :
    IUpdateNotificationService
{
    private readonly ConcurrentDictionary<Guid, ToastNotification> notifications = [];

    public void Show(ToastContent content, Action<string> onActivated)
    {
        if (PackageIdentity.IsExternalLocation)
        {
            ShowPackageNotification(content, onActivated);
            return;
        }

        try
        {
            notifier.Show(content, onActivated);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "The Windows App SDK notification service is unavailable; using the package notification service");
            ShowPackageNotification(content, onActivated);
        }
    }

    private void ShowPackageNotification(ToastContent content, Action<string> onActivated)
    {
        XmlDocument document = new();
        document.LoadXml(BuildXml(content));

        Guid id = Guid.NewGuid();
        ToastNotification notification = new(document);
        void RemoveNotification() => _ = notifications.TryRemove(id, out _);
        notification.Activated += (_, args) =>
        {
            RemoveNotification();
            onActivated(args is ToastActivatedEventArgs activated ? activated.Arguments : content.LaunchArgument);
        };
        notification.Dismissed += (_, _) => RemoveNotification();
        notification.Failed += (_, _) => RemoveNotification();
        notifications[id] = notification;
        ToastNotificationManager.CreateToastNotifier().Show(notification);
    }

    private static string BuildXml(ToastContent content)
    {
        StringBuilder text = new();

        foreach (string line in content.TextLines)
        {
            text.Append("<text>").Append(Escape(line)).Append("</text>");
        }

        StringBuilder actions = new();

        foreach (ToastButton button in content.Buttons)
        {
            actions
                .Append("<action content=\"").Append(Escape(button.Content))
                .Append("\" arguments=\"").Append(Escape(button.Arguments))
                .Append("\" activationType=\"").Append(GetActivationType(button.ActivationType))
                .Append("\" />");
        }

        string actionContent = actions.Length == 0 ? string.Empty : $"<actions>{actions}</actions>";
        return $"<toast launch=\"{Escape(content.LaunchArgument)}\"><visual><binding template=\"ToastGeneric\">{text}</binding></visual>{actionContent}</toast>";
    }

    private static string GetActivationType(ToastButtonActivationType activationType) => activationType switch
    {
        ToastButtonActivationType.Background => "background",
        ToastButtonActivationType.Protocol => "protocol",
        _ => "foreground"
    };

    private static string Escape(string value) => SecurityElement.Escape(value) ?? string.Empty;
}
