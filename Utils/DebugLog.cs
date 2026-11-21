using System.IO;
using TaleWorlds.Library;

namespace CasusBelli
{
    /// <summary>
    /// Logger compartilhado para debug do mod CasusBelli
    /// Escreve em Documents/Mount and Blade II Bannerlord/Logs/casusbelli_debug.log
    /// </summary>
    public static class DebugLog
    {
        private static string LogPath => System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
            "Mount and Blade II Bannerlord",
            "Logs",
            "casusbelli_debug.log");

        public static void Log(string message)
        {
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(LogPath));
                System.IO.File.AppendAllText(LogPath, System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message + System.Environment.NewLine);
            }
            catch (System.Exception)
            {
                // Silencioso
            }
        }
    }
}