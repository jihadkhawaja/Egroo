namespace Egroo.Server.Test
{
    [TestClass]
    public class SharedModelsTest
    {
        [TestMethod]
        public void Channel_GetTitle_PrefersExplicitTitle()
        {
            var channel = new Channel { Title = "General", DefaultTitle = "Fallback" };
            Assert.AreEqual("General", channel.GetTitle());
        }

        [TestMethod]
        public void Channel_GetTitle_FallsBackToDefaultTitle()
        {
            var channel = new Channel { Title = "  ", DefaultTitle = "Fallback" };
            Assert.AreEqual("Fallback", channel.GetTitle());
        }

        [TestMethod]
        public void UserDetail_Helpers_ReturnExpectedValues()
        {
            var detail = new UserDetail
            {
                FirstName = "Repo",
                LastName = "Owner",
                DisplayName = "Display",
                Region = "Amman",
                Country = "Jordan",
                PhoneCountryCode = "962",
                PhoneNumber = "123456",
                Interests = "code,chat,test",
                Sex = UserDetail.SexEnum.Other
            };

            CollectionAssert.AreEqual(new[] { "code", "chat", "test" }, detail.GetInterests());
            Assert.AreEqual("Repo Owner", detail.GetFullName());
            Assert.AreEqual("Display", detail.GetDisplayName());
            Assert.AreEqual("Amman, Jordan", detail.GetComposedAddress());
            Assert.AreEqual("+962 123456", detail.GetComposedPhone());
            Assert.AreEqual("Other", detail.GetSex());
        }

        [TestMethod]
        public void UserDto_HelperMethods_ReturnCopiesAndPreview()
        {
            var user = new UserDto
            {
                UserDetail = new UserDetail
                {
                    DisplayName = "Display",
                    FirstName = "First",
                    LastName = "Last",
                    Email = "mail@example.com",
                    PhoneNumber = "123",
                    PhoneCountryCode = "1",
                    Region = "North",
                    Country = "Earth"
                },
                UserStorage = new UserStorage
                {
                    AvatarImageBase64 = "avatar",
                    CoverImageBase64 = "cover"
                }
            };

            Assert.AreEqual("Display", user.GetPublicDetail()?.DisplayName);
            Assert.AreEqual("First", user.GetPrivateDetail()?.FirstName);
            Assert.AreEqual("cover", user.GetStorage()?.CoverImageBase64);
            Assert.AreEqual("avatar", user.GetAvatar()?.AvatarImageBase64);
            Assert.AreEqual("cover", user.GetCover()?.CoverImageBase64);
            Assert.AreEqual("data:image/png;base64,abc123", user.CombineAvatarForPreview(new MediaResult("PNG", "abc123")));
            Assert.IsNull(user.CombineAvatarForPreview(new MediaResult("", "abc123")));
        }
    }
}