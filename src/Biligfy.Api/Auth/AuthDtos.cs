using System.ComponentModel.DataAnnotations;
using Biligfy.Api.Domain;

namespace Biligfy.Api.Auth;

// Not: Attribute'lar parametre üzerinde olmalı (property: hedefi .NET 8 MVC doğrulamasında hata verir).
public sealed record RegisterRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(8), MaxLength(128)] string Password,
    [Required, MaxLength(100)] string DisplayName,
    AccountType AccountType = AccountType.Individual,
    [RegularExpression("^(tr|en)$", ErrorMessage = "PreferredLanguage 'tr' veya 'en' olmalı.")]
    string PreferredLanguage = "tr");

public sealed record LoginRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MaxLength(128)] string Password);

public sealed record AuthResponse(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc,
    UserResponse User);

public sealed record UserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string PreferredLanguage,
    AccountType AccountType);
