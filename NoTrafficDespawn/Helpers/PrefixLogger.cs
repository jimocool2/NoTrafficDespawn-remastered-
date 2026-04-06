using Colossal.Logging;

namespace NoTrafficDespawn.Helpers
{
    internal class PrefixLogger
    {
        private readonly ILog m_Log;
        public string Prefix { get; set; }

        public PrefixLogger(string prefix)
        {
            m_Log = Mod.Instance.Log;
            Prefix = prefix;
        }

        public void Info(string message)
        {
            Log("INFO", message);
        }

        public void Warn(string message)
        {
            Log("WARN", message);
        }

        public void Error(string message)
        {
            Log("ERROR", message);
        }

        public void Debug(string message)
        {
            Log("DEBUG", message);
        }

        private void Log(string level, string message)
        {
            var formattedMessage = $"[{Prefix}] {message}";

            switch (level)
            {
                case "ERROR":
                    m_Log.Error(formattedMessage);
                    break;
                case "WARN":
                    m_Log.Warn(formattedMessage);
                    break;
                case "DEBUG":
                    m_Log.Debug(formattedMessage);
                    break;
                default:
                    m_Log.Info(formattedMessage);
                    break;
            }
        }
    }
}