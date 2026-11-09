namespace KongZuo
{
    // 跨场景的剧情ID，必须统一命名；不要把剧情状态写在单个道具的私有变量里。
    public static class KZFlags
    {
        public const string Worksheet = "WORKSHEET_VIEWED";
        public const string Door = "FRONT_DOOR_OPENED";
        public const string Phone = "CALL_DONE";
        public const string Seat = "SEAT_INSPECTED";
        public const string Register = "REGISTER_READ";
        public const string AnomalyStarted = "EVT_ANOMALY_STARTED";
        public const string AnomalyResolved = "EVT_ANOMALY_RESOLVED";
        public const string Program = "PROGRAM_FOUND";
        public const string PrologueDone = "PROLOGUE_DONE";
        public const string Ritual = "RITUAL_READ";
        public const string Photo = "ZHOU_IDENTIFIED";
        public const string Ticket = "TICKETS_OBTAINED";
        public const string EchoStarted = "ECHO01_STARTED";
        public const string EchoFinished = "ECHO01_FINISHED";
        public const string ChapterOneDone = "CHAPTER1_DONE";
        public const string AudienceVisited = "AUDIENCE_VISITED";
        public const string AudienceLeft = "AUDIENCE_LEFT_AFTER_VISIT";
        public const string ChapterOneStarted = "CHAPTER1_STARTED";
        public const string PhotoAligned = "PHOTO_ALIGNED";
        public const string RelicIdentified = "RELIC_IDENTIFIED";
        public const string WorkRecord = "WORK_RECORD_READ";
    }
}
