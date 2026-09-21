using back.Models;
using Microsoft.AspNetCore.Identity;

namespace back.Services
{
    public class PasswordService : IPasswordService
    {
        private readonly PasswordHasher<Users> passwordHasher = new();

        public string HashPassword(Users user, string password)
        {
            return passwordHasher.HashPassword(user, password);
        }

        public bool VerifyPassword(Users user, string password)
        {
            var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
            return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
        }
    }
}
