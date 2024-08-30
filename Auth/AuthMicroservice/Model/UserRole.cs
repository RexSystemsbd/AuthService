namespace AuthMicroservice.Model
{
    public class UserRole:BaseEntity
    {
        public string? UserName {  get; set; }   
        public string RoleName { get; set; }
        public Guid ApplicationId { get; set; }
        public string? ApplicationName { get; set; } 

    }
}
