using AuthMicroservice.Model;

namespace AuthMicroservice.Repository
{
    public interface IFeedbackSurveyRepository : IGenericRepository<FeedbackSurvey>
    {
    }

    public class FeedbackSurveyRepository : GenericRepository<FeedbackSurvey>, IFeedbackSurveyRepository
    {
        public FeedbackSurveyRepository(UserDbContext context)
            : base(context)
        {
        }
    }
}
