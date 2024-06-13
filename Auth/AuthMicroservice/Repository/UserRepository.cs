using AuthMicroservice.Model;

namespace AuthMicroservice.Repository
{
    public interface IUserRepository : IGenericRepository<User>
    {

    }
    public class UserRepository : GenericRepository<User>, IUserRepository
    {
        public UserRepository(UserDbContext context) : base(context)
        {
        }
    }
}
