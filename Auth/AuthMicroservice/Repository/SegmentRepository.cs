using AuthMicroservice.Model;

namespace AuthMicroservice.Repository
{
    public interface ISegmentRepository : IGenericRepository<Segment>
    {
    }

    public class SegmentRepository : GenericRepository<Segment>, ISegmentRepository
    {
        public SegmentRepository(UserDbContext context)
            : base(context)
        {
        }
    }
}
