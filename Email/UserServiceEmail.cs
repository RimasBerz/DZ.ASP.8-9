using WebApplication1.Data;
using System.Linq;
using WebApplication1.Data.Entities;

namespace WebApplication1.Servicies.Email
{
    public class UserServiceEmail : IUserServiceEmail
    {
        private readonly DataContext _dbContext;

        public UserServiceEmail(DataContext dbContext)
        {
            _dbContext = dbContext;
        }

        public User GetUserById(string userId)
        {
            Guid parsedUserId;
            if (!Guid.TryParse(userId, out parsedUserId))
            {
                throw new ArgumentException("Invalid userId format");
            }

            User user = _dbContext.Users.FirstOrDefault(u => u.Id == parsedUserId);

            if (user == null)
            {
                throw new ApplicationException($"User not found for userId: {userId}");
            }

            return user;
        }

        public void UpdateUser(User user)
        {
            User existingUser = _dbContext.Users.FirstOrDefault(u => u.Id == user.Id);

            if (existingUser == null)
            {
                throw new ApplicationException($"User not found for userId: {user.Id}");
            }
            existingUser.Name = user.Name;
            existingUser.Email = user.Email;
            existingUser.ConfirmCode = user.ConfirmCode;
            existingUser.Login = user.Login;
            existingUser.PasswordHash = user.PasswordHash;
            existingUser.Avatar = user.Avatar;
            existingUser.CreatedDt = user.CreatedDt;
            existingUser.DeletedDt = user.DeletedDt;
            _dbContext.SaveChanges();
        }
    }
}
