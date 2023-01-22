using System;
using Styx;
using Styx.Common.Helpers;
using System.Windows.Media;
using Styx.Common;
namespace KBA.Helpers
{
    internal static class Logger
    {

        private static readonly CapacityQueue<string> LogQueue = new CapacityQueue<string>(5);
        static void Output(LogLevel level, Color color, string format, params object[] args)
        {
            if (LogQueue.Contains(string.Format(format, args))) return;
            LogQueue.Enqueue(string.Format(format, args));

            Logging.Write(level, color, string.Format("[{0}]: {1}", "KBA" , format), args);
        }

        public static void InitLog(string format, params object[] args)
        { Output(LogLevel.Normal, Colors.Gainsboro, format, args); }

        public static void ItemLog(string format, params object[] args)
        { if (Styx.Helpers.GlobalSettings.Instance.LogLevel == LogLevel.Diagnostic) Output(LogLevel.Diagnostic, Colors.LawnGreen, format, args); }

        public static void FailLog(string format, params object[] args)
        { Output(LogLevel.Normal, Colors.GreenYellow, format, args); }

        public static void InfoLog(string format, params object[] args)
        { Output(LogLevel.Normal, Colors.CornflowerBlue, format, args); }

        public static void CombatLog(string format, params object[] args)
        { if (Styx.Helpers.GlobalSettings.Instance.LogLevel == LogLevel.Diagnostic) Output(LogLevel.Diagnostic, Colors.Pink, format, args); }

        public static void DebugLog(string format, params object[] args)
        { if (Styx.Helpers.GlobalSettings.Instance.LogLevel == LogLevel.Diagnostic) Output(LogLevel.Diagnostic, Colors.DarkGoldenrod, format, args); }

        private static WaitTimer logTimer = new WaitTimer(TimeSpan.FromMilliseconds(1000));
    }

}
