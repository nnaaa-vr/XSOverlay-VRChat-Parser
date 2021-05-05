using System.Collections.Generic;
using XSNotifications;
using XSOverlay_VRChat_Parser.Helpers;

namespace XSOverlay_VRChat_Parser.Models
{
    class NotificationDispatchModel
    {
        public int DurationMilliseconds { get; set; }
        public EventType Type { get; set; }
        public XSNotification Message { get; set; }
        public bool WasGrouped { get; set; }
        public List<NotificationDispatchModel> GroupedNotifications { get; set; }

        public NotificationDispatchModel()
        {
            WasGrouped = false;
            GroupedNotifications = new List<NotificationDispatchModel>();
        }
    }
}
