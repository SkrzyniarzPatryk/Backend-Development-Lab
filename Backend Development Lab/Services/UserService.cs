using Backend_Development_Lab.Interfaces;
using Backend_Development_Lab.Models;
using Microsoft.AspNetCore.Identity;
using System.Collections.Concurrent;

namespace Backend_Development_Lab.Services
{
    public class UserService : IUserService
    {
        private static readonly ConcurrentDictionary<Guid, User> _users = new ConcurrentDictionary<Guid, User>();
       // private static readonly ConcurrentDictionary<string, Guid> _usernameIndex = new ConcurrentDictionary<string, Guid>();
        private static readonly ConcurrentDictionary<string, Guid> _emailIndex = new ConcurrentDictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, Guid> _externalIdIndex = new ConcurrentDictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        static UserService()
        {
            var user1 = new User
            {
                Id = Guid.NewGuid(),
                Username = "patryk33",
                Email = "patryk@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass12")
            };
            var user2 = new User
            {
                Id = Guid.NewGuid(),
                Username = "mateusz33",
                Email = "mateusz@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass12")
            };

            _users.TryAdd(user1.Id, user1);
            //_usernameIndex.TryAdd(user1.Username, user1.Id);
            _emailIndex.TryAdd(user1.Email, user1.Id);

            _users.TryAdd(user2.Id, user2);
           // _usernameIndex.TryAdd(user2.Username, user2.Id);
            _emailIndex.TryAdd(user2.Email, user2.Id);
        }

        //public Task<User?> GetUserByUsernameAsync(string username)
        //{
        //    if (_usernameIndex.TryGetValue(username, out Guid userId))
        //    {
        //        _users.TryGetValue(userId, out User? user);
        //        return Task.FromResult(user);
        //    }
        //    return Task.FromResult<User?>(null);
        //}

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

        //Stara wersja rejestracji użytkownika bez oauth2
        //public async Task<User?> RegisterUserAsync(string username, string email, string password)
        //{
        //    // Sprawdź, czy użytkownik już istnieje (po nazwie lub emailu)
        //    if (await GetUserByUsernameAsync(username) != null || await GetUserByEmailAsync(email) != null)
        //    {
        //        return null; // Użytkownik już istnieje
        //    }

        //    // Hashuj hasło
        //    string passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

        //    var newUser = new User
        //    {
        //        Id = Guid.NewGuid(),
        //        Username = username,
        //        Email = email,
        //        PasswordHash = passwordHash
        //    };

        //    // Dodaj do główného słownika i indeksów
        //    // Używamy TryAdd dla bezpieczeństwa wątków, chociaż sprawdziliśmy wyżej
        //    if (_users.TryAdd(newUser.Id, newUser) &&
        //       _usernameIndex.TryAdd(newUser.Username, newUser.Id) &&
        //       _emailIndex.TryAdd(newUser.Email, newUser.Id))
        //    {
        //        return newUser;
        //    }
        //    else
        //    {
        //        // W razie problemów z współbieżnością, wycofaj częściowe dodanie (rzadkie, ale możliwe)
        //        _users.TryRemove(newUser.Id, out _);
        //        _usernameIndex.TryRemove(newUser.Username, out _);
        //        _emailIndex.TryRemove(newUser.Email, out _);
        //        // Można tu dodać logowanie błędu
        //        return null; // Błąd rejestracji
        //    }
        //}

        public async Task<User?> RegisterUserAsync(string username, string email, string? password, string? externalProvider = null, string? externalId = null)
        {
            // Sprawdź, czy użytkownik z tym zewnętrznym ID już istnieje
            if (!string.IsNullOrEmpty(externalProvider) && !string.IsNullOrEmpty(externalId))
            {
                if (await GetUserByExternalIdAsync(externalProvider, externalId) != null)
                {
                    // Zwykle nie powinno się zdarzyć, jeśli sprawdzamy przed wywołaniem, ale na wszelki wypadek
                    return null; // Użytkownik z tym zewnętrznym ID już istnieje
                }
                if (await GetUserByEmailAsync(email) != null)
                {
                    return null;
                }
            }
            else // Rejestracja lokalna - sprawdź nazwę użytkownika i email
            {
                if (await GetUserByEmailAsync(email) != null)
                {
                    return null; // Użytkownik lokalny już istnieje
                }
                if (string.IsNullOrEmpty(password)) // Hasło wymagane dla lokalnej rejestracji
                {
                    // Można rzucić wyjątek lub zwrócić null
                    throw new ArgumentNullException(nameof(password), "Password is required for local registration.");
                }
            }

            // Hashuj hasło tylko jeśli zostało podane (rejestracja lokalna)
            string? passwordHash = !string.IsNullOrEmpty(password) ? BCrypt.Net.BCrypt.HashPassword(password) : null;

            var newUser = new User
            {
                Id = Guid.NewGuid(),
                Username = username, // Można by tu użyć email lub części emaila jeśli username koliduje
                Email = email,
                PasswordHash = passwordHash,
                ExternalProvider = externalProvider,
                ExternalId = externalId
            };

            // Dodaj do słowników i indeksów
            bool externalIndexAdded = true;
            if (!string.IsNullOrEmpty(externalProvider) && !string.IsNullOrEmpty(externalId))
            {
                externalIndexAdded = _externalIdIndex.TryAdd($"{externalProvider}:{externalId}", newUser.Id);
            }

            if (_users.TryAdd(newUser.Id, newUser) &&
                _emailIndex.TryAdd(newUser.Email, newUser.Id) &&      // Uwaga na potencjalne kolizje email
                externalIndexAdded)
            {
                return newUser;
            }
            else
            {
                // Wycofanie w razie problemów
                _users.TryRemove(newUser.Id, out _);
                _emailIndex.TryRemove(newUser.Email, out _);
                if (!string.IsNullOrEmpty(externalProvider) && !string.IsNullOrEmpty(externalId))
                {
                    _externalIdIndex.TryRemove($"{externalProvider}:{externalId}", out _);
                }
                return null; // Błąd rejestracji
            }
        }

        public Task<User?> GetUserByExternalIdAsync(string provider, string externalId)
        {
            string key = $"{provider}:{externalId}";
            if (_externalIdIndex.TryGetValue(key, out Guid userId))
            {
                _users.TryGetValue(userId, out User? user);
                return Task.FromResult(user);
            }
            return Task.FromResult<User?>(null);
        }

        public Task<List<User>> GetAllUsersAsync()
        {
            // Zwróć listę użytkowników
            return Task.FromResult(_users.Values.ToList());
        }
    }
}
