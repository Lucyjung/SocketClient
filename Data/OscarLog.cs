namespace Receiver.Data
{
    class OscarLog
    {
        public string hostName { get; set; }
        public string status { get; set; }
        public int timestamps { get; set; }
        public int statusCount { get; set; }
        public float cpuUsage { get; set; }
        public float ramUsage { get; set; }
        public float diskUsage { get; set; }
        public float? cpu1 { get; set; }
        public float? cpu2 { get; set; }
        public float? cpu3 { get; set; }
        public float? cpu4 { get; set; }
        public float? cpu5 { get; set; }
        public float? cpu6 { get; set; }
        public float? cpu7 { get; set; }
        public float? cpu8 { get; set; }
        public float? cpu9 { get; set; }
        public float? cpu10 { get; set; }
        public float? cpu11 { get; set; }
        public float? cpu12 { get; set; }
        public float? cpu13 { get; set; }
        public float? cpu14 { get; set; }
        public float? cpu15 { get; set; }
        public float? cpu16 { get; set; }
    }
    public class OscarResponse
    {
        public bool success;
        public OscarCommand command;
    }
    public class OscarCommand
    {
        public bool success;
        public string id;
        public string process;
        public string userPass;
        public string parameter;
    }
    public class OscarUpdateStatus
    {
        public string _id;
        public string status;
    }
}
