namespace ArduinoApiHTTP
{
    public static class EventManager
    {
        public static event EventHandler<string> ApiPosted;


        public static void InvokeApi(string msg)
        {
            ApiPosted.Invoke(null, msg);
        }
    }
}
