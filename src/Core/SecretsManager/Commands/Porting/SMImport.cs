namespace Bit.Core.SecretsManager.Commands.Porting;

public class SMImport
{
    public IEnumerable<InnerProject> Projects { get; set; }
    public IEnumerable<InnerSecret> Secrets { get; set; }

    public class InnerProject
    {
        public InnerProject() { }

        public InnerProject(Core.SecretsManager.Entities.Project project)
        {
            Id = project.Id;
            Name = project.Name;
        }

        public Guid Id { get; set; }
        public string Name { get; set; }
    }

    public class InnerSecret
    {
        public InnerSecret() { }

        public InnerSecret(Core.SecretsManager.Entities.Secret secret)
        {
            Id = secret.Id;
            Key = secret.Key;
            Value = secret.Value;
            Note = secret.Note;
            ProjectIds = secret.Projects != null && secret.Projects.Any() ? secret.Projects.Select(p => p.Id) : null;
        }

        public Guid Id { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        public string Note { get; set; }
        public IEnumerable<Guid> ProjectIds { get; set; }
    }
}
