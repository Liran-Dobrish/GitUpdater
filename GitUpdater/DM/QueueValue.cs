namespace GitUpdater.DM
{
    public enum QueueStatus
    {
        New,
        InProgress,
        Done
    }
    public class QueueValue
    {
        public Guid RequestId { get; set; }
        public string RepoUrl { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public RepoType RepoType { get; set; } = RepoType.Generic;
        public List<Update> Updates { get; set; } = new List<Update>();
        public bool Done { get; set; } = false;
    }

    public class QueueValues
    {
        public List<QueueValue> Values { get; set; } = new List<QueueValue>();
        public QueueStatus Status { get; set; } = QueueStatus.New;
    }
}
