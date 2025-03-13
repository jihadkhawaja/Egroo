﻿using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace jihadkhawaja.chat.shared.Models
{
    [Index(nameof(Username), IsUnique = true)]
    public class User : EntityBase
    {
        [Required]
        public string? Username { get; set; }
        [Required]
        public string? Password { get; set; }
        [NotMapped]
        public string? ConnectionId { get; set; }
        [NotMapped]
        public bool IsOnline { get; set; }
        public bool InCall { get; set; }
        [Required]
        public string? Role { get; set; }
        public DateTimeOffset? LastLoginDate { get; set; }
        public UserDetail? UserDetail { get; set; }
        public UserStorage? UserStorage { get; set; }
        public UserSecurity? UserSecuriy { get; set; }
        public UserFeedback? UserFeedback { get; set; }

        public UserDetail? GetPublicDetail()
        {
            return new UserDetail
            {
                DisplayName = UserDetail?.DisplayName
            };
        }

        public UserDetail? GetPrivateDetail()
        {
            return new UserDetail
            {
                FirstName = UserDetail?.FirstName,
                LastName = UserDetail?.LastName,
                DisplayName = UserDetail?.DisplayName,
                Email = UserDetail?.Email,
                PhoneNumber = UserDetail?.PhoneNumber,
                PhoneCountryCode = UserDetail?.PhoneCountryCode,
                Region = UserDetail?.Region,
                Country = UserDetail?.Country,
            };
        }

        public UserStorage? GetStorage()
        {
            return new UserStorage
            {
                AvatarImageBase64 = UserStorage?.AvatarImageBase64,
                CoverImageBase64 = UserStorage?.CoverImageBase64
            };
        }

        public UserStorage? GetAvatar()
        {
            return new UserStorage
            {
                AvatarImageBase64 = UserStorage?.AvatarImageBase64
            };
        }

        public UserStorage? GetCover()
        {
            return new UserStorage
            {
                CoverImageBase64 = UserStorage?.CoverImageBase64
            };
        }
    }

    public class UserDetail
    {
        public enum SexEnum
        {
            NotSpecified = 0,
            Male = 1,
            Female = 2,
            Other = 3
        }
        [Key]
        public Guid UserId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? DisplayName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? PhoneCountryCode { get; set; }
        public string? Region { get; set; }
        public string? Country { get; set; }
        public SexEnum Sex { get; set; }
        public string? Pronounce { get; set; }
        public string? Interests { get; set; }
        public string? ShortDescription { get; set; }
        public string? FullDescription { get; set; }
        public List<string>? SocialLinks { get; set; }

        public List<string> GetInterests()
        {
            return Interests?.Split(',').ToList() ?? new List<string>();
        }

        public string GetFullName()
        {
            return $"{FirstName} {LastName}";
        }

        public string GetDisplayName()
        {
            return string.IsNullOrWhiteSpace(DisplayName) ? GetFullName() : DisplayName;
        }

        public string GetComposedAddress()
        {
            return $"{Region}, {Country}";
        }

        public string GetComposedPhone()
        {
            return $"+{PhoneCountryCode} {PhoneNumber}";
        }

        public string GetSex()
        {
            return Sex.ToString();
        }
    }

    public class UserStorage
    {
        [Key]
        public Guid UserId { get; set; }
        [Base64String]
        public string? AvatarImageBase64 { get; set; }
        [Base64String]
        public string? CoverImageBase64 { get; set; }
    }

    public class UserSecurity
    {
        [Key]
        public Guid UserId { get; set; }
        public bool IsTwoFactorEnabled { get; set; }
    }

    public class UserFeedback : EntityBase
    {
        public Guid UserId { get; set; }
        public string? Text { get; set; }
    }

    public class CallOffer
    {
        public User? Caller { get; set; }
        public User? Callee { get; set; }
        public string SdpOffer { get; set; } = string.Empty;
    }

    public class UserCall
    {
        public List<User>? Users { get; set; }
    }
}
