namespace AuthMicroservice.Model
{
    public class Subscriber : BaseEntity
    {
        public string Email { get; set; }
        public bool IsSubscribed { get; set; }
        public string ApplicationId { get; set; }
    }
}
