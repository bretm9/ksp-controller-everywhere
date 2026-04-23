using UnityEngine;

namespace ControllerEverywhere
{
    internal static class Log
    {
        private const string Prefix = "[ControllerEverywhere] ";
        public static void Info(string msg)  => Debug.Log(Prefix + msg);
        public static void Warn(string msg)  => Debug.LogWarning(Prefix + msg);
        public static void Err(string msg)   => Debug.LogError(Prefix + msg);
    }
}
