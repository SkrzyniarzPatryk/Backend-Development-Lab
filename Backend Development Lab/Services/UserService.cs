using Backend_Development_Lab.Interfaces;
using Backend_Development_Lab.Models;
using Microsoft.AspNetCore.Identity;
using System.Collections.Concurrent;

namespace Backend_Development_Lab.Services
{
    public class UserService : IUserService
    {
        // Używamy ConcurrentDictionary zamiast List dla bezpieczeństwa wątków
        // Kluczem będzie Guid (Id użytkownika)
        private static readonly ConcurrentDictionary<Guid, User> _users = new ConcurrentDictionary<Guid, User>();
        private static readonly ConcurrentDictionary<string, Guid> _usernameIndex = new ConcurrentDictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, Guid> _emailIndex = new ConcurrentDictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
          

        public Task<User?> GetUserByUsernameAsync(string username)
        {
            if (_usernameIndex.TryGetValue(username, out Guid userId))
            {
                _users.TryGetValue(userId, out User? user);
                return Task.FromResult(user);
            }
            return Task.FromResult<User?>(null);
        }

        public Task<User?> GetUserByEmailAsync(string email)
        {
            if (_emailIndex.TryGetValue(email, out Guid userId))
            {
                _users.TryGetValue(userId, out User? user);
                return Task.FromResult(user);
            }
            return Task.FromResult<User?>(null);
        }

        public Task<User?> GetUserByIdAsync(Guid id)
        {
            _users.TryGetValue(id, out User? user);
            return Task.FromResult(user);
        }

        public async Task<User?> RegisterUserAsync(string username, string email, string password)
        {
            // Sprawdź, czy użytkownik już istnieje (po nazwie lub emailu)
            if (await GetUserByUsernameAsync(username) != null || await GetUserByEmailAsync(email) != null)
            {
                return null; // Użytkownik już istnieje
            }

            // Hashuj hasło
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            var newUser = new User
            {
                Id = Guid.NewGuid(),
                Username = username,
                Email = email,
                PasswordHash = passwordHash
            };

            // Dodaj do główného słownika i indeksów
            // Używamy TryAdd dla bezpieczeństwa wątków, chociaż sprawdziliśmy wyżej
            if (_users.TryAdd(newUser.Id, newUser) &&
               _usernameIndex.TryAdd(newUser.Username, newUser.Id) &&
               _emailIndex.TryAdd(newUser.Email, newUser.Id))
            {
                return newUser;
            }
            else
            {
                // W razie problemów z współbieżnością, wycofaj częściowe dodanie (rzadkie, ale możliwe)
                _users.TryRemove(newUser.Id, out _);
                _usernameIndex.TryRemove(newUser.Username, out _);
                _emailIndex.TryRemove(newUser.Email, out _);
                // Można tu dodać logowanie błędu
                return null; // Błąd rejestracji
            }
        }
    }
}
