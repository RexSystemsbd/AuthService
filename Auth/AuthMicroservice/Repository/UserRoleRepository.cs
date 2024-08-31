using AuthMicroservice.Model;

namespace AuthMicroservice.Repository
{
    public interface IUserRoleRepository : IGenericRepository<UserRole>
    {

    }
    public class UserRoleRepository : GenericRepository<UserRole>, IUserRoleRepository
    {
        public UserRoleRepository(UserDbContext context) : base(context)
        {
          
        }
    }
}