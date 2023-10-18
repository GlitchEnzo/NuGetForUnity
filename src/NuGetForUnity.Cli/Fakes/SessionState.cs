namespace UnityEditor
{
    internal static class SessionState
    {
        internal static string GetString(string key, string defaultValue)
        {
            return key == "IsRunningInUnity" ? "false" : null;
        }

        internal static void SetString(string key, object value)
        {
            // do nothing
        }
    }
}
