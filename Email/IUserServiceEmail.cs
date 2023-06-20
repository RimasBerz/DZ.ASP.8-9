using WebApplication1.Data.Entities;

namespace WebApplication1.Servicies.Email
{
    public interface IUserServiceEmail
    {
        User GetUserById(string userId);
        void UpdateUser(User user);
    }
}
