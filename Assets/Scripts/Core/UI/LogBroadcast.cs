using System;
using UnityEngine;

namespace ProjectTowerRpg.Core
{
    public enum LogChannel
    {
        System,     
        Combat,     
        World,      
        Dialogue,   
        Chat        
    }

    public struct LogLine
    {
        public LogChannel Channel;
        public string RawText;
        public string FormattedText; 
    }

    public static class LogBroadcast
    {
        public static event Action<LogLine> OnMessageReceived;
        public static event Action OnClearRequested;

        public static void Send(LogChannel channel, string text)
        {
            string hexColor = channel switch
            {
                LogChannel.System   => "#865f87", 
                LogChannel.Combat   => "#f7768e", 
                LogChannel.World    => "#e0af68", 
                LogChannel.Dialogue => "#7dceff", 
                LogChannel.Chat     => "#bb9af7", 
                _                   => "#c0caf5"  
            };

            var line = new LogLine
            {
                Channel = channel,
                RawText = text,
                FormattedText = "<color=" + hexColor + ">" + text + "</color>"
            };

            OnMessageReceived?.Invoke(line);
        }

        public static void Clear()
        {
            OnClearRequested?.Invoke();
        }
    }
}
