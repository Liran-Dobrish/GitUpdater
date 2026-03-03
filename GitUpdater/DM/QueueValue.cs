namespace GitUpdater.DM
{
    public class QueueValue
    {
        public Guid RequestId { get; set; }
        public string RepoUrl { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public RepoType RepoType { get; set; } = RepoType.Generic;
        public List<Update> Updates { get; set; } = new List<Update>();
        public bool Done { get; set; } = false;
    }
}
