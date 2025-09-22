using AuthMicroservice.Model;

public class Application : BaseEntity
{
    public string Name { get; set; }
    public string? Description { get; set; }
    public string AppKey { get; set; }
    public string AppSecret { get; set; }

    public string RedirectUri { get; set; }
    public string ContactEmail { get; set; }

}


