namespace GitUpdater.DM
{
    public enum RepoType
    {
        Generic,
        AzureRepos,
        GitHub,
        GitLab,
        Bitbucket
    }

    public enum UpdateType
    {
        File,
        Line
    }

    public enum FileType
    {
        Text,
        Json,
        Xml,
        Yaml
    }

    public class Update
    {
        public UpdateType Type { get; set; }
        public string Contents { get; set; }
        public FileType FileType { get; set; } = FileType.Text;
    }

    public class GitUpdateRequest
    {
        public string RepoUrl { get; set; }
        public string Token { get; set; }
        public RepoType Type { get; set; } = RepoType.Generic;
        public List<Update> Updates { get; set; } = new List<Update>();

    }
}
