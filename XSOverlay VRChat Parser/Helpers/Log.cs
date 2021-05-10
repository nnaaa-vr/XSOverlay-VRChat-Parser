using System;
using System.Collections.Generic;

namespace XSOverlay_VRChat_Parser.Helpers
{
    public static class Log
    {
        public delegate void EndpointAction(string message);
        private static Dictionary<string, EndpointAction> LoggingActions = new Dictionary<string, EndpointAction>();

        public static void RegisterLoggingAction(string identifier, EndpointAction action) => LoggingActions.Add(identifier, action);
        public static void UnregisterLoggingAction(string identifier)
        {
            if (LoggingActions.ContainsKey(identifier))
                LoggingActions.Remove(identifier);
        }

        private static void LogInternal(LogEventType type, string message)
        {
            DateTime dt = DateTime.Now;

            string typeName = Enum.GetName(typeof(LogEventType), type).ToUpper();
            string logMessage = $"[{dt.Hour:00}:{dt.Minute:00}:{dt.Second:00}] <{typeName}> {message}\r\n";

            foreach (KeyValuePair<string, EndpointAction> kvp in LoggingActions)
                kvp.Value(logMessage);
        }

        public static void Error(string message)
        {
            LogInternal(LogEventType.Error, message);
        }

        public static void Info(string message)
        {
            LogInternal(LogEventType.Info, message);
        }

        public static void Event(string message)
        {
            LogInternal(LogEventType.Event, message);
        }

        public static void Update(string message)
        {
            LogInternal(LogEventType.Update, message);
        }

        public static void Exception(Exception ex)
        {
            LogInternal(LogEventType.Error, $"{ex.Message}\r\n{ex.InnerException}\r\n{ex.StackTrace}");
        }

        public static void Unspecified(LogEventType type, string message)
        {
            LogInternal(type, message);
        }
    }
}
